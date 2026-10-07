using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models;
using Esertifikasi.Api.Security;
using Esertifikasi.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Esertifikasi.Api.Services.OperationsAccess;
namespace Esertifikasi.Api.Controllers;
public sealed class ObservationRequest {
  public string? RowKind { get; set; }
  [Required, MinLength(1), MaxLength(100)] public List<JsonElement> Rows { get; set; } = [];
}
public sealed partial class MonitoringController {
  private static readonly JsonSerializerOptions RowJson = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
  private static Type RowType(MonitoringType type, string? kind = null) => type switch {
    MonitoringType.LandBoundaryMarker => typeof(LandBoundaryInspection), MonitoringType.Turnera => typeof(TurneraInspection), MonitoringType.ChemicalBufferBoundary => typeof(ChemicalBufferInspection), MonitoringType.WoodyPlantAndErosionControl => typeof(WoodyPlantInspection), MonitoringType.FirstAidKit => typeof(FirstAidKitInspection), MonitoringType.PersonalProtectiveEquipment => typeof(PpeInspection), MonitoringType.HighConservationValue => kind == "location" ? typeof(HcvLocationAssessment) : typeof(ProtectedSpeciesObservation), MonitoringType.FireIncident => typeof(FireIncident), MonitoringType.WorkplaceAccident => typeof(WorkplaceAccidentIncident), MonitoringType.Weed => typeof(WeedInspection), MonitoringType.PlantDisease => typeof(PlantDiseaseInspection), MonitoringType.Pest => typeof(PestInspection), MonitoringType.MemberComplaint => typeof(MemberComplaint), _ => throw new MonitoringValidationException("Jenis tidak valid.")
  };
  private static IEnumerable<object> DetailRows(object detail) => detail.GetType().GetProperties().Where(p => p.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(p.PropertyType)).SelectMany(p => ((IEnumerable?)p.GetValue(detail))?.Cast<object>() ?? []);
  private static Guid RowId(object row) => (Guid)row.GetType().GetProperty("Id")!.GetValue(row)!;
  private static Guid ObservationId(object row) { var value = (Guid?)row.GetType().GetProperty("ObservationId")?.GetValue(row); return value.HasValue && value != Guid.Empty ? value.Value : RowId(row); }
  private async Task<bool> CanSeeComplaint(MemberComplaint row, MonitoringSubmission header, CancellationToken ct) {
    if (await _monitoring.CanReopenAsync(User, header.AssociationId, ct)) return true;
    if (row.IsAnonymous) return !row.IsConfidential && await _monitoring.CanManageAsync(User, header.PoktanId, ct);
    var own = row.PetaniId.HasValue && await _db.Petani.AnyAsync(x => x.Id == row.PetaniId && x.ApplicationUserId == AccessService.UserId(User), ct);
    return own || !row.IsConfidential && await _monitoring.CanManageAsync(User, header.PoktanId, ct);
  }
  private async Task<object?> VisibleDetailAsync(MonitoringSubmission header, CancellationToken ct) {
    var detail = await DetailAsync(header, ct); if (detail is null) return null;
    var ownIds = await new OperationsAccess(_db, new AccessService(_db)).Farmers(User).Select(x => x.Id).ToListAsync(ct);
    foreach (var prop in detail.GetType().GetProperties().Where(p => p.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(p.PropertyType))) {
      if (prop.GetValue(detail) is not IList list) continue;
      foreach (var row in list.Cast<object>().ToArray()) {
        if (row is FarmerLandMonitoringRow f && (!f.PetaniId.HasValue || !ownIds.Contains(f.PetaniId.Value))) list.Remove(row);
        if (row is MemberComplaint c) { if (!await CanSeeComplaint(c, header, ct)) list.Remove(row); else if (c.IsAnonymous) { c.PetaniId = null; c.FarmerNameSnapshot = null; c.NikSnapshot = null; c.LahanId = null; c.LandLegalNumberSnapshot = null; } }
        if (row is FirstAidKitInspection && !await _monitoring.CanManageAsync(User, header.PoktanId, ct)) list.Remove(row);
      }
    }
    return detail;
  }
  private static void ValidateDates(IEnumerable<FarmerLandMonitoringRow> rows, CreateMonitoringSubmissionRequest request) { foreach (var row in rows) Require(row.ObservedOn >= request.PeriodStart && row.ObservedOn <= request.PeriodEnd, "Tanggal monitoring di luar periode."); }
  private async Task PrepareComplaintsAsync(IEnumerable<MemberComplaint> rows, MonitoringSubmission h, CancellationToken ct) {
    foreach (var c in rows) {
      Require(c.ReceivedOn >= h.PeriodStart && c.ReceivedOn <= h.PeriodEnd && !string.IsNullOrWhiteSpace(c.ComplaintType) && !string.IsNullOrWhiteSpace(c.Description), "Tanggal/jenis/deskripsi pengaduan wajib.");
      Require(Enum.IsDefined(c.Status) && c.Status != ComplaintStatus.Rejected, "Gunakan Closed dengan alasan penutupan, bukan Rejected.");
      Require(c.Status != ComplaintStatus.Closed || !string.IsNullOrWhiteSpace(c.ClosureReason), "Alasan penutupan wajib.");
      Require(c.Status != ComplaintStatus.Resolved || c.ResolvedOn.HasValue, "Tanggal penyelesaian wajib.");
      Require(!c.ResolvedOn.HasValue || c.ResolvedOn >= c.ReceivedOn, "Tanggal penyelesaian sebelum pengaduan.");
      if (c.IsConfidential || c.IsAnonymous) { if (!await _monitoring.CanReopenAsync(User, h.AssociationId, ct)) throw new UnauthorizedAccessException(); }
      c.Id = c.Id == Guid.Empty ? Guid.NewGuid() : c.Id; c.MonitoringSubmissionId = h.Id; c.Petani = null;
      if (c.IsAnonymous) { c.PetaniId = null; c.FarmerNameSnapshot = null; c.NikSnapshot = null; }
      else {
        var farmer = await _db.Petani.SingleOrDefaultAsync(x => x.Id == c.PetaniId && x.PoktanId == h.PoktanId, ct) ?? throw new MonitoringValidationException("Petani pengaduan di luar Poktan.");
        c.FarmerNameSnapshot = farmer.Nama; c.NikSnapshot = farmer.Nik;
      }
      if (c.LahanId.HasValue) { var land = await _db.Lahan.SingleOrDefaultAsync(x => x.Id == c.LahanId && x.Petani.PoktanId == h.PoktanId && (c.PetaniId == null || x.PetaniId == c.PetaniId), ct) ?? throw new MonitoringValidationException("Lahan pengaduan tidak valid."); c.LandLegalNumberSnapshot = land.NoLegalitas; } else c.LandLegalNumberSnapshot = null;
    }
  }
  private async Task ValidateDetailAsync(MonitoringSubmission h, object detail, CancellationToken ct) {
    foreach (var row in DetailRows(detail)) {
      if (row is FarmerLandMonitoringRow f) {
        if (f is TurneraInspection turnera) Require(Enum.IsDefined(turnera.Condition), "Kondisi Turnera tidak valid.");
        Require(f.ObservedOn >= h.PeriodStart && f.ObservedOn <= h.PeriodEnd, "Tanggal monitoring di luar periode.");
        if (f is WoodyPlantInspection woody) { Require(woody.Observations is not null, "Observations wajib berupa array."); foreach (var o in woody.Observations!) { Require(!string.IsNullOrWhiteSpace(o.TreeName) && o.Quantity >= 0 && o.HeightCentimeters is not < 0, "Data pohon tidak valid."); o.Id = o.Id == Guid.Empty ? Guid.NewGuid() : o.Id; } }
        if (f is PpeInspection ppe) { Require(ppe.Items is not null, "Items wajib berupa array."); Require(Enum.IsDefined(ppe.Activity), "Aktivitas APD tidak valid."); foreach (var i in ppe.Items!) { Require(!string.IsNullOrWhiteSpace(i.ItemName) && (i.Condition == null || Enum.IsDefined(i.Condition.Value)), "Item APD/kondisi tidak valid."); i.Id = i.Id == Guid.Empty ? Guid.NewGuid() : i.Id; } }
        if (f is WeedInspection weed) Require(!string.IsNullOrWhiteSpace(weed.WeedType), "Jenis gulma wajib.");
        if (f is PlantDiseaseInspection disease) Require(!string.IsNullOrWhiteSpace(disease.DiseaseType), "Jenis penyakit wajib.");
        if (f is PestInspection pest) Require(!string.IsNullOrWhiteSpace(pest.PestType) && (pest.ObservedDensity == null || !string.IsNullOrWhiteSpace(pest.DensityUnit)), "Jenis/unit hama wajib.");
        if (f is ProtectedSpeciesObservation species) Require(!string.IsNullOrWhiteSpace(species.SpeciesName) && Enum.IsDefined(species.SpeciesKind), "Spesies tidak valid.");
      }
      if (row is FirstAidKitInspection kit) {
        Require(kit.Items is not null, "Items wajib berupa array.");
        Require(kit.ObservedOn >= h.PeriodStart && kit.ObservedOn <= h.PeriodEnd, "Tanggal P3K di luar periode.");
        if (kit.LocationId.HasValue) { var location = await _db.Set<FirstAidLocation>().SingleOrDefaultAsync(x => x.Id == kit.LocationId && x.AssociationId == h.AssociationId && x.IsActive, ct) ?? throw new MonitoringValidationException("Lokasi P3K tidak valid."); kit.Location = location.Name; }
        Require(!string.IsNullOrWhiteSpace(kit.Location), "Lokasi P3K wajib.");
        foreach (var item in kit.Items!) { Require(!string.IsNullOrWhiteSpace(item.ItemName) && Enum.IsDefined(item.Condition), "Item P3K tidak valid."); item.Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id; }
      }
    }
    if (detail is FireMonitoring fire) { ValidateZero(fire.NoIncidents, fire.Incidents.Count, "kebakaran"); Require(!fire.NoIncidents || !string.IsNullOrWhiteSpace(fire.ZeroIncidentDeclaration), "Deklarasi nihil wajib."); }
    if (detail is WorkplaceAccidentMonitoring accident) { ValidateZero(accident.NoIncidents, accident.Incidents.Count, "kecelakaan"); Require(!accident.NoIncidents || !string.IsNullOrWhiteSpace(accident.ZeroIncidentDeclaration), "Deklarasi nihil wajib."); }
    if (detail is MemberComplaintMonitoring complaint) { ValidateZero(complaint.NoComplaints, complaint.Complaints.Count, "pengaduan"); Require(!complaint.NoComplaints || !string.IsNullOrWhiteSpace(complaint.ZeroComplaintDeclaration), "Deklarasi nihil wajib."); }
  }
  internal async Task<List<object>> PrepareObservation(MonitoringSubmission h, ObservationRequest r, Guid observationId, CancellationToken ct) {
    Require(r.Rows.Count > 0 && r.Rows.Count <= 100, "Pilih 1-100 baris observasi.");
    if (h.Type == MonitoringType.HighConservationValue) Require(r.RowKind is null or "location" or "species", "RowKind harus location atau species."); if (h.Type is not (MonitoringType.PersonalProtectiveEquipment or MonitoringType.HighConservationValue or MonitoringType.Weed or MonitoringType.PlantDisease or MonitoringType.Pest)) Require(r.Rows.Count == 1, "Jenis ini menggunakan satu baris per observasi."); var result = new List<object>();
    foreach (var json in r.Rows) {
      Require(json.ValueKind == JsonValueKind.Object, "Baris harus objek.");
      object row; try { row = json.Deserialize(RowType(h.Type, r.RowKind), RowJson) ?? throw new JsonException(); } catch (JsonException) { throw new MonitoringValidationException("Payload observasi tidak valid."); }
      row.GetType().GetProperty("Id")!.SetValue(row, Guid.NewGuid()); row.GetType().GetProperty("ObservationId")?.SetValue(row, observationId); row.GetType().GetProperty("IsDeleted")!.SetValue(row, false); row.GetType().GetProperty("MonitoringSubmissionId")!.SetValue(row, h.Id);
      if (row is FarmerLandMonitoringRow f) { f.Petani = null; f.Lahan = null; f.FarmerNameSnapshot = null; f.NikSnapshot = null; f.LandLegalNumberSnapshot = null; f.LandAreaSnapshot = null; f.ObservationId = observationId; await _monitoring.PrepareRowsAsync([f], h.PoktanId, h.Id, ct); Require(f.LahanId.HasValue || f is PpeInspection, "Lahan wajib untuk observasi ini."); }
      if (row is LandBoundaryInspection boundary) Require(boundary.InstallationYear.HasValue && boundary.InstallationYear <= boundary.ObservedOn.Year, "Tahun pemasangan wajib dan tidak boleh setelah tahun monitoring.");
      if (row is MemberComplaint c) await PrepareComplaintsAsync([c], h, ct);
      if (row is WoodyPlantInspection w) { Require(w.Observations is not null, "Observations wajib berupa array."); foreach (var item in w.Observations!) { item.Id = Guid.NewGuid(); item.WoodyPlantInspectionId = RowId(row); } }
      if (row is PpeInspection p) { Require(p.Items is not null, "Items wajib berupa array."); foreach (var item in p.Items!) { item.Id = Guid.NewGuid(); item.PpeInspectionId = RowId(row); } }
      if (row is FirstAidKitInspection k) { Require(k.LocationId.HasValue && k.Items is not null, "Pilih lokasi dan items P3K."); foreach (var item in k.Items!) { item.Id = Guid.NewGuid(); item.FirstAidKitInspectionId = k.Id; } }
      var shell = Activator.CreateInstance(DetailType(h.Type))!;
      var collection = shell.GetType().GetProperties().First(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericArguments().Contains(row.GetType())); ((IList)collection.GetValue(shell)!).Add(row);
      await ValidateDetailAsync(h, shell, ct); result.Add(row);
    }
    var subjects = result.OfType<FarmerLandMonitoringRow>().ToList();
    Require(subjects.Select(x => (x.PetaniId, x.LahanId, x.ObservedOn)).Distinct().Count() <= 1, "Observasi harus memiliki subjek dan tanggal yang sama.");
    return result;
  }
  private static Type DetailType(MonitoringType t) => t switch { MonitoringType.LandBoundaryMarker => typeof(LandBoundaryMonitoring), MonitoringType.Turnera => typeof(TurneraMonitoring), MonitoringType.ChemicalBufferBoundary => typeof(ChemicalBufferMonitoring), MonitoringType.WoodyPlantAndErosionControl => typeof(WoodyPlantMonitoring), MonitoringType.FirstAidKit => typeof(FirstAidKitMonitoring), MonitoringType.PersonalProtectiveEquipment => typeof(PpeMonitoring), MonitoringType.HighConservationValue => typeof(HighConservationValueMonitoring), MonitoringType.FireIncident => typeof(FireMonitoring), MonitoringType.WorkplaceAccident => typeof(WorkplaceAccidentMonitoring), MonitoringType.Weed => typeof(WeedMonitoring), MonitoringType.PlantDisease => typeof(PlantDiseaseMonitoring), MonitoringType.Pest => typeof(PestMonitoring), _ => typeof(MemberComplaintMonitoring) };
  private async Task<MonitoringSubmission> Editable(Guid id, CancellationToken ct) { var h = await _db.MonitoringSubmissions.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException(); if (!await _monitoring.CanManageAsync(User, h.PoktanId, ct)) throw new UnauthorizedAccessException(); Require(h.Status != MonitoringSubmissionStatus.Finalized, "Buka report kembali sebelum mengubah."); h.Version = Guid.NewGuid(); return h; }
  [HttpPost("submissions/{id:guid}/observations")]
  public async Task<IActionResult> AddObservation(Guid id, ObservationRequest r, CancellationToken ct) {
    var h = await Editable(id, ct); var observationId = Guid.NewGuid(); var rows = await PrepareObservation(h, r, observationId, ct);
    await ClearZeroAsync(h, ct); foreach (var row in rows) _db.Add(row); await _db.SaveChangesAsync(ct); return Ok(new { ObservationId = observationId, RowIds = rows.Select(RowId) });
  }
  private async Task ClearZeroAsync(MonitoringSubmission h, CancellationToken ct) {
    if (h.Type == MonitoringType.FireIncident) { var d = await _db.Set<FireMonitoring>().SingleAsync(x => x.MonitoringSubmissionId == h.Id, ct); d.NoIncidents = false; d.ZeroIncidentDeclaration = null; }
    if (h.Type == MonitoringType.WorkplaceAccident) { var d = await _db.Set<WorkplaceAccidentMonitoring>().SingleAsync(x => x.MonitoringSubmissionId == h.Id, ct); d.NoIncidents = false; d.ZeroIncidentDeclaration = null; }
    if (h.Type == MonitoringType.MemberComplaint) { var d = await _db.Set<MemberComplaintMonitoring>().SingleAsync(x => x.MonitoringSubmissionId == h.Id, ct); d.NoComplaints = false; d.ZeroComplaintDeclaration = null; }
  }
  [HttpPut("submissions/{id:guid}/observations/{observationId:guid}")]
  public async Task<IActionResult> UpdateObservation(Guid id, Guid observationId, ObservationRequest r, CancellationToken ct) {
    var h = await Editable(id, ct); var old = DetailRows((await DetailAsync(h, ct))!).Where(x => ObservationId(x) == observationId).ToList(); if (old.Count == 0) return NotFound();
    if (old.OfType<MemberComplaint>().Any(x => x.IsConfidential || x.IsAnonymous) && !await _monitoring.CanReopenAsync(User, h.AssociationId, ct)) return Forbid();
    var rows = await PrepareObservation(h, r, observationId, ct);
    
    if (rows[0] is FirstAidKitInspection kit) { var previous = old.OfType<FirstAidKitInspection>().First(); Require(previous.LocationId == kit.LocationId, "Lokasi observasi tidak dapat diubah."); kit.Location = previous.Location; foreach (var item in kit.Items!) item.FirstAidKitInspectionId = kit.Id; }
    if (rows[0] is MemberComplaint complaint) { var previous = old.OfType<MemberComplaint>().First(); Require(previous.PetaniId == complaint.PetaniId && previous.LahanId == complaint.LahanId && previous.IsAnonymous == complaint.IsAnonymous, "Identitas pengaduan tidak dapat diubah."); complaint.FarmerNameSnapshot = previous.FarmerNameSnapshot; complaint.NikSnapshot = previous.NikSnapshot; complaint.LandLegalNumberSnapshot = previous.LandLegalNumberSnapshot; }
    // Keep historical snapshots when updating the same subject.
    foreach (var row in rows.OfType<FarmerLandMonitoringRow>()) { var previous = old.OfType<FarmerLandMonitoringRow>().FirstOrDefault(x => x.PetaniId == row.PetaniId && x.LahanId == row.LahanId); Require(previous is not null, "Subjek observasi tidak dapat diubah."); if (previous is not null) { row.FarmerNameSnapshot = previous.FarmerNameSnapshot; row.NikSnapshot = previous.NikSnapshot; row.LandLegalNumberSnapshot = previous.LandLegalNumberSnapshot; row.LandAreaSnapshot = previous.LandAreaSnapshot; } }
    await using var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(ct) : null;
    foreach (var row in old) { _db.Attach(row); row.GetType().GetProperty("IsDeleted")!.SetValue(row, true); } await _db.SaveChangesAsync(ct); foreach (var row in rows) _db.Add(row); await ClearZeroAsync(h, ct); await _db.SaveChangesAsync(ct); if (transaction is not null) await transaction.CommitAsync(ct);
    return Ok(new { ObservationId = observationId, RowIds = rows.Select(RowId) });
  }
  [HttpDelete("submissions/{id:guid}/observations/{observationId:guid}")]
  public async Task<IActionResult> DeleteObservation(Guid id, Guid observationId, CancellationToken ct) { var h = await Editable(id, ct); var rows = DetailRows((await DetailAsync(h, ct))!).Where(x => ObservationId(x) == observationId).ToList(); if (rows.Count == 0) return NotFound(); if (rows.OfType<MemberComplaint>().Any(x => x.IsConfidential || x.IsAnonymous) && !await _monitoring.CanReopenAsync(User, h.AssociationId, ct)) return Forbid(); foreach (var row in rows) { _db.Attach(row); row.GetType().GetProperty("IsDeleted")!.SetValue(row, true); } await _db.SaveChangesAsync(ct); return NoContent(); }
  [HttpGet("table")]
  public async Task<IActionResult> Table([FromQuery] MonitoringSubmissionQuery r, CancellationToken ct) {
    Require(r.Type.HasValue && Enum.IsDefined(r.Type.Value) && r.Year is >= 1900 and <= 9999, "Type dan Year valid wajib."); var scope = new OperationsAccess(_db, new AccessService(_db));
    var farmerIds = scope.Farmers(User).Where(x => x.Poktan.AssociationId == r.AssociationId && (!r.PoktanId.HasValue || x.PoktanId == r.PoktanId) && (!r.PetaniId.HasValue || x.Id == r.PetaniId)).Select(x => x.Id);
    var lands = _db.Lahan.Where(x => farmerIds.Contains(x.PetaniId) && (!r.LahanId.HasValue || x.Id == r.LahanId));
    if (!string.IsNullOrWhiteSpace(r.Search)) lands = lands.Where(x => x.Petani.Nama.Contains(r.Search) || x.NoLegalitas != null && x.NoLegalitas.Contains(r.Search));
    var page = await lands.OrderBy(x => x.Petani.Nama).ThenBy(x => x.Id).Select(x => new { LahanId = x.Id, x.PetaniId, FarmerName = x.Petani.Nama, x.Petani.Nik, x.NoLegalitas, NoLahan = x.NoPetaLahan, AreaHa = x.LuasLegalitas, AreaM2 = x.LuasLegalitas * 10000, x.Petani.PoktanId }).ToPagedResultAsync(r, ct);
    var poktanIds = page.Items.Select(x => x.PoktanId).Distinct().ToArray(); var headers = await _db.MonitoringSubmissions.Where(x => poktanIds.Contains(x.PoktanId) && x.Type == r.Type && x.PeriodStart.Year == r.Year && (!r.Status.HasValue || x.Status == r.Status)).ToListAsync(ct);
    var rows = new List<(MonitoringSubmission Header, object Row)>(); var nihil = new HashSet<Guid>(); foreach (var h in headers) { var d = await VisibleDetailAsync(h, ct); if (d is FireMonitoring { NoIncidents: true } or WorkplaceAccidentMonitoring { NoIncidents: true } or MemberComplaintMonitoring { NoComplaints: true }) nihil.Add(h.Id); if (d is not null) rows.AddRange(DetailRows(d).Select(row => (h, row))); }
    var definition = await _monitoring.EffectiveDefinitionAsync(r.Type!.Value, ct); var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Asia/Makassar").DateTime);
    return Ok(new { page.Page, page.PageSize, page.TotalCount, page.TotalPages, Items = page.Items.Select(land => { var matching = rows.Where(x => x.Row is FarmerLandMonitoringRow f && (f.LahanId == land.LahanId || f is PpeInspection && f.LahanId == null && f.PetaniId == land.PetaniId) || x.Row is MemberComplaint c && c.LahanId == land.LahanId).ToList(); var observations = matching.GroupBy(x => ObservationId(x.Row)).Select(g => new { ObservationId = g.Key, SubmissionId = g.First().Header.Id, Status = g.First().Header.Status, Rows = g.Select(x => x.Row) }).ToList(); var dates = matching.Select(x => x.Row is FarmerLandMonitoringRow f ? f.ObservedOn : ((MemberComplaint)x.Row).ReceivedOn).ToList(); var dueEnd = new DateOnly(r.Year!.Value, definition.Frequency == MonitoringFrequency.SemiAnnual && today.Month <= 6 ? 6 : 12, definition.Frequency == MonitoringFrequency.SemiAnnual && today.Month <= 6 ? 30 : 31); var currentStart = definition.Frequency == MonitoringFrequency.SemiAnnual && dueEnd.Month == 12 ? new DateOnly(r.Year.Value, 7, 1) : new DateOnly(r.Year.Value, 1, 1); var requiredInPeriod = definition.Frequency == MonitoringFrequency.SemiAnnual ? Math.Max(1, (int)Math.Ceiling(definition.RequiredOccurrencesPerYear / 2d)) : definition.RequiredOccurrencesPerYear; var current = matching.Where(x => x.Header.Status == MonitoringSubmissionStatus.Finalized && (x.Row is FarmerLandMonitoringRow f ? f.ObservedOn : ((MemberComplaint)x.Row).ReceivedOn) >= currentStart).Select(x => ObservationId(x.Row)).Distinct().Count() >= requiredInPeriod || headers.Any(h => h.PoktanId == land.PoktanId && h.Status == MonitoringSubmissionStatus.Finalized && h.PeriodStart <= currentStart && h.PeriodEnd >= dueEnd && nihil.Contains(h.Id)); return new { Land = land, ObservationCount = observations.Count, LatestObservedOn = dates.Count == 0 ? (DateOnly?)null : dates.Max(), MonitoringStatus = current ? "Current" : today > dueEnd ? "Overdue" : "Pending", Observations = observations }; }) });
  }
}
