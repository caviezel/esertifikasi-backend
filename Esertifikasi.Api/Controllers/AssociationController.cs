using System.ComponentModel.DataAnnotations;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models;
using Esertifikasi.Api.Security;
using Esertifikasi.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Esertifikasi.Api.Controllers;

[ApiController, Route("api/associations"), Authorize]
public sealed class AssociationController : ControllerBase {
  private readonly AppDbContext _db;
  private readonly AccessService _access;
  private readonly CertificationWorkflowService _workflow;
  private readonly CertificationProgressService _progress;

  public AssociationController(AppDbContext db, AccessService access, CertificationWorkflowService workflow,
      CertificationProgressService progress) {
    _db = db;
    _access = access;
    _workflow = workflow;
    _progress = progress;
  }

  [HttpGet("{associationId:guid}/current-certification-cycle")]
  [HttpGet("{associationId:guid}/certification-cycles/current")]
  public async Task<ActionResult<CurrentCertificationCycleResponse>> CurrentCertificationCycle(
      Guid associationId, CancellationToken ct) {
    var canAccessAssociation = await _access.CanAccessAssociationAsync(User, associationId, ct);
    var isIcsAuditor = !canAccessAssociation && await _access.IsIcsAuditorForAnyPoktanInAssociationAsync(User, associationId, ct);
    if (!canAccessAssociation && !isIcsAuditor) return Forbid();
    if (!await _db.Associations.AnyAsync(x => x.Id == associationId, ct)) return NotFound();

    var cycle = await _db.CertificationCycles.Where(x => x.AssociationId == associationId && x.IsCurrent)
        .Select(x => new CertificationCycleResponse(x.Id, x.AssociationId, x.Association.Nama, x.Type,
            x.SequenceNumber, x.Status, x.CurrentPhase, x.StartDate, x.TargetAuditStartDate,
            x.TargetAuditEndDate, x.CompletedDate, x.IsCurrent)).SingleOrDefaultAsync(ct);
    if (cycle is null) return NotFound();

    var progress = await _progress.ComputeAsync(cycle.Id, null, ct);
    if (progress is null) return NotFound();

    var canManageCycle = await _access.CanManageCertificationCycleAsync(User, cycle.Id, ct);
    var userId = AccessService.UserId(User);
    var canManagePreparation = canManageCycle || userId is not null
        && await _db.PoktanAdminAssignments.AnyAsync(x => x.UserId == userId
            && x.Poktan.AssociationId == associationId, ct);
    var canManageInternalAudit = canManageCycle || isIcsAuditor;
    var isActive = cycle.IsCurrent && cycle.Status == CertificationCycleStatus.Active;
    var isSuperAdmin = _access.IsSuperAdmin(User);
    var permissions = new CertificationCyclePermissions(
        canManageCycle, canManagePreparation, canManageCycle && isActive,
        canManageCycle && isActive,
        true, canManageCycle && isActive,
        true, canManagePreparation && isActive,
        true, canManagePreparation && isActive,
        true, canManageCycle && isActive, isSuperAdmin && isActive,
        true, canManagePreparation && isActive, isSuperAdmin && isActive,
        true, canManageInternalAudit && isActive,
        true, canManageCycle && isActive,
        true, canManageCycle && isActive,
        canManageCycle, canManageCycle && isActive,
        true, canManagePreparation || isIcsAuditor,
        true, canManagePreparation || isIcsAuditor);

    return Ok(new CurrentCertificationCycleResponse(
        cycle, progress, permissions, BuildPageAvailability(cycle, permissions)));
  }

  private static IReadOnlyList<CertificationPageAvailability> BuildPageAvailability(
      CertificationCycleResponse cycle, CertificationCyclePermissions permissions) {
    var pages = new[] {
      ("baseline-assessment", CertificationPhase.Disclosure),
      ("disclosure", CertificationPhase.Disclosure),
      ("document-verification", CertificationPhase.Preparation),
      ("internal-audit", CertificationPhase.InternalAudit),
      ("external-audit", CertificationPhase.ExternalAudit),
      ("certificates", CertificationPhase.CertificateIssuance),
      ("settings", CertificationPhase.Disclosure),
      ("training", CertificationPhase.Preparation),
      ("monitoring", CertificationPhase.Preparation)
    };
    var parallelPages = new HashSet<string> {
      "disclosure", "document-verification", "internal-audit", "external-audit"
    };
    return pages.Select(page => {
      var isOngoing = page.Item1 is "training" or "monitoring";
      if (isOngoing) return new CertificationPageAvailability(page.Item1, page.Item2, true, false, PageCanEdit(page.Item1, permissions));
      var isParallel = parallelPages.Contains(page.Item1);
      var available = isParallel || cycle.CurrentPhase >= page.Item2;
      var canEdit = available && cycle.Status == CertificationCycleStatus.Active
          && (isParallel || cycle.CurrentPhase == page.Item2)
          && PageCanEdit(page.Item1, permissions);
      return new CertificationPageAvailability(
          page.Item1, page.Item2, available, isParallel || cycle.CurrentPhase == page.Item2, canEdit);
    }).ToList();
  }

  private static bool PageCanEdit(string key, CertificationCyclePermissions permissions) => key switch {
    "baseline-assessment" => permissions.CanManageBaseline,
    "disclosure" => permissions.CanManageDisclosure,
    "document-verification" => permissions.CanManageDocuments,
    "internal-audit" => permissions.CanManageInternalAudit,
    "external-audit" => permissions.CanManageExternalAudit,
    "certificates" => permissions.CanIssueCertificate,
    "settings" => permissions.CanManageSettings,
    "training" => permissions.CanManageTraining,
    "monitoring" => permissions.CanManageMonitoring,
    _ => false
  };

  [HttpGet]
  public async Task<ActionResult<PagedResult<AssociationResponse>>> Get(
      [FromQuery] AssociationQuery request,
      CancellationToken ct) {
    var query = _access.AccessibleAssociations(User);

    if (!string.IsNullOrWhiteSpace(request.Search)) {
      var search = request.Search.Trim();
      query = query.Where(x =>
          EF.Functions.ILike(x.Nama, $"%{search}%")
          || (x.NoTelp != null && EF.Functions.ILike(x.NoTelp, $"%{search}%")));
    }

    query = (request.SortBy?.ToLowerInvariant(), request.Descending) switch {
      ("notelp", false) => query.OrderBy(x => x.NoTelp),
      ("notelp", true) => query.OrderByDescending(x => x.NoTelp),
      ("id", false) => query.OrderBy(x => x.Id),
      ("id", true) => query.OrderByDescending(x => x.Id),
      (_, true) => query.OrderByDescending(x => x.Nama),
      _ => query.OrderBy(x => x.Nama)
    };

    var result = await query
        .Select(x => new AssociationResponse(x.Id, x.Nama, x.LevelOrganisasi, x.JenisOrganisasi,
            x.KetuaOrganisasi, x.Bendahara, x.SekretarisOrganisasi, x.Bidang,
            x.NoTelp, x.ProvinceId, x.RegencyId, x.DistrictId, x.DesaId))
        .ToPagedResultAsync(request, ct);

    return Ok(result);
  }

  [HttpPost, Authorize(Roles = AppRoles.SuperAdmin)]
  public async Task<IActionResult> Create(AssociationRequest request, CancellationToken ct) {
    if (!await HasValidRegionHierarchyAsync(request, ct))
      return ValidationProblem("Wilayah organisasi tidak ditemukan, tidak aktif, atau hierarkinya tidak sesuai.");
    var entity = new Association {
      Id = Guid.NewGuid(),
      Nama = request.Nama.Trim()
    };
    request.ApplyTo(entity);

    _db.Associations.Add(entity);
    var userId = AccessService.UserId(User)!.Value;
    var cycle = _workflow.CreateInitialCycle(entity, userId);
    _db.CertificationCycles.Add(cycle);
    var documentTypes = await _db.DocumentTypes.Where(x => x.IsActive).ToListAsync(ct);
    foreach (var type in documentTypes) cycle.DocumentRequirements.Add(new CycleDocumentRequirement {
      Id = Guid.NewGuid(), DocumentTypeId = type.Id,
      OwnerType = type.OwnerType == DocumentOwnerType.Petani ? DocumentOwnerType.CertificationParticipant
          : type.OwnerType == DocumentOwnerType.Lahan ? DocumentOwnerType.CertificationParticipantLahan : type.OwnerType,
      IsRequired = type.IsRequired
    });
    await _db.SaveChangesAsync(ct);

    return Created($"/api/associations/{entity.Id}", AssociationResponse.From(entity));
  }

  [HttpGet("{id:guid}")]
  public async Task<ActionResult<AssociationResponse>> GetById(Guid id, CancellationToken ct) {
    if (!await _access.CanAccessAssociationAsync(User, id, ct)) {
      return Forbid();
    }

    var row = await _db.Associations
        .Where(x => x.Id == id)
        .Select(x => new AssociationResponse(x.Id, x.Nama, x.LevelOrganisasi, x.JenisOrganisasi,
            x.KetuaOrganisasi, x.Bendahara, x.SekretarisOrganisasi, x.Bidang,
            x.NoTelp, x.ProvinceId, x.RegencyId, x.DistrictId, x.DesaId))
        .SingleOrDefaultAsync(ct);

    return row is null ? NotFound() : Ok(row);
  }

  [HttpGet("{associationId:guid}/summary")]
  public async Task<ActionResult<AssociationSummaryResponse>> GetSummary(
      Guid associationId,
      CancellationToken ct) {
    if (!await _access.CanAccessAssociationAsync(User, associationId, ct)) {
      return Forbid();
    }

    var summary = await _db.Associations
        .Where(x => x.Id == associationId)
        .Select(x => new AssociationSummaryResponse(
            x.Poktan.Count(),
            x.Poktan.SelectMany(poktan => poktan.Petani).Count(),
            x.Poktan.SelectMany(poktan => poktan.Petani)
                .SelectMany(petani => petani.Lahan)
                .Count(),
            x.Poktan.SelectMany(poktan => poktan.Petani)
                .SelectMany(petani => petani.Lahan)
                .Sum(lahan => lahan.LuasLegalitas) ?? 0m))
        .SingleOrDefaultAsync(ct);

    return summary is null ? NotFound() : Ok(summary);
  }

  [HttpGet("{associationId:guid}/certification-dashboard")]
  public async Task<IActionResult> GetCertificationDashboard(Guid associationId, CancellationToken ct) {
    if (!await _access.CanAccessAssociationAsync(User, associationId, ct)) return Forbid();
    var cycle = await _db.CertificationCycles.Where(x => x.AssociationId == associationId && x.IsCurrent)
        .Select(x => new {
          x.Id, x.Type, x.SequenceNumber, x.Status, x.CurrentPhase, x.StartDate,
          x.TargetAuditStartDate, x.TargetAuditEndDate,
          NewParticipants = x.Participants.Count(p => p.EntryPath == ParticipantEntryPath.New && p.Status != ParticipationStatus.Excluded && p.Status != ParticipationStatus.Withdrawn),
          ExistingParticipants = x.Participants.Count(p => p.EntryPath == ParticipantEntryPath.Existing && p.Status != ParticipationStatus.Excluded && p.Status != ParticipationStatus.Withdrawn),
          IncludedLahan = x.Participants.SelectMany(p => p.Lahan).Count(l => l.Status == LahanParticipationStatus.Included),
          OpenFindings = x.Audits.SelectMany(a => a.Findings).Count(f => f.Status != FindingStatus.Closed),
          InternalAuditStatus = x.Audits.Where(a => a.Type == AuditType.Internal).Select(a => (AuditStatus?)a.Status).SingleOrDefault(),
          ExternalAuditStatus = x.Audits.Where(a => a.Type == AuditType.External).Select(a => (AuditStatus?)a.Status).SingleOrDefault(),
          CertificateStatus = x.Certificates.Select(c => (CertificateStatus?)c.Status).SingleOrDefault()
        }).SingleOrDefaultAsync(ct);
    return cycle is null ? NotFound() : Ok(cycle);
  }

  [HttpGet("{associationId:guid}/certification-database")]
  public async Task<ActionResult<PagedResult<CertificationDatabasePetaniItem>>> CertificationDatabase(
      Guid associationId, [FromQuery] CertificationDatabaseQuery request, CancellationToken ct) {
    if (!await _access.CanAccessAssociationAsync(User, associationId, ct)) return Forbid();
    if (!await _db.Associations.AnyAsync(x => x.Id == associationId, ct)) return NotFound();
    var cycleId = await _db.CertificationCycles.Where(x => x.AssociationId == associationId && x.IsCurrent)
        .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
    var participantDocumentRequired = cycleId is null ? 0 : await _db.CycleDocumentRequirements
        .Where(x => x.CertificationCycleId == cycleId && x.OwnerType == DocumentOwnerType.CertificationParticipant && x.IsRequired)
        .SumAsync(x => x.RequiredCount, ct);
    var lahanDocumentRequired = cycleId is null ? 0 : await _db.CycleDocumentRequirements
        .Where(x => x.CertificationCycleId == cycleId && x.OwnerType == DocumentOwnerType.CertificationParticipantLahan && x.IsRequired)
        .SumAsync(x => x.RequiredCount, ct);
    var baselineRequired = Enum.GetValues<BaselineAssessmentType>().Length;
    var query = _db.Petani.Where(x => x.Poktan.AssociationId == associationId);
    if (request.PoktanId is not null) query = query.Where(x => x.PoktanId == request.PoktanId);
    if (!string.IsNullOrWhiteSpace(request.Search)) {
      var search = request.Search.Trim().ToLower();
      query = query.Where(x => x.Nama.ToLower().Contains(search)
          || x.Nik != null && x.Nik.ToLower().Contains(search)
          || x.Poktan.Nama.ToLower().Contains(search)
          || x.Lahan.Any(l => l.NoLegalitas != null && l.NoLegalitas.ToLower().Contains(search)));
    }
    query = (request.SortBy?.ToLowerInvariant(), request.Descending) switch {
      ("poktan", false) => query.OrderBy(x => x.Poktan.Nama).ThenBy(x => x.Nama),
      ("poktan", true) => query.OrderByDescending(x => x.Poktan.Nama).ThenByDescending(x => x.Nama),
      ("nik", false) => query.OrderBy(x => x.Nik),
      ("nik", true) => query.OrderByDescending(x => x.Nik),
      (_, true) => query.OrderByDescending(x => x.Nama),
      _ => query.OrderBy(x => x.Nama)
    };
    return Ok(await query.Select(p => new CertificationDatabasePetaniItem(
        p.Id, p.Nama, p.Nik, p.PoktanId, p.Poktan.Nama,
        p.CertificationParticipations.Where(cp => cp.CertificationCycleId == cycleId).Select(cp => (Guid?)cp.Id).SingleOrDefault(),
        p.CertificationParticipations.Where(cp => cp.CertificationCycleId == cycleId).Select(cp => (ParticipantEntryPath?)cp.EntryPath).SingleOrDefault(),
        p.CertificationParticipations.Where(cp => cp.CertificationCycleId == cycleId).Select(cp => (ParticipationStatus?)cp.Status).SingleOrDefault(),
        participantDocumentRequired,
        p.CertificationParticipations.Where(cp => cp.CertificationCycleId == cycleId)
            .SelectMany(cp => _db.Documents.Where(d => d.CertificationParticipantId == cp.Id && d.Status == DocumentStatus.Verified)).Count(),
        p.Lahan.OrderBy(l => l.NoLegalitas).Select(l => new CertificationDatabaseLahanItem(
          l.Id, l.NoLegalitas, l.Komoditas, l.LuasLegalitas,
          l.CertificationParticipations.Where(pl => pl.CertificationParticipant.CertificationCycleId == cycleId).Select(pl => (Guid?)pl.Id).SingleOrDefault(),
          l.CertificationParticipations.Where(pl => pl.CertificationParticipant.CertificationCycleId == cycleId).Select(pl => (LahanParticipationStatus?)pl.Status).SingleOrDefault(),
          l.CertificationParticipations.Where(pl => pl.CertificationParticipant.CertificationCycleId == cycleId).Select(pl => (ProgressStatus?)pl.LandMapping!.Status).SingleOrDefault(),
          l.BaselineAssessments.Count(a => a.Status == ProgressStatus.Completed),
          l.CertificationParticipations.Any(pl => pl.CertificationParticipant.CertificationCycleId == cycleId
              && pl.EntryPath == LahanEntryPath.New) ? baselineRequired : 0,
          l.CertificationParticipations.Any(pl => pl.CertificationParticipant.CertificationCycleId == cycleId
              && _db.Set<DisclosureLahan>().Any(dl => dl.CertificationParticipantLahanId == pl.Id
                  && dl.Disclosure.CertificationCycleId == cycleId && dl.Disclosure.Status == DisclosureStatus.Completed
                  && dl.Disclosure.VersionNumber == _db.Disclosures.Where(d => d.CertificationCycleId == cycleId
                      && d.Status == DisclosureStatus.Completed).Max(d => (int?)d.VersionNumber))),
          lahanDocumentRequired,
          l.CertificationParticipations.Where(pl => pl.CertificationParticipant.CertificationCycleId == cycleId)
              .SelectMany(pl => pl.Documents).Count(d => d.Status == DocumentStatus.Verified)
        )).ToList()
      )).ToPagedResultAsync(request, ct));
  }

  [HttpGet("{associationId:guid}/certification-database/lahan")]
  public async Task<ActionResult<CertificationDatabaseLahanPage>> CertificationDatabaseLahan(
      Guid associationId, [FromQuery] CertificationDatabaseLahanQuery request, CancellationToken ct) {
    if (!await _access.CanAccessAssociationAsync(User, associationId, ct)) return Forbid();
    if (!await _db.Associations.AnyAsync(x => x.Id == associationId, ct)) return NotFound();

    var cycle = await CurrentCycleAsync(associationId, ct);
    var requiredDocuments = await RequiredMasterLahanDocumentsAsync(ct);
    var baselineRequired = Enum.GetValues<BaselineAssessmentType>().Length;
    var filtered = FilterLahan(_db.Lahan.Where(x => x.Petani.Poktan.AssociationId == associationId), cycle.Id, request);
    var totalCount = await filtered.CountAsync(ct);
    var rows = await CertificationLahanQuery(ApplyLahanSort(filtered, request), cycle.Id)
        .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
    var items = rows.Select(x => {
      var boundaryAvailable = !string.IsNullOrWhiteSpace(x.BoundaryGeoJson);
      var baselineStatus = LahanSummaryRules.BaselineStatus(x.IsCertified, boundaryAvailable,
          x.BaselineAssessmentCount, x.BaselineCompleted, baselineRequired);
      return new CertificationDatabaseLahanFlatItem(
          x.LahanId, x.PetaniId, x.PetaniName, x.PoktanId, x.PoktanName,
          x.LegalNumber, x.Commodity, x.LegalArea, x.ParticipationStatus, x.MappingStatus,
          x.BaselineCompleted, x.ParticipationStatus is null ? 0 : baselineRequired,
          x.UploadedDocuments, x.VerifiedDocuments, requiredDocuments, x.IncludedInLatestDisclosure, boundaryAvailable,
          baselineStatus, x.IsCertified,
          LahanSummaryRules.DocumentsStatus(requiredDocuments, x.UploadedDocuments, x.VerifiedDocuments));
    }).ToList();

    var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)request.PageSize);
    return Ok(new CertificationDatabaseLahanPage(cycle.Id, cycle.Type, items,
        request.Page, request.PageSize, totalCount, totalPages));
  }

  [HttpGet("{associationId:guid}/certification-database/lahan/map")]
  public async Task<ActionResult<CertificationDatabaseLahanMapResponse>> CertificationDatabaseLahanMap(
      Guid associationId, [FromQuery] CertificationDatabaseLahanQuery request, CancellationToken ct) {
    if (!await _access.CanAccessAssociationAsync(User, associationId, ct)) return Forbid();
    if (!await _db.Associations.AnyAsync(x => x.Id == associationId, ct)) return NotFound();

    var cycle = await CurrentCycleAsync(associationId, ct);
    var filtered = FilterLahan(_db.Lahan.Where(x => x.Petani.Poktan.AssociationId == associationId), cycle.Id, request);
    var rows = await CertificationLahanQuery(
        filtered.OrderBy(x => x.Petani.Poktan.Nama).ThenBy(x => x.Petani.Nama).ThenBy(x => x.NoLegalitas), cycle.Id)
        .ToListAsync(ct);
    var features = new List<GeoJsonFeature>();
    var unavailable = 0;
    foreach (var row in rows) {
      if (!TryParseGeometry(row.BoundaryGeoJson, out var geometry)) {
        unavailable++;
        continue;
      }
      features.Add(new GeoJsonFeature("Feature", row.LahanId, geometry,
          new CertificationDatabaseMapProperties(row.PetaniId, row.PetaniName,
              row.PoktanId, row.PoktanName, row.LegalNumber, row.Commodity, row.LegalArea,
              row.ParticipationStatus, row.MappingStatus)));
    }
    return Ok(new CertificationDatabaseLahanMapResponse(
        "FeatureCollection", cycle.Id, cycle.Type, features, unavailable));
  }

  private async Task<(Guid? Id, CertificationCycleType? Type)> CurrentCycleAsync(
      Guid associationId, CancellationToken ct) {
    var cycle = await _db.CertificationCycles.Where(x => x.AssociationId == associationId && x.IsCurrent)
        .Select(x => new { x.Id, x.Type }).SingleOrDefaultAsync(ct);
    return cycle is null ? (null, null) : (cycle.Id, cycle.Type);
  }

  private Task<int> RequiredMasterLahanDocumentsAsync(CancellationToken ct) =>
      _db.DocumentTypes.CountAsync(x =>
          x.OwnerType == DocumentOwnerType.Lahan && x.IsActive && x.IsRequired, ct);

  private IQueryable<CertificationDatabaseLahanProjection> CertificationLahanQuery(
      IQueryable<Lahan> query, Guid? cycleId) => query.Select(l => new CertificationDatabaseLahanProjection(
          l.Id, l.PetaniId, l.Petani.Nama, l.Petani.PoktanId, l.Petani.Poktan.Nama,
          l.NoLegalitas, l.Komoditas, l.LuasLegalitas,
          l.CertificationParticipations.Where(pl => pl.CertificationParticipant.CertificationCycleId == cycleId)
              .Select(pl => (LahanParticipationStatus?)pl.Status).SingleOrDefault(),
          l.CertificationParticipations.Where(pl => pl.CertificationParticipant.CertificationCycleId == cycleId)
              .Select(pl => (ProgressStatus?)pl.LandMapping!.Status).SingleOrDefault(),
          l.BaselineAssessments.Count(a => a.Status == ProgressStatus.Completed),
          l.BaselineAssessments.Count(),
          _db.CertificateLahan.Any(c => c.LahanId == l.Id),
          l.CertificationParticipations.Any(pl => pl.CertificationParticipant.CertificationCycleId == cycleId
              && _db.Set<DisclosureLahan>().Any(dl => dl.CertificationParticipantLahanId == pl.Id
                  && dl.Disclosure.CertificationCycleId == cycleId && dl.Disclosure.Status == DisclosureStatus.Completed
                  && dl.Disclosure.VersionNumber == _db.Disclosures.Where(d => d.CertificationCycleId == cycleId
                      && d.Status == DisclosureStatus.Completed).Max(d => (int?)d.VersionNumber))),
          l.Documents.Where(d => d.DocumentType.OwnerType == DocumentOwnerType.Lahan
              && d.DocumentType.IsActive && d.DocumentType.IsRequired)
              .Select(d => d.DocumentTypeId).Distinct().Count(),
          l.Documents.Where(d => d.Status == DocumentStatus.Verified
              && d.DocumentType.OwnerType == DocumentOwnerType.Lahan
              && d.DocumentType.IsActive && d.DocumentType.IsRequired)
              .Select(d => d.DocumentTypeId).Distinct().Count(),
          l.BoundaryGeoJson));

  private IQueryable<Lahan> FilterLahan(IQueryable<Lahan> query, Guid? cycleId,
      CertificationDatabaseLahanQuery request) {
    if (request.PoktanId is not null) query = query.Where(x => x.Petani.PoktanId == request.PoktanId);
    if (!string.IsNullOrWhiteSpace(request.Search)) {
      var search = request.Search.Trim().ToLower();
      query = query.Where(x => x.Petani.Nama.ToLower().Contains(search)
          || x.Petani.Poktan.Nama.ToLower().Contains(search)
          || x.NoLegalitas != null && x.NoLegalitas.ToLower().Contains(search)
          || x.Komoditas != null && x.Komoditas.ToLower().Contains(search));
    }
    if (request.ParticipationStatus is not null)
      query = query.Where(x => x.CertificationParticipations.Any(pl =>
          pl.CertificationParticipant.CertificationCycleId == cycleId
          && pl.Status == request.ParticipationStatus));
    if (request.MappingStatus is not null)
      query = query.Where(x => x.CertificationParticipations.Any(pl =>
          pl.CertificationParticipant.CertificationCycleId == cycleId
          && pl.LandMapping != null && pl.LandMapping.Status == request.MappingStatus));
    if (request.IncludedInLatestDisclosure is not null)
      query = query.Where(x => x.CertificationParticipations.Any(pl =>
          pl.CertificationParticipant.CertificationCycleId == cycleId
          && _db.Set<DisclosureLahan>().Any(dl => dl.CertificationParticipantLahanId == pl.Id
              && dl.Disclosure.CertificationCycleId == cycleId && dl.Disclosure.Status == DisclosureStatus.Completed
              && dl.Disclosure.VersionNumber == _db.Disclosures.Where(d => d.CertificationCycleId == cycleId
                  && d.Status == DisclosureStatus.Completed).Max(d => (int?)d.VersionNumber)))
          == request.IncludedInLatestDisclosure.Value);
    if (request.BoundaryAvailable is true) query = query.Where(x => x.BoundaryGeoJson != null);
    if (request.BoundaryAvailable is false) query = query.Where(x => x.BoundaryGeoJson == null);
    return query;
  }

  private static IQueryable<Lahan> ApplyLahanSort(
      IQueryable<Lahan> query, CertificationDatabaseLahanQuery request) =>
      (request.SortBy?.ToLowerInvariant(), request.Descending) switch {
        ("petani", false) => query.OrderBy(x => x.Petani.Nama),
        ("petani", true) => query.OrderByDescending(x => x.Petani.Nama),
        ("poktan", false) => query.OrderBy(x => x.Petani.Poktan.Nama).ThenBy(x => x.Petani.Nama),
        ("poktan", true) => query.OrderByDescending(x => x.Petani.Poktan.Nama).ThenByDescending(x => x.Petani.Nama),
        ("legalnumber", false) => query.OrderBy(x => x.NoLegalitas),
        ("legalnumber", true) => query.OrderByDescending(x => x.NoLegalitas),
        ("legalarea", false) => query.OrderBy(x => x.LuasLegalitas),
        ("legalarea", true) => query.OrderByDescending(x => x.LuasLegalitas),
        (_, true) => query.OrderByDescending(x => x.Id),
        _ => query.OrderBy(x => x.Petani.Poktan.Nama).ThenBy(x => x.Petani.Nama).ThenBy(x => x.NoLegalitas)
      };

  private static bool TryParseGeometry(string? value, out JsonElement geometry) {
    geometry = default;
    if (string.IsNullOrWhiteSpace(value)) return false;
    try {
      using var document = JsonDocument.Parse(value);
      var root = document.RootElement;
      if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out _)
          || !root.TryGetProperty("coordinates", out _)) return false;
      geometry = root.Clone();
      return true;
    }
    catch (JsonException) {
      return false;
    }
  }

  [HttpPut("{id:guid}")]
  public async Task<IActionResult> Update(Guid id, AssociationRequest request, CancellationToken ct) {
    if (!await _access.CanManageAssociationAsync(User, id, ct)) {
      return Forbid();
    }

    var entity = await _db.Associations.FindAsync(new object[] { id }, ct);
    if (entity is null) {
      return NotFound();
    }
    if (!await HasValidRegionHierarchyAsync(request, ct))
      return ValidationProblem("Wilayah organisasi tidak ditemukan, tidak aktif, atau hierarkinya tidak sesuai.");

    request.ApplyTo(entity);
    await _db.SaveChangesAsync(ct);

    return NoContent();
  }

  private Task<bool> HasValidRegionHierarchyAsync(AssociationRequest request, CancellationToken ct) =>
      request.LevelOrganisasi switch {
        OrganizationLevel.Provinsi => _db.Provinces.AnyAsync(x => x.Id == request.ProvinceId && x.IsActive, ct),
        OrganizationLevel.Kabupaten => _db.Regencies.AnyAsync(x => x.Id == request.RegencyId
            && x.ProvinceId == request.ProvinceId && x.IsActive && x.Province.IsActive, ct),
        OrganizationLevel.Kecamatan => _db.Districts.AnyAsync(x => x.Id == request.DistrictId
            && x.RegencyId == request.RegencyId && x.Regency.ProvinceId == request.ProvinceId
            && x.IsActive && x.Regency.IsActive && x.Regency.Province.IsActive, ct),
        OrganizationLevel.Desa => _db.Villages.AnyAsync(x => x.Id == request.DesaId
            && x.DistrictId == request.DistrictId && x.District.RegencyId == request.RegencyId
            && x.District.Regency.ProvinceId == request.ProvinceId && x.IsActive
            && x.District.IsActive && x.District.Regency.IsActive && x.District.Regency.Province.IsActive, ct),
        _ => Task.FromResult(false)
      };

  [HttpDelete("{id:guid}")]
  public async Task<IActionResult> Delete(Guid id, CancellationToken ct) {
    if (!await _access.CanManageAssociationAsync(User, id, ct)) {
      return Forbid();
    }

    var entity = await _db.Associations.FindAsync(new object[] { id }, ct);
    if (entity is null) {
      return NotFound();
    }

    entity.IsDeleted = true;
    entity.DeletedAt = DateTimeOffset.UtcNow;
    await _db.SaveChangesAsync(ct);

    return NoContent();
  }
}

public sealed class AssociationRequest : IValidatableObject {
  [Required, StringLength(255)]
  public string Nama { get; set; } = string.Empty;

  public OrganizationLevel LevelOrganisasi { get; set; }
  [Required] public OrganizationType? JenisOrganisasi { get; set; }

  [Required, StringLength(255)] public string KetuaOrganisasi { get; set; } = string.Empty;
  [Required, StringLength(255)] public string Bendahara { get; set; } = string.Empty;
  [Required, StringLength(255)] public string SekretarisOrganisasi { get; set; } = string.Empty;
  [Required, StringLength(255)] public string Bidang { get; set; } = string.Empty;

  [Required, StringLength(30), RegularExpression(@"^[0-9+() .-]*$")]
  public string? NoTelp { get; set; }

  [Range(1, long.MaxValue)] public long? ProvinceId { get; set; }
  [Range(1, long.MaxValue)] public long? RegencyId { get; set; }
  [Range(1, long.MaxValue)] public long? DistrictId { get; set; }
  [Range(1, long.MaxValue)] public long? DesaId { get; set; }

  public void ApplyTo(Association entity) {
    entity.Nama = Nama.Trim();
    entity.LevelOrganisasi = LevelOrganisasi;
    entity.JenisOrganisasi = JenisOrganisasi!.Value;
    entity.KetuaOrganisasi = KetuaOrganisasi.Trim();
    entity.Bendahara = Bendahara.Trim();
    entity.SekretarisOrganisasi = SekretarisOrganisasi.Trim();
    entity.Bidang = Bidang.Trim();
    entity.NoTelp = NoTelp;
    entity.ProvinceId = ProvinceId;
    entity.RegencyId = RegencyId;
    entity.DistrictId = DistrictId;
    entity.DesaId = DesaId;
  }

  public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) {
    if (ProvinceId is null) yield return new ValidationResult("Provinsi wajib diisi.", new[] { nameof(ProvinceId) });
    if (LevelOrganisasi >= OrganizationLevel.Kabupaten && RegencyId is null)
      yield return new ValidationResult("Kabupaten wajib diisi untuk level organisasi ini.", new[] { nameof(RegencyId) });
    if (LevelOrganisasi >= OrganizationLevel.Kecamatan && DistrictId is null)
      yield return new ValidationResult("Kecamatan wajib diisi untuk level organisasi ini.", new[] { nameof(DistrictId) });
    if (LevelOrganisasi >= OrganizationLevel.Desa && DesaId is null)
      yield return new ValidationResult("Desa wajib diisi untuk level organisasi ini.", new[] { nameof(DesaId) });
  }
}

public sealed record AssociationResponse(Guid Id, string Nama, OrganizationLevel LevelOrganisasi, OrganizationType? JenisOrganisasi,
    string KetuaOrganisasi, string Bendahara, string SekretarisOrganisasi, string Bidang,
    string? NoTelp, long? ProvinceId, long? RegencyId, long? DistrictId, long? DesaId) {
  public static AssociationResponse From(Association entity) => new(entity.Id, entity.Nama,
      entity.LevelOrganisasi, entity.JenisOrganisasi, entity.KetuaOrganisasi, entity.Bendahara, entity.SekretarisOrganisasi,
      entity.Bidang, entity.NoTelp, entity.ProvinceId, entity.RegencyId, entity.DistrictId, entity.DesaId);
}

public sealed class AssociationQuery : PagedQuery { }
