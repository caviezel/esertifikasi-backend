using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models;
using Esertifikasi.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api.Services;

public sealed class MonitoringValidationException : Exception {
  public MonitoringValidationException(string message) : base(message) { }
}

public sealed class MonitoringService {
  private readonly AppDbContext _db;
  private readonly AccessService _access;
  public MonitoringService(AppDbContext db, AccessService access) { _db = db; _access = access; }

  public async Task<bool> CanViewAsync(System.Security.Claims.ClaimsPrincipal user, MonitoringSubmission row, CancellationToken ct) {
    if (row.Type == MonitoringType.MemberComplaint) return await CanReopenAsync(user, row.AssociationId, ct);
    if (await CanManageAsync(user, row.PoktanId, ct)) return true;
    var userId = AccessService.UserId(user);
    return userId is not null && await _db.Petani.AnyAsync(x => x.ApplicationUserId == userId && x.PoktanId == row.PoktanId, ct);
  }

  public async Task<bool> CanManageAsync(System.Security.Claims.ClaimsPrincipal user, Guid poktanId, CancellationToken ct) {
    if (await _access.CanManagePoktanAsync(user, poktanId, ct)) return true;
    var userId = AccessService.UserId(user);
    return userId is not null && await _db.IcsAuditorAssignments.AnyAsync(x => x.UserId == userId && x.PoktanId == poktanId, ct);
  }

  public async Task<bool> CanFinalizeAsync(System.Security.Claims.ClaimsPrincipal user, Guid poktanId, CancellationToken ct) =>
      await _access.CanManagePoktanAsync(user, poktanId, ct);

  public async Task<bool> CanReopenAsync(System.Security.Claims.ClaimsPrincipal user, Guid associationId, CancellationToken ct) =>
      await _access.CanManageAssociationAsync(user, associationId, ct);

  public async Task<MonitoringSubmission> CreateHeaderAsync(CreateMonitoringSubmissionRequest request, MonitoringType type,
      System.Security.Claims.ClaimsPrincipal user, CancellationToken ct) {
    if (request.PeriodEnd < request.PeriodStart) throw new MonitoringValidationException("PeriodEnd tidak boleh sebelum PeriodStart.");
    var poktan = await _db.Poktan.Where(x => x.Id == request.PoktanId).Select(x => new { x.Id, x.AssociationId }).SingleOrDefaultAsync(ct)
        ?? throw new KeyNotFoundException("Poktan tidak ditemukan.");
    if (!await CanManageAsync(user, request.PoktanId, ct)) throw new UnauthorizedAccessException();
    if (await _db.MonitoringSubmissions.AnyAsync(x => x.PoktanId == request.PoktanId && x.Type == type
        && x.PeriodStart <= request.PeriodEnd && x.PeriodEnd >= request.PeriodStart, ct))
      throw new MonitoringValidationException("Periode monitoring bertumpang tindih dengan submission yang sudah ada.");
    await ValidateEffectivePeriodAsync(type, request.PeriodStart, request.PeriodEnd, ct);
    return new MonitoringSubmission { Id = Guid.NewGuid(), AssociationId = poktan.AssociationId, PoktanId = request.PoktanId,
      Type = type, Group = (await EffectiveDefinitionAsync(type, ct)).Group, PeriodStart = request.PeriodStart, PeriodEnd = request.PeriodEnd,
      Summary = request.Summary, PreparedByName = request.PreparedByName, AcknowledgedByName = request.AcknowledgedByName,
      CreatedByUserId = AccessService.UserId(user)!.Value };
  }

  public async Task PrepareRowsAsync(IEnumerable<FarmerLandMonitoringRow> rows, Guid poktanId, Guid submissionId, CancellationToken ct) {
    foreach (var row in rows) {
      row.Id = row.Id == Guid.Empty ? Guid.NewGuid() : row.Id;
      SetSubmissionId(row, submissionId);
      if (row.PetaniId is null && row.LahanId is null) throw new MonitoringValidationException("Petani atau lahan wajib.");
      var header = await _db.MonitoringSubmissions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == submissionId, ct);
      if (row.ObservedOn == default || header is not null && (row.ObservedOn < header.PeriodStart || row.ObservedOn > header.PeriodEnd)) throw new MonitoringValidationException("Tanggal monitoring di luar periode.");
      row.ObservationId = row.ObservationId == Guid.Empty ? Guid.NewGuid() : row.ObservationId;
      if (row is LandBoundaryInspection boundary) { boundary.InstallationYear ??= boundary.InstalledOn?.Year; if (boundary.InstallationYear is < 1900 or > 9999 || boundary.MarkerCount < 0 || !Enum.IsDefined(boundary.Condition)) throw new MonitoringValidationException("Data patok tidak valid."); }
      if (row is FireIncident fire && (fire.IncidentDate == default || fire.IncidentDate > row.ObservedOn || !Enum.IsDefined(fire.Severity) || string.IsNullOrWhiteSpace(fire.Chronology))) throw new MonitoringValidationException("Tanggal/kronologi/kategori kebakaran tidak valid.");
      if (row is WorkplaceAccidentIncident accident && (accident.IncidentDate == default || accident.IncidentDate > row.ObservedOn || accident.CaseCount < 1 || !Enum.IsDefined(accident.Category) || string.IsNullOrWhiteSpace(accident.Chronology))) throw new MonitoringValidationException("Data kecelakaan tidak valid.");
      if (row is PestInspection pest && (!Enum.IsDefined(pest.Severity) || pest.ObservedDensity < 0)) throw new MonitoringValidationException("Data hama tidak valid.");
      var identity = await _db.Lahan.Where(x => x.Id == row.LahanId && x.Petani.PoktanId == poktanId)
          .Select(x => new { PetaniId = (Guid?)x.PetaniId, x.Petani.Nama, x.Petani.Nik, x.NoLegalitas, Area = x.LuasLegalitas }).SingleOrDefaultAsync(ct);
      if (row.LahanId is not null && identity is null) throw new MonitoringValidationException("Lahan tidak berada dalam Poktan submission.");
      var petani = identity ?? await _db.Petani.Where(x => x.Id == row.PetaniId && x.PoktanId == poktanId)
          .Select(x => new { PetaniId = (Guid?)x.Id, x.Nama, x.Nik, NoLegalitas = (string?)null, Area = (decimal?)null }).SingleOrDefaultAsync(ct);
      if (petani is null) throw new MonitoringValidationException("Petani tidak berada dalam Poktan submission.");
      if (row.PetaniId is not null && row.PetaniId != petani.PetaniId) throw new MonitoringValidationException("Lahan tidak dimiliki Petani yang dipilih.");
      row.PetaniId = petani.PetaniId; row.FarmerNameSnapshot = petani.Nama; row.NikSnapshot = petani.Nik;
      row.LandLegalNumberSnapshot = petani.NoLegalitas; row.LandAreaSnapshot = petani.Area;
      row.Petani = null; row.Lahan = null;
    }
  }

  private static void SetSubmissionId(FarmerLandMonitoringRow row, Guid id) => row.GetType().GetProperty("MonitoringSubmissionId")!.SetValue(row, id);

  public async Task ValidateEffectivePeriodAsync(MonitoringType type, DateOnly start, DateOnly end, CancellationToken ct) {
    var definition = await EffectiveDefinitionAsync(type, ct);
    if (!definition.IsActive) throw new MonitoringValidationException("Jenis monitoring tidak aktif.");
    if (start == default || end < start || start.Year != end.Year) throw new MonitoringValidationException("Periode tidak valid.");
    bool valid = definition.Frequency == MonitoringFrequency.SemiAnnual
      ? start.Month == 1 && start.Day == 1 && end.Month == 6 && end.Day == 30 || start.Month == 7 && start.Day == 1 && end.Month == 12 && end.Day == 31
      : start.Month == 1 && start.Day == 1 && end.Month == 12 && end.Day == 31;
    if (!valid) throw new MonitoringValidationException("Periode tidak sesuai frekuensi monitoring.");
  }

  public static void ValidatePeriod(MonitoringType type, DateOnly start, DateOnly end) {
    if (start.Year != end.Year) throw new MonitoringValidationException("Periode harus berada dalam tahun kalender yang sama.");
    var semi = type is MonitoringType.FirstAidKit or MonitoringType.HighConservationValue;
    if (semi && !((start.Month == 1 && start.Day == 1 && end.Month == 6 && end.Day == 30)
        || (start.Month == 7 && start.Day == 1 && end.Month == 12 && end.Day == 31)))
      throw new MonitoringValidationException("Monitoring semester harus menggunakan periode Januari-Juni atau Juli-Desember.");
    if (!semi && (start.Month != 1 || start.Day != 1 || end.Month != 12 || end.Day != 31))
      throw new MonitoringValidationException("Monitoring tahunan harus menggunakan periode Januari-Desember.");
  }

  public static MonitoringDefinition Definition(MonitoringType type) {
    var (name, group, frequency) = type switch {
      MonitoringType.LandBoundaryMarker => ("Patok Batas Lahan", MonitoringGroup.Ics, MonitoringFrequency.Annual),
      MonitoringType.Turnera => ("Bunga Turnera", MonitoringGroup.Environment, MonitoringFrequency.Annual),
      MonitoringType.ChemicalBufferBoundary => ("Patok Batas Semprot dan Pupuk Kimia", MonitoringGroup.Environment, MonitoringFrequency.Annual),
      MonitoringType.WoodyPlantAndErosionControl => ("Tanaman Berkayu dan Pengendalian Erosi", MonitoringGroup.Environment, MonitoringFrequency.Annual),
      MonitoringType.FirstAidKit => ("Isi Kotak P3K", MonitoringGroup.OccupationalSafety, MonitoringFrequency.SemiAnnual),
      MonitoringType.PersonalProtectiveEquipment => ("Alat Pelindung Diri", MonitoringGroup.OccupationalSafety, MonitoringFrequency.Annual),
      MonitoringType.HighConservationValue => ("Nilai Konservasi Tinggi", MonitoringGroup.Environment, MonitoringFrequency.SemiAnnual),
      MonitoringType.FireIncident => ("Kebakaran", MonitoringGroup.Environment, MonitoringFrequency.Annual),
      MonitoringType.WorkplaceAccident => ("Kecelakaan Kerja", MonitoringGroup.OccupationalSafety, MonitoringFrequency.Annual),
      MonitoringType.Weed => ("Gulma", MonitoringGroup.Budidaya, MonitoringFrequency.Annual),
      MonitoringType.PlantDisease => ("Penyakit Tanaman Sawit", MonitoringGroup.Budidaya, MonitoringFrequency.Annual),
      MonitoringType.Pest => ("Hama", MonitoringGroup.Budidaya, MonitoringFrequency.Annual),
      _ => ("Pengaduan Anggota", MonitoringGroup.Social, MonitoringFrequency.Annual)
    };
    return new MonitoringDefinition { Type = type, Name = name, Group = group, Frequency = frequency,
      RequiredOccurrencesPerYear = frequency == MonitoringFrequency.SemiAnnual ? 2 : 1 };
  }

  public async Task<MonitoringDefinition> EffectiveDefinitionAsync(MonitoringType type, CancellationToken ct) =>
      await _db.MonitoringDefinitions.AsNoTracking().SingleOrDefaultAsync(x => x.Type == type, ct) ?? Definition(type);
}
