using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models;
using Esertifikasi.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Esertifikasi.Api.Services;

namespace Esertifikasi.Api.Controllers;

[ApiController, Route("api/lahan"), Authorize]
public sealed class LahanController : ControllerBase {
  private readonly AppDbContext _db;
  private readonly AccessService _access;
  private readonly AdministrativeRegionService _regions;

  public LahanController(AppDbContext db, AccessService access, AdministrativeRegionService regions) {
    _db = db;
    _access = access;
    _regions = regions;
  }

  [HttpGet]
  public async Task<ActionResult<PagedResult<LahanListItem>>> GetAll(
      [FromQuery] LahanQuery request,
      CancellationToken ct) {
    var query = _access.AccessibleLahan(User);

    if (request.AssociationId is not null) {
      query = query.Where(x => x.Petani.Poktan.AssociationId == request.AssociationId);
    }

    if (request.PoktanId is not null) {
      query = query.Where(x => x.Petani.PoktanId == request.PoktanId);
    }

    if (request.PetaniId is not null) {
      query = query.Where(x => x.PetaniId == request.PetaniId);
    }

    if (!string.IsNullOrWhiteSpace(request.Komoditas)) {
      query = query.Where(x => x.Komoditas == request.Komoditas);
    }

    if (!string.IsNullOrWhiteSpace(request.Search)) {
      var search = request.Search.Trim();
      query = query.Where(x =>
          (x.NoLegalitas != null && EF.Functions.ILike(x.NoLegalitas, $"%{search}%"))
          || (x.Komoditas != null && EF.Functions.ILike(x.Komoditas, $"%{search}%"))
          || EF.Functions.ILike(x.Petani.Nama, $"%{search}%"));
    }

    query = (request.SortBy?.ToLowerInvariant(), request.Descending) switch {
      ("nolegalitas", false) => query.OrderBy(x => x.NoLegalitas),
      ("nolegalitas", true) => query.OrderByDescending(x => x.NoLegalitas),
      ("komoditas", false) => query.OrderBy(x => x.Komoditas),
      ("komoditas", true) => query.OrderByDescending(x => x.Komoditas),
      ("luaslegalitas", false) => query.OrderBy(x => x.LuasLegalitas),
      ("luaslegalitas", true) => query.OrderByDescending(x => x.LuasLegalitas),
      ("petani", false) => query.OrderBy(x => x.Petani.Nama),
      ("petani", true) => query.OrderByDescending(x => x.Petani.Nama),
      ("id", false) => query.OrderBy(x => x.Id),
      ("id", true) => query.OrderByDescending(x => x.Id),
      (_, true) => query.OrderByDescending(x => x.NoLegalitas),
      _ => query.OrderBy(x => x.NoLegalitas)
    };

    var result = await query
        .Select(x => new LahanListItem(
            x.Id,
            x.PetaniId,
            x.Petani.Nama,
            x.Petani.PoktanId,
            x.Petani.Poktan.Nama,
            x.Petani.Poktan.AssociationId,
            x.NoLegalitas,
            x.Komoditas,
            x.LuasLegalitas))
        .ToPagedResultAsync(request, ct);

    return Ok(result);
  }

  [HttpGet("by-petani/{petaniId:guid}")]
  public async Task<IActionResult> Get(Guid petaniId, CancellationToken ct) {
    if (!await _access.CanViewPetaniAsync(User, petaniId, ct)) {
      return Forbid();
    }

    var requiredChecks = Enum.GetValues<BaselineAssessmentType>().Length;
    var requiredDocuments = await _db.DocumentTypes.CountAsync(x =>
        x.OwnerType == DocumentOwnerType.Lahan && x.IsActive && x.IsRequired, ct);
    var rows = await _db.Lahan
        .Where(x => x.PetaniId == petaniId)
        .OrderBy(x => x.NoLegalitas)
        .Select(x => new {
          x.Id, x.PetaniId, x.NoLegalitas, x.Komoditas, x.LuasLegalitas,
          BoundaryGeoJson = x.BoundaryGeoJson,
          IsCertified = _db.CertificateLahan.Any(c => c.LahanId == x.Id),
          AssessmentCount = x.BaselineAssessments.Count(),
          CompletedChecks = x.BaselineAssessments.Where(a => a.Status == ProgressStatus.Completed)
              .Select(a => a.Type).Distinct().Count(),
          UploadedDocuments = x.Documents.Where(d => d.DocumentType.OwnerType == DocumentOwnerType.Lahan
              && d.DocumentType.IsActive && d.DocumentType.IsRequired)
              .Select(d => d.DocumentTypeId).Distinct().Count(),
          VerifiedDocuments = x.Documents.Where(d => d.Status == DocumentStatus.Verified
              && d.DocumentType.OwnerType == DocumentOwnerType.Lahan
              && d.DocumentType.IsActive && d.DocumentType.IsRequired)
              .Select(d => d.DocumentTypeId).Distinct().Count()
        })
        .ToListAsync(ct);

    return Ok(rows.Select(x => {
      var boundaryAvailable = !string.IsNullOrWhiteSpace(x.BoundaryGeoJson);
      return new PetaniLahanListItem(
          x.Id, x.PetaniId, x.NoLegalitas, x.Komoditas, x.LuasLegalitas,
          boundaryAvailable,
          LahanSummaryRules.BaselineStatus(x.IsCertified, boundaryAvailable,
              x.AssessmentCount, x.CompletedChecks, requiredChecks),
          x.IsCertified, requiredDocuments, x.UploadedDocuments, x.VerifiedDocuments,
          LahanSummaryRules.DocumentsStatus(requiredDocuments, x.UploadedDocuments, x.VerifiedDocuments));
    }));
  }

  [HttpGet("{id:guid}")]
  public async Task<ActionResult<LahanResponse>> GetById(Guid id, CancellationToken ct) {
    var entity = await _access.AccessibleLahan(User)
        .AsNoTracking()
        .SingleOrDefaultAsync(x => x.Id == id, ct);

    return entity is null ? NotFound() : Ok(LahanResponse.From(entity));
  }

  [HttpGet("{id:guid}/summary")]
  public async Task<ActionResult<LahanSummaryResponse>> Summary(Guid id, CancellationToken ct) {
    var requiredChecks = Enum.GetValues<BaselineAssessmentType>().Length;
    var row = await _access.AccessibleLahan(User).Where(x => x.Id == id).Select(x => new {
      BoundaryAvailable = x.BoundaryGeoJson != null,
      IsCertified = _db.CertificateLahan.Any(c => c.LahanId == x.Id),
      AssessmentCount = x.BaselineAssessments.Count(),
      CompletedChecks = x.BaselineAssessments.Where(a => a.Status == ProgressStatus.Completed)
          .Select(a => a.Type).Distinct().Count(),
      RequiredDocuments = _db.DocumentTypes.Count(t =>
          t.OwnerType == DocumentOwnerType.Lahan && t.IsActive && t.IsRequired),
      UploadedDocuments = _db.Documents.Where(d => d.LahanId == x.Id
          && d.DocumentType.OwnerType == DocumentOwnerType.Lahan && d.DocumentType.IsActive && d.DocumentType.IsRequired)
          .Select(d => d.DocumentTypeId).Distinct().Count(),
      VerifiedDocuments = _db.Documents.Where(d => d.LahanId == x.Id && d.Status == DocumentStatus.Verified
          && d.DocumentType.OwnerType == DocumentOwnerType.Lahan && d.DocumentType.IsActive && d.DocumentType.IsRequired)
          .Select(d => d.DocumentTypeId).Distinct().Count()
    }).SingleOrDefaultAsync(ct);
    if (row is null) return NotFound();

    var baselineStatus = LahanSummaryRules.BaselineStatus(row.IsCertified, row.BoundaryAvailable,
        row.AssessmentCount, row.CompletedChecks, requiredChecks);
    return Ok(new LahanSummaryResponse(row.BoundaryAvailable, baselineStatus, row.IsCertified,
        row.RequiredDocuments, row.UploadedDocuments, row.VerifiedDocuments,
        LahanSummaryRules.DocumentsStatus(row.RequiredDocuments, row.UploadedDocuments, row.VerifiedDocuments)));
  }

  [HttpPost]
  public async Task<IActionResult> Create(LahanRequest request, CancellationToken ct) {
    var petani = await _db.Petani
        .Where(x => x.Id == request.PetaniId)
        .Select(x => new { x.PoktanId })
        .SingleOrDefaultAsync(ct);
    if (petani is null) {
      return NotFound();
    }

    if (!await _access.CanManagePoktanAsync(User, petani.PoktanId, ct)) {
      return Forbid();
    }

    if (request.DesaId is not null && !await _regions.IsActiveVillageAsync(request.DesaId.Value, ct)) {
      return ValidationProblem(new ValidationProblemDetails { Errors = { [nameof(request.DesaId)] = new[] { "DesaId tidak ditemukan atau tidak aktif." } } });
    }

    if (!MultiPolygonGeoJson.TryNormalize(request.Boundary, out var boundaryGeoJson, out var boundaryError)) {
      return ValidationProblem(new ValidationProblemDetails { Errors = { [nameof(request.Boundary)] = new[] { boundaryError! } } });
    }

    var entity = new Lahan { Id = Guid.NewGuid() };
    request.ApplyTo(entity);
    entity.BoundaryGeoJson = boundaryGeoJson;

    _db.Lahan.Add(entity);
    await _db.SaveChangesAsync(ct);

    return Created($"/api/lahan/{entity.Id}", new { entity.Id });
  }

  [HttpPut("{id:guid}")]
  public async Task<IActionResult> Update(Guid id, LahanRequest request, CancellationToken ct) {
    var entity = await _db.Lahan.Include(x => x.Petani).SingleOrDefaultAsync(x => x.Id == id, ct);
    if (entity is null) {
      return NotFound();
    }

    if (!await _access.CanManagePoktanAsync(User, entity.Petani.PoktanId, ct)) {
      return Forbid();
    }

    if (entity.PetaniId != request.PetaniId) {
      var targetPoktanId = await _db.Petani
          .Where(x => x.Id == request.PetaniId)
          .Select(x => (Guid?)x.PoktanId)
          .SingleOrDefaultAsync(ct);
      if (targetPoktanId is null) {
        return ValidationProblem(new ValidationProblemDetails {
          Errors = { [nameof(request.PetaniId)] = new[] { "Petani tidak ditemukan." } }
        });
      }

      if (!await _access.CanManagePoktanAsync(User, targetPoktanId.Value, ct)) {
        return Forbid();
      }
    }


    if (request.DesaId is not null && !await _regions.IsActiveVillageAsync(request.DesaId.Value, ct)) {
      return ValidationProblem(new ValidationProblemDetails { Errors = { [nameof(request.DesaId)] = new[] { "DesaId tidak ditemukan atau tidak aktif." } } });
    }

    if (!MultiPolygonGeoJson.TryNormalize(request.Boundary, out var boundaryGeoJson, out var boundaryError)) {
      return ValidationProblem(new ValidationProblemDetails { Errors = { [nameof(request.Boundary)] = new[] { boundaryError! } } });
    }

    request.ApplyTo(entity);
    entity.BoundaryGeoJson = boundaryGeoJson;
    await _db.SaveChangesAsync(ct);

    return NoContent();
  }

  [HttpGet("{id:guid}/baseline")]
  public async Task<IActionResult> Baseline(Guid id, CancellationToken ct) {
    if (!await _access.AccessibleLahan(User).AnyAsync(x => x.Id == id, ct)) return NotFound();
    return Ok(await _db.BaselineAssessments.Where(x => x.LahanId == id).OrderBy(x => x.Type)
        .Select(x => new BaselineAssessmentSummaryResponse(
            x.Type, x.Status, x.Result, x.Source, x.Notes, x.AssessedAt)).ToListAsync(ct));
  }

  [HttpPut("{id:guid}/baseline")]
  public async Task<IActionResult> Baseline(Guid id, BaselineAssessmentRequest request, CancellationToken ct) {
    var lahan = await _db.Lahan.Where(x => x.Id == id)
        .Select(x => new { x.Petani.PoktanId, x.BoundaryGeoJson }).SingleOrDefaultAsync(ct);
    if (lahan is null) return NotFound();
    if (!await _access.CanManagePoktanAsync(User, lahan.PoktanId, ct)) return Forbid();
    if (await _db.CertificateLahan.AnyAsync(x => x.LahanId == id, ct))
      return Conflict(new { message = "Baseline assessment tidak perlu diulang untuk Lahan yang telah tersertifikasi." });
    if (string.IsNullOrWhiteSpace(lahan.BoundaryGeoJson))
      return Conflict(new { code = "LAHAN_BOUNDARY_MISSING", message = "Batas Lahan harus tersedia sebelum baseline assessment dilakukan." });

    var assessment = await _db.BaselineAssessments.SingleOrDefaultAsync(x => x.LahanId == id && x.Type == request.Type, ct);
    assessment ??= new BaselineAssessment { Id = Guid.NewGuid(), LahanId = id, Type = request.Type };
    if (_db.Entry(assessment).State == EntityState.Detached) _db.BaselineAssessments.Add(assessment);
    assessment.Status = request.Status;
    assessment.Result = request.Result;
    assessment.Source = request.Source;
    assessment.Notes = request.Notes;
    assessment.AssessedByUserId = AccessService.UserId(User);
    assessment.AssessedAt = DateTimeOffset.UtcNow;
    await _db.SaveChangesAsync(ct);
    return Ok(new { assessment.Id });
  }

  [HttpGet("~/api/associations/{associationId:guid}/baseline-readiness")]
  public async Task<ActionResult<AssociationBaselineSummaryResponse>> BaselineReadiness(
      Guid associationId, CancellationToken ct) {
    if (!await _access.CanAccessAssociationAsync(User, associationId, ct)) return Forbid();
    if (!await _db.Associations.AnyAsync(x => x.Id == associationId, ct)) return NotFound();

    var required = Enum.GetValues<BaselineAssessmentType>().Length;
    var rows = await _db.Lahan.Where(x => x.Petani.Poktan.AssociationId == associationId)
        .OrderBy(x => x.Petani.Nama).ThenBy(x => x.NoLegalitas)
        .Select(x => new {
          x.Id, x.NoLegalitas, x.PetaniId, PetaniName = x.Petani.Nama,
          BoundaryAvailable = x.BoundaryGeoJson != null,
          IsCertified = _db.CertificateLahan.Any(c => c.LahanId == x.Id)
        }).ToListAsync(ct);
    var lahanIds = rows.Select(x => x.Id).ToArray();
    var assessmentRows = await _db.BaselineAssessments.Where(x => lahanIds.Contains(x.LahanId))
        .OrderBy(x => x.Type).Select(x => new {
          x.LahanId, Assessment = new BaselineAssessmentSummaryResponse(
              x.Type, x.Status, x.Result, x.Source, x.Notes, x.AssessedAt)
        }).ToListAsync(ct);
    var assessments = assessmentRows.GroupBy(x => x.LahanId)
        .ToDictionary(x => x.Key, x => (IReadOnlyList<BaselineAssessmentSummaryResponse>)x.Select(a => a.Assessment).ToList());
    var result = rows.Select(x => {
      var lahanAssessments = assessments.GetValueOrDefault(x.Id)
          ?? Array.Empty<BaselineAssessmentSummaryResponse>();
      var completed = lahanAssessments.Where(a => a.Status == ProgressStatus.Completed)
          .Select(a => a.Type).Distinct().Count();
      var status = x.IsCertified ? BaselineSummaryStatus.Certified
          : lahanAssessments.Count == 0 ? BaselineSummaryStatus.NotAssessed
          : x.BoundaryAvailable && completed == required ? BaselineSummaryStatus.Complete
          : BaselineSummaryStatus.Incomplete;
      return new BaselineLahanSummaryResponse(x.Id, x.NoLegalitas, x.PetaniId, x.PetaniName,
          x.IsCertified, x.BoundaryAvailable, status, x.IsCertified ? 0 : required, completed, lahanAssessments);
    }).ToList();
    return Ok(new AssociationBaselineSummaryResponse(associationId, result.Count,
        result.Count(x => x.SummaryStatus is BaselineSummaryStatus.Complete or BaselineSummaryStatus.Certified),
        result.Count(x => x.SummaryStatus is BaselineSummaryStatus.NotAssessed or BaselineSummaryStatus.Incomplete), result));
  }

  [HttpDelete("{id:guid}")]
  public async Task<IActionResult> Delete(Guid id, CancellationToken ct) {
    var entity = await _db.Lahan.Include(x => x.Petani).SingleOrDefaultAsync(x => x.Id == id, ct);
    if (entity is null) {
      return NotFound();
    }

    if (!await _access.CanManagePoktanAsync(User, entity.Petani.PoktanId, ct)) {
      return Forbid();
    }

    entity.IsDeleted = true;
    entity.DeletedAt = DateTimeOffset.UtcNow;
    await _db.SaveChangesAsync(ct);

    return NoContent();
  }
}

public class LahanRequest {
  [NotEmptyGuid] public Guid PetaniId { get; set; }
  [StringLength(255)] public string? NoLegalitas { get; set; }
  [StringLength(255)] public string? NoSppl { get; set; }
  [StringLength(255)] public string? NoStdb { get; set; }
  [StringLength(100)] public string? JenisKepemilikan { get; set; }
  [Range(0, 9999)] public int? TahunKepemilikan { get; set; }
  [StringLength(100)] public string? StatusKepemilikan { get; set; }
  [StringLength(1000)] public string? KeteranganSertifikat { get; set; }
  [StringLength(255)] public string? KemanaMenjualPanen { get; set; }
  [StringLength(255)] public string? NamaPembeli { get; set; }
  [StringLength(255)] public string? NamaPabrik { get; set; }
  [StringLength(100)] public string? Komoditas { get; set; }
  [Range(typeof(decimal), "0", "9999999999999999")] public decimal? LuasLegalitas { get; set; }
  [StringLength(255)] public string? BatasUtara { get; set; }
  [StringLength(255)] public string? BatasTimur { get; set; }
  [StringLength(255)] public string? BatasBarat { get; set; }
  [StringLength(255)] public string? BatasSelatan { get; set; }
  public SoilType? JenisTanah { get; set; }
  [StringLength(255)] public string? TanamanLain { get; set; }
  public DateOnly? BulanTahunTanam { get; set; }
  [Range(typeof(decimal), "0", "9999999999999999")] public decimal? ProduksiRataRata { get; set; }
  [StringLength(255)] public string? TempatBeliPupuk { get; set; }
  public AvailabilityStatus? MitraPengelola { get; set; }
  public AvailabilityStatus? MitraPengelolaLainnya { get; set; }
  [Range(0, int.MaxValue)] public int? JumlahPohonPerHa { get; set; }
  public CroppingPattern? PolaTanam { get; set; }
  [StringLength(255)] public string? AsalBibit { get; set; }
  [StringLength(255)] public string? JenisBibit { get; set; }
  public bool? BibitBersertifikat { get; set; }
  [StringLength(255)] public string? BisnisLain { get; set; }
  [Range(typeof(decimal), "0", "9999999999999999")] public decimal? LuasTertanam { get; set; }
  [Range(typeof(decimal), "0", "9999999999999999")] public decimal? LuasProduktif { get; set; }
  [Range(0, int.MaxValue)] public int? RotasiPanen { get; set; }
  [Range(typeof(decimal), "0", "9999999999999999")] public decimal? BeratTbs { get; set; }
  [Required, Range(1, long.MaxValue)] public long? DesaId { get; set; }
  [Required, StringLength(20)] public string? RtRw { get; set; }
  [Required, StringLength(1000)] public string? Alamat { get; set; }
  public JsonElement? Boundary { get; set; }
  [StringLength(255)] public string? NoPetaLahan { get; set; }
  [Required, StringLength(255)] public string? MetodeBuka { get; set; }
  [Required, StringLength(255)] public string? DibukaOleh { get; set; }
  [Required, Range(1, 9999)] public int? TahunDibuka { get; set; }
  [Required, StringLength(255)] public string? TutupanLahan { get; set; }
  [StringLength(255)] public string? TutupanLahanLainnya { get; set; }
  [Required] public LandAcquisition? PerolehanTanahGarapan { get; set; }
  [Required] public LandUse? PeruntukanTanah { get; set; }
  [Required, StringLength(255)] public string? TanamanAwal { get; set; }
  public bool? StatusHgu { get; set; }
  public bool? StatusGambutFeg { get; set; }
  public bool? StatusGambutKhg { get; set; }
  [Range(typeof(decimal), "0", "9999999999999999")] public decimal? LuasGeometri { get; set; }
  [Range(typeof(decimal), "0", "9999999999999999")] public decimal? LuasTerverifikasi { get; set; }
  public decimal? SelisihLuas { get; set; }

  public void ApplyTo(Lahan entity) {
    entity.PetaniId = PetaniId;
    entity.NoLegalitas = NoLegalitas;
    entity.NoSppl = NoSppl;
    entity.NoStdb = NoStdb;
    entity.JenisKepemilikan = JenisKepemilikan;
    entity.TahunKepemilikan = TahunKepemilikan;
    entity.StatusKepemilikan = StatusKepemilikan;
    entity.KeteranganSertifikat = KeteranganSertifikat;
    entity.KemanaMenjualPanen = KemanaMenjualPanen;
    entity.NamaPembeli = NamaPembeli;
    entity.NamaPabrik = NamaPabrik;
    entity.Komoditas = Komoditas;
    entity.LuasLegalitas = LuasLegalitas;
    entity.BatasUtara = BatasUtara;
    entity.BatasTimur = BatasTimur;
    entity.BatasBarat = BatasBarat;
    entity.BatasSelatan = BatasSelatan;
    entity.JenisTanah = JenisTanah;
    entity.TanamanLain = TanamanLain;
    entity.BulanTahunTanam = BulanTahunTanam;
    entity.ProduksiRataRata = ProduksiRataRata;
    entity.TempatBeliPupuk = TempatBeliPupuk;
    entity.MitraPengelola = MitraPengelola;
    entity.MitraPengelolaLainnya = MitraPengelolaLainnya;
    entity.JumlahPohonPerHa = JumlahPohonPerHa;
    entity.PolaTanam = PolaTanam;
    entity.AsalBibit = AsalBibit;
    entity.JenisBibit = JenisBibit;
    entity.BibitBersertifikat = BibitBersertifikat;
    entity.BisnisLain = BisnisLain;
    entity.LuasTertanam = LuasTertanam;
    entity.LuasProduktif = LuasProduktif;
    entity.RotasiPanen = RotasiPanen;
    entity.BeratTbs = BeratTbs;
    entity.DesaId = DesaId;
    entity.RtRw = RtRw!.Trim();
    entity.Alamat = Alamat!.Trim();
    entity.NoPetaLahan = NoPetaLahan;
    entity.MetodeBuka = MetodeBuka!.Trim();
    entity.DibukaOleh = DibukaOleh!.Trim();
    entity.TahunDibuka = TahunDibuka!.Value;
    entity.TutupanLahan = TutupanLahan!.Trim();
    entity.TutupanLahanLainnya = TutupanLahanLainnya;
    entity.PerolehanTanahGarapan = PerolehanTanahGarapan!.Value;
    entity.PeruntukanTanah = PeruntukanTanah!.Value;
    entity.TanamanAwal = TanamanAwal!.Trim();
    entity.StatusHgu = StatusHgu;
    entity.StatusGambutFeg = StatusGambutFeg;
    entity.StatusGambutKhg = StatusGambutKhg;
    entity.LuasGeometri = LuasGeometri;
    entity.LuasTerverifikasi = LuasTerverifikasi;
    entity.SelisihLuas = SelisihLuas;
  }
}

public sealed class LahanResponse : LahanRequest {
  public Guid Id { get; init; }

  public static LahanResponse From(Lahan entity) {
    var response = new LahanResponse { Id = entity.Id };
    response.CopyFrom(entity);
    return response;
  }

  private void CopyFrom(Lahan entity) {
    PetaniId = entity.PetaniId;
    NoLegalitas = entity.NoLegalitas;
    NoSppl = entity.NoSppl;
    NoStdb = entity.NoStdb;
    JenisKepemilikan = entity.JenisKepemilikan;
    TahunKepemilikan = entity.TahunKepemilikan;
    StatusKepemilikan = entity.StatusKepemilikan;
    KeteranganSertifikat = entity.KeteranganSertifikat;
    KemanaMenjualPanen = entity.KemanaMenjualPanen;
    NamaPembeli = entity.NamaPembeli;
    NamaPabrik = entity.NamaPabrik;
    Komoditas = entity.Komoditas;
    LuasLegalitas = entity.LuasLegalitas;
    BatasUtara = entity.BatasUtara;
    BatasTimur = entity.BatasTimur;
    BatasBarat = entity.BatasBarat;
    BatasSelatan = entity.BatasSelatan;
    JenisTanah = entity.JenisTanah;
    TanamanLain = entity.TanamanLain;
    BulanTahunTanam = entity.BulanTahunTanam;
    ProduksiRataRata = entity.ProduksiRataRata;
    TempatBeliPupuk = entity.TempatBeliPupuk;
    MitraPengelola = entity.MitraPengelola;
    MitraPengelolaLainnya = entity.MitraPengelolaLainnya;
    JumlahPohonPerHa = entity.JumlahPohonPerHa;
    PolaTanam = entity.PolaTanam;
    AsalBibit = entity.AsalBibit;
    JenisBibit = entity.JenisBibit;
    BibitBersertifikat = entity.BibitBersertifikat;
    BisnisLain = entity.BisnisLain;
    LuasTertanam = entity.LuasTertanam;
    LuasProduktif = entity.LuasProduktif;
    RotasiPanen = entity.RotasiPanen;
    BeratTbs = entity.BeratTbs;
    DesaId = entity.DesaId;
    RtRw = entity.RtRw;
    Alamat = entity.Alamat;
    Boundary = ParseBoundary(entity.BoundaryGeoJson);
    NoPetaLahan = entity.NoPetaLahan;
    MetodeBuka = entity.MetodeBuka;
    DibukaOleh = entity.DibukaOleh;
    TahunDibuka = entity.TahunDibuka;
    TutupanLahan = entity.TutupanLahan;
    TutupanLahanLainnya = entity.TutupanLahanLainnya;
    PerolehanTanahGarapan = entity.PerolehanTanahGarapan;
    PeruntukanTanah = entity.PeruntukanTanah;
    TanamanAwal = entity.TanamanAwal;
    StatusHgu = entity.StatusHgu;
    StatusGambutFeg = entity.StatusGambutFeg;
    StatusGambutKhg = entity.StatusGambutKhg;
    LuasGeometri = entity.LuasGeometri;
    LuasTerverifikasi = entity.LuasTerverifikasi;
    SelisihLuas = entity.SelisihLuas;
  }

  private static JsonElement? ParseBoundary(string? geoJson) {
    if (string.IsNullOrWhiteSpace(geoJson)) return null;
    using var document = JsonDocument.Parse(geoJson);
    return document.RootElement.Clone();
  }
}

public sealed record LahanListItem(
    Guid Id,
    Guid PetaniId,
    string PetaniNama,
    Guid PoktanId,
    string PoktanNama,
    Guid AssociationId,
    string? NoLegalitas,
    string? Komoditas,
    decimal? LuasLegalitas);

public sealed class LahanQuery : PagedQuery {
  public Guid? AssociationId { get; set; }
  public Guid? PoktanId { get; set; }
  public Guid? PetaniId { get; set; }
  public string? Komoditas { get; set; }
}
