using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models;
using Esertifikasi.Api.Models.Documents;
using Esertifikasi.Api.Security;
using Esertifikasi.Api.Services;
using Esertifikasi.Api.Services.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api.Controllers;

[ApiController, Route("api/certification"), Authorize]
public sealed class CertificationActivitiesController : ControllerBase {
  private readonly AppDbContext _db;
  private readonly AccessService _access;
  private readonly CertificationWorkflowService _workflow;
  private readonly CertificationReadinessService _readiness;
  private readonly DocumentUploadService _uploads;
  private readonly AuditFindingExcelService _findingExcel;

  public CertificationActivitiesController(AppDbContext db, AccessService access, CertificationWorkflowService workflow,
      CertificationReadinessService readiness, DocumentUploadService uploads, AuditFindingExcelService findingExcel) {
    _db = db;
    _access = access;
    _workflow = workflow;
    _readiness = readiness;
    _uploads = uploads;
    _findingExcel = findingExcel;
  }

  [HttpGet("cycles/{cycleId:guid}/disclosures")]
  public async Task<IActionResult> Disclosures(Guid cycleId, CancellationToken ct) {
    if (!await _access.CanAccessCertificationCycleAsync(User, cycleId, ct)) return Forbid();
    if (User.IsInRole(AppRoles.MemberTaniBaik)) return Forbid();
    return Ok(await _db.Disclosures.Where(x => x.CertificationCycleId == cycleId).OrderBy(x => x.VersionNumber)
        .Select(x => new { x.Id, x.VersionNumber, x.Status, x.CreatedAt, x.SubmittedAt, x.CompletedAt,
          x.ReviewedAt, x.ReviewNotes, ParticipantCount = x.Participants.Count, LahanCount = x.Lahan.Count }).ToListAsync(ct));
  }

  [HttpGet("disclosures/{id:guid}")]
  public async Task<IActionResult> DisclosureDetail(Guid id, CancellationToken ct) {
    var disclosure = await _db.Disclosures.Where(x => x.Id == id).Select(x => new {
      x.Id, x.CertificationCycleId, x.VersionNumber, x.Status, x.CreatedByUserId, x.CreatedAt,
      x.SubmittedAt, x.CompletedAt, x.ReviewedByUserId, x.ReviewedAt, x.ReviewNotes
    }).SingleOrDefaultAsync(ct);
    if (disclosure is null) return NotFound();
    if (!await _access.CanAccessCertificationCycleAsync(User, disclosure.CertificationCycleId, ct)) return Forbid();
    if (User.IsInRole(AppRoles.MemberTaniBaik)) return Forbid();
    var finalized = disclosure.Status == DisclosureStatus.Completed;
    var participants = await _db.CertificationParticipants.Where(x => x.CertificationCycleId == disclosure.CertificationCycleId
        && (finalized ? _db.Set<DisclosureParticipant>().Any(dp => dp.DisclosureId == id && dp.CertificationParticipantId == x.Id)
            : x.Status != ParticipationStatus.Excluded && x.Status != ParticipationStatus.Withdrawn))
        .OrderBy(x => x.Petani.Nama).Select(x => new {
          x.Id, x.PetaniId, PetaniName = x.Petani.Nama, x.PoktanIdSnapshot,
          PoktanName = x.Petani.Poktan.Nama, x.EntryPath, x.Status, x.StatusReason
        }).ToListAsync(ct);
    var participantIds = participants.Select(x => x.Id).ToArray();
    var lahan = await _db.CertificationParticipantLahan.Where(x => participantIds.Contains(x.CertificationParticipantId)
        && (finalized ? _db.Set<DisclosureLahan>().Any(dl => dl.DisclosureId == id && dl.CertificationParticipantLahanId == x.Id)
            : x.Status != LahanParticipationStatus.Withdrawn))
        .OrderBy(x => x.Lahan.NoLegalitas).Select(x => new {
          x.Id, x.CertificationParticipantId, x.LahanId, LegalNumber = x.Lahan.NoLegalitas,
          x.EntryPath, x.Status, x.StatusReason
        }).ToListAsync(ct);
    return Ok(new { disclosure.Id, disclosure.CertificationCycleId, disclosure.VersionNumber, disclosure.Status,
      disclosure.CreatedByUserId, disclosure.CreatedAt, disclosure.SubmittedAt, disclosure.CompletedAt,
      disclosure.ReviewedByUserId, disclosure.ReviewedAt, disclosure.ReviewNotes,
      IsSnapshotFinal = finalized, Participants = participants, Lahan = lahan });
  }

  [HttpPost("cycles/{cycleId:guid}/disclosures")]
  public async Task<IActionResult> CreateDisclosure(Guid cycleId, CreateDisclosureRequest request, CancellationToken ct) {
    if (!await _access.CanManageCertificationCycleAsync(User, cycleId, ct)) return Forbid();
    var cycle = await _db.CertificationCycles.Include(x => x.Disclosures).SingleOrDefaultAsync(x => x.Id == cycleId, ct);
    if (cycle is null) return NotFound();
    if (cycle.Status != CertificationCycleStatus.Active)
      throw new CertificationWorkflowException("Siklus tidak aktif.");
    if (cycle.Disclosures.Any(x => x.Status is DisclosureStatus.Draft or DisclosureStatus.PendingSecondDisclosureApproval or DisclosureStatus.ApprovedForSecondDisclosure))
      return Conflict(new { message = "Masih ada disclosure aktif." });
    var version = cycle.Disclosures.Count + 1;
    if (version == 1) {
      CertificationWorkflowService.EnsurePhase(cycle, CertificationPhase.Disclosure);
    }
    else if (cycle.CurrentPhase is not (CertificationPhase.Disclosure or CertificationPhase.Preparation)) {
      return Conflict(new { message = "Disclosure kedua hanya dapat diajukan sebelum audit internal dimulai." });
    }
    var disclosure = new Disclosure {
      Id = Guid.NewGuid(), CertificationCycleId = cycleId, VersionNumber = version,
      Status = version == 1 ? DisclosureStatus.Draft : DisclosureStatus.PendingSecondDisclosureApproval,
      CreatedByUserId = AccessService.UserId(User)!.Value, ReviewNotes = request.Notes
    };
    _db.Disclosures.Add(disclosure);
    await _db.SaveChangesAsync(ct);
    return Created($"/api/certification/cycles/{cycleId}/disclosures/{disclosure.Id}", new { disclosure.Id, disclosure.VersionNumber, disclosure.Status });
  }

  [HttpGet("cycles/{cycleId:guid}/disclosure-workspace")]
  public async Task<IActionResult> DisclosureWorkspace(
      Guid cycleId, [FromQuery] DisclosureWorkspaceQuery request, CancellationToken ct) {
    if (!await _access.CanAccessCertificationCycleAsync(User, cycleId, ct)) return Forbid();
    if (User.IsInRole(AppRoles.MemberTaniBaik)) return Forbid();
    if (!await _db.CertificationCycles.AnyAsync(x => x.Id == cycleId, ct)) return NotFound();
    return Ok(await BuildDisclosureWorkspaceAsync(cycleId, request, null, ct));
  }

  [HttpPost("cycles/{cycleId:guid}/disclosure-workspace/lahan/include")]
  public async Task<IActionResult> IncludeDisclosureWorkspaceLahan(
      Guid cycleId, BulkIncludeDisclosureLahanRequest request, CancellationToken ct) {
    if (!await _access.CanManageCertificationCycleAsync(User, cycleId, ct)) return Forbid();
    var cycle = await _db.CertificationCycles.Include(x => x.Disclosures).SingleOrDefaultAsync(x => x.Id == cycleId, ct);
    if (cycle is null) return NotFound();
    var disclosure = cycle.Disclosures.OrderByDescending(x => x.VersionNumber).FirstOrDefault(x =>
        x.Status is DisclosureStatus.Draft or DisclosureStatus.PendingSecondDisclosureApproval
            or DisclosureStatus.ApprovedForSecondDisclosure);
    if (disclosure is null) {
      if (cycle.Disclosures.Any(x => x.Status == DisclosureStatus.Completed))
        return Conflict(new { message = "Disclosure lanjutan harus dibuat dan disetujui sebelum scope dapat ditambah." });
      CertificationWorkflowService.EnsurePhase(cycle, CertificationPhase.Disclosure);
      disclosure = new Disclosure {
        Id = Guid.NewGuid(), CertificationCycleId = cycleId, VersionNumber = 1,
        Status = DisclosureStatus.Draft, CreatedByUserId = AccessService.UserId(User)!.Value
      };
      _db.Disclosures.Add(disclosure);
    }
    return await IncludeDisclosureLahanAsync(cycle, disclosure, request.LahanIds, ct);
  }

  [HttpPost("disclosures/{id:guid}/lahan/include")]
  public async Task<IActionResult> IncludeDisclosureLahan(
      Guid id, BulkIncludeDisclosureLahanRequest request, CancellationToken ct) {
    var disclosure = await _db.Disclosures.Include(x => x.CertificationCycle)
        .SingleOrDefaultAsync(x => x.Id == id, ct);
    if (disclosure is null) return NotFound();
    if (!await _access.CanManageCertificationCycleAsync(User, disclosure.CertificationCycleId, ct)) return Forbid();
    return await IncludeDisclosureLahanAsync(disclosure.CertificationCycle, disclosure, request.LahanIds, ct);
  }

  [HttpPost("disclosures/{id:guid}/lahan/exclude")]
  public async Task<IActionResult> ExcludeDisclosureLahan(
      Guid id, BulkExcludeDisclosureLahanRequest request, CancellationToken ct) {
    var disclosure = await _db.Disclosures.Include(x => x.CertificationCycle)
        .SingleOrDefaultAsync(x => x.Id == id, ct);
    if (disclosure is null) return NotFound();
    if (!await _access.CanManageCertificationCycleAsync(User, disclosure.CertificationCycleId, ct)) return Forbid();
    if (disclosure.Status is not (DisclosureStatus.Draft or DisclosureStatus.ApprovedForSecondDisclosure))
      return Conflict(new { message = "Scope hanya dapat diubah pada disclosure aktif yang dapat diedit." });
    await _workflow.EnsureScopeMutationAllowedAsync(disclosure.CertificationCycleId, ct);
    var ids = request.LahanIds.Distinct().ToArray();
    var rows = await _db.CertificationParticipantLahan.Include(x => x.CertificationParticipant).ThenInclude(x => x.Lahan)
        .Where(x => x.CertificationParticipant.CertificationCycleId == disclosure.CertificationCycleId
            && ids.Contains(x.LahanId)).ToListAsync(ct);
    if (rows.Count != ids.Length) return ValidationProblem("Salah satu Lahan belum terdaftar pada siklus ini.");
    foreach (var row in rows) {
      row.Status = LahanParticipationStatus.Excluded;
      row.StatusReason = request.Reason.Trim();
    }
    foreach (var participant in rows.Select(x => x.CertificationParticipant).Distinct()) {
      if (!participant.Lahan.Any(x => x.Status == LahanParticipationStatus.Included)) {
        participant.Status = ParticipationStatus.Excluded;
        participant.StatusReason = request.Reason.Trim();
      }
    }
    await _db.SaveChangesAsync(ct);
    var workspace = await BuildDisclosureWorkspaceAsync(disclosure.CertificationCycleId,
        new DisclosureWorkspaceQuery { PageSize = 100 }, ids, ct);
    return Ok(new DisclosureWorkspaceMutationResponse(workspace.Disclosure, workspace.Summary, workspace.Items));
  }

  private async Task<IActionResult> IncludeDisclosureLahanAsync(
      CertificationCycle cycle, Disclosure disclosure, IEnumerable<Guid> requestedIds, CancellationToken ct) {
    CertificationWorkflowService.EnsureCurrent(cycle);
    if (disclosure.Status is not (DisclosureStatus.Draft or DisclosureStatus.ApprovedForSecondDisclosure))
      return Conflict(new { message = "Scope hanya dapat diubah pada disclosure aktif yang dapat diedit." });
    await _workflow.EnsureScopeMutationAllowedAsync(cycle.Id, ct);
    var ids = requestedIds.Distinct().ToArray();
    var requiredBaseline = Enum.GetValues<BaselineAssessmentType>().Length;
    var candidates = await _db.Lahan.Where(x => ids.Contains(x.Id) && x.Petani.Poktan.AssociationId == cycle.AssociationId)
        .Select(x => new {
          Lahan = x, x.PetaniId,
          IsCertified = _db.CertificateLahan.Any(c => c.LahanId == x.Id),
          BoundaryAvailable = x.BoundaryGeoJson != null,
          BaselineCompleted = x.BaselineAssessments.Where(a => a.Status == ProgressStatus.Completed)
              .Select(a => a.Type).Distinct().Count()
        }).ToListAsync(ct);
    if (candidates.Count != ids.Length) return ValidationProblem("Salah satu Lahan tidak ditemukan pada Association siklus ini.");
    var ineligible = candidates.Where(x => !x.IsCertified
        && (!x.BoundaryAvailable || x.BaselineCompleted < requiredBaseline)).Select(x => x.Lahan.Id).ToArray();
    if (ineligible.Length != 0) return Conflict(new {
      message = "Salah satu Lahan belum memenuhi boundary atau baseline.", lahanIds = ineligible
    });

    foreach (var group in candidates.GroupBy(x => x.PetaniId)) {
      var participant = await _db.CertificationParticipants.Include(x => x.CertificationCycle).Include(x => x.Lahan)
          .SingleOrDefaultAsync(x => x.CertificationCycleId == cycle.Id && x.PetaniId == group.Key, ct);
      if (participant is null) {
        var existingPetani = await _db.Set<CertificateParticipant>().AnyAsync(x => x.PetaniId == group.Key, ct);
        participant = await _workflow.EnrollParticipantAsync(cycle, group.Key,
            existingPetani ? ParticipantEntryPath.Existing : ParticipantEntryPath.New,
            group.Select(x => x.Lahan.Id), ct);
      }
      else {
        var missing = group.Select(x => x.Lahan.Id).Where(id => participant.Lahan.All(x => x.LahanId != id)).ToArray();
        if (missing.Length != 0) {
          var added = await _workflow.AddParticipantLahanAsync(participant, missing, ct);
          foreach (var item in added) {
            item.Status = LahanParticipationStatus.Included;
            item.StatusReason = null;
          }
        }
      }
      participant.Status = ParticipationStatus.Included;
      participant.StatusReason = null;
      foreach (var item in participant.Lahan.Where(x => ids.Contains(x.LahanId))) {
        item.Status = LahanParticipationStatus.Included;
        item.StatusReason = null;
      }
    }
    await _db.SaveChangesAsync(ct);
    var workspace = await BuildDisclosureWorkspaceAsync(cycle.Id,
        new DisclosureWorkspaceQuery { PageSize = 100 }, ids, ct);
    return Ok(new DisclosureWorkspaceMutationResponse(workspace.Disclosure, workspace.Summary, workspace.Items));
  }

  private async Task<DisclosureWorkspaceResponse> BuildDisclosureWorkspaceAsync(
      Guid cycleId, DisclosureWorkspaceQuery request, Guid[]? affectedIds, CancellationToken ct) {
    var cycle = await _db.CertificationCycles.Where(x => x.Id == cycleId)
        .Select(x => new { x.AssociationId }).SingleAsync(ct);
    var disclosure = await _db.Disclosures.Where(x => x.CertificationCycleId == cycleId
            && x.Status != DisclosureStatus.Rejected)
        .OrderByDescending(x => x.VersionNumber)
        .Select(x => new { x.Id, x.VersionNumber, x.Status }).FirstOrDefaultAsync(ct);
    var requiredBaseline = Enum.GetValues<BaselineAssessmentType>().Length;
    var requiredDocuments = await _db.DocumentTypes.CountAsync(x =>
        x.OwnerType == DocumentOwnerType.Lahan && x.IsActive && x.IsRequired, ct);

    var query = _db.Lahan.Where(x => x.Petani.Poktan.AssociationId == cycle.AssociationId);
    if (request.PoktanId is not null) query = query.Where(x => x.Petani.PoktanId == request.PoktanId);
    if (!string.IsNullOrWhiteSpace(request.Search)) {
      var search = request.Search.Trim().ToLower();
      query = query.Where(x => x.Petani.Nama.ToLower().Contains(search)
          || x.Petani.Poktan.Nama.ToLower().Contains(search)
          || x.NoLegalitas != null && x.NoLegalitas.ToLower().Contains(search));
    }
    if (request.Eligibility == DisclosureEligibilityFilter.Eligible) query = query.Where(x =>
        _db.CertificateLahan.Any(c => c.LahanId == x.Id)
        || x.BoundaryGeoJson != null && x.BaselineAssessments.Where(a => a.Status == ProgressStatus.Completed)
            .Select(a => a.Type).Distinct().Count() == requiredBaseline);
    else if (request.Eligibility == DisclosureEligibilityFilter.Ineligible) query = query.Where(x =>
        !_db.CertificateLahan.Any(c => c.LahanId == x.Id)
        && (x.BoundaryGeoJson == null || x.BaselineAssessments.Where(a => a.Status == ProgressStatus.Completed)
            .Select(a => a.Type).Distinct().Count() < requiredBaseline));

    if (request.SelectionStatus == DisclosureSelectionFilter.Selected) query = query.Where(x =>
        x.CertificationParticipations.Any(p => p.CertificationParticipant.CertificationCycleId == cycleId
            && p.Status == LahanParticipationStatus.Included));
    else if (request.SelectionStatus == DisclosureSelectionFilter.Unselected) query = query.Where(x =>
        !x.CertificationParticipations.Any(p => p.CertificationParticipant.CertificationCycleId == cycleId
            && p.Status == LahanParticipationStatus.Included));
    else if (request.SelectionStatus == DisclosureSelectionFilter.Included) query = query.Where(x =>
        x.CertificationParticipations.Any(p => p.CertificationParticipant.CertificationCycleId == cycleId
            && p.Status == LahanParticipationStatus.Included));
    else if (request.SelectionStatus == DisclosureSelectionFilter.Excluded) query = query.Where(x =>
        x.CertificationParticipations.Any(p => p.CertificationParticipant.CertificationCycleId == cycleId
            && p.Status == LahanParticipationStatus.Excluded));

    var totalCount = await query.CountAsync(ct);
    var readyLahan = await query.CountAsync(x => _db.CertificateLahan.Any(c => c.LahanId == x.Id)
        || x.BoundaryGeoJson != null && x.BaselineAssessments.Where(a => a.Status == ProgressStatus.Completed)
            .Select(a => a.Type).Distinct().Count() == requiredBaseline, ct);
    var selectedLahan = await query.CountAsync(x => x.CertificationParticipations.Any(p =>
        p.CertificationParticipant.CertificationCycleId == cycleId
        && p.Status == LahanParticipationStatus.Included), ct);

    query = (request.SortBy?.ToLowerInvariant(), request.Descending) switch {
      ("petani", false) => query.OrderBy(x => x.Petani.Nama).ThenBy(x => x.NoLegalitas),
      ("petani", true) => query.OrderByDescending(x => x.Petani.Nama).ThenByDescending(x => x.NoLegalitas),
      ("poktan", false) => query.OrderBy(x => x.Petani.Poktan.Nama).ThenBy(x => x.Petani.Nama),
      ("poktan", true) => query.OrderByDescending(x => x.Petani.Poktan.Nama).ThenByDescending(x => x.Petani.Nama),
      ("legalnumber", true) => query.OrderByDescending(x => x.NoLegalitas),
      _ => query.OrderBy(x => x.NoLegalitas)
    };
    if (affectedIds is not null) query = query.Where(x => affectedIds.Contains(x.Id));
    else query = query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize);

    var rows = await query.Select(x => new DisclosureWorkspaceProjection(
        x.Id, x.NoLegalitas, x.PetaniId, x.Petani.Nama, x.Petani.PoktanId, x.Petani.Poktan.Nama,
        x.BoundaryGeoJson != null, _db.CertificateLahan.Any(c => c.LahanId == x.Id),
        x.BaselineAssessments.Count(),
        x.BaselineAssessments.Where(a => a.Status == ProgressStatus.Completed).Select(a => a.Type).Distinct().Count(),
        x.Documents.Where(d => d.DocumentType.OwnerType == DocumentOwnerType.Lahan
            && d.DocumentType.IsActive && d.DocumentType.IsRequired)
            .Select(d => d.DocumentTypeId).Distinct().Count(),
        x.Documents.Where(d => d.Status == DocumentStatus.Verified
            && d.DocumentType.OwnerType == DocumentOwnerType.Lahan
            && d.DocumentType.IsActive && d.DocumentType.IsRequired)
            .Select(d => d.DocumentTypeId).Distinct().Count(),
        x.Petani.CertificationParticipations.Where(p => p.CertificationCycleId == cycleId)
            .Select(p => (Guid?)p.Id).SingleOrDefault(),
        x.CertificationParticipations.Where(p => p.CertificationParticipant.CertificationCycleId == cycleId)
            .Select(p => (Guid?)p.Id).SingleOrDefault(),
        x.Petani.CertificationParticipations.Where(p => p.CertificationCycleId == cycleId)
            .Select(p => (ParticipationStatus?)p.Status).SingleOrDefault(),
        x.CertificationParticipations.Where(p => p.CertificationParticipant.CertificationCycleId == cycleId)
            .Select(p => (LahanParticipationStatus?)p.Status).SingleOrDefault())).ToListAsync(ct);
    var items = rows.Select(x => {
      var baselineStatus = x.IsCertified ? BaselineSummaryStatus.Certified
          : x.AssessmentCount == 0 ? BaselineSummaryStatus.NotAssessed
          : x.BoundaryAvailable && x.BaselineCompleted == requiredBaseline ? BaselineSummaryStatus.Complete
          : BaselineSummaryStatus.Incomplete;
      var blockers = new List<string>();
      if (!x.IsCertified && !x.BoundaryAvailable) blockers.Add("LAHAN_BOUNDARY_MISSING");
      if (!x.IsCertified && x.BaselineCompleted < requiredBaseline) blockers.Add("BASELINE_INCOMPLETE");
      return new DisclosureWorkspaceItem(x.LahanId, x.LegalNumber, x.PetaniId, x.PetaniName,
          x.PoktanId, x.PoktanName, requiredDocuments, x.UploadedDocuments, x.VerifiedDocuments,
          LahanSummaryRules.DocumentsStatus(requiredDocuments, x.UploadedDocuments, x.VerifiedDocuments),
          x.BoundaryAvailable, baselineStatus, x.IsCertified, blockers.Count == 0, blockers,
          x.ParticipantId, x.ParticipantLahanId, x.ParticipantStatus, x.LahanStatus, x.LahanStatus);
    }).ToList();
    var progress = totalCount == 0 ? 0m : decimal.Round(readyLahan * 100m / totalCount, 2);
    var disclosureInfo = new DisclosureWorkspaceDisclosure(disclosure?.Id, disclosure?.VersionNumber ?? 1,
        disclosure?.Status, selectedLahan);
    var summary = new DisclosureWorkspaceSummary(totalCount, readyLahan, totalCount - readyLahan,
        selectedLahan, progress);
    var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)request.PageSize);
    return new DisclosureWorkspaceResponse(disclosureInfo, summary, items,
        request.Page, request.PageSize, totalCount, totalPages);
  }

  private sealed record DisclosureWorkspaceProjection(
      Guid LahanId, string? LegalNumber, Guid PetaniId, string PetaniName, Guid PoktanId, string PoktanName,
      bool BoundaryAvailable, bool IsCertified, int AssessmentCount, int BaselineCompleted,
      int UploadedDocuments, int VerifiedDocuments, Guid? ParticipantId, Guid? ParticipantLahanId,
      ParticipationStatus? ParticipantStatus, LahanParticipationStatus? LahanStatus);

  [HttpGet("disclosures/{id:guid}/candidates")]
  public async Task<IActionResult> DisclosureCandidates(Guid id, CancellationToken ct) {
    var disclosure = await _db.Disclosures.Where(x => x.Id == id)
        .Select(x => new { x.CertificationCycleId, x.CertificationCycle.AssociationId })
        .SingleOrDefaultAsync(ct);
    if (disclosure is null) return NotFound();
    if (!await _access.CanAccessCertificationCycleAsync(User, disclosure.CertificationCycleId, ct)) return Forbid();
    if (User.IsInRole(AppRoles.MemberTaniBaik)) return Forbid();

    var required = Enum.GetValues<BaselineAssessmentType>().Length;
    var rows = await _db.Lahan.Where(x => x.Petani.Poktan.AssociationId == disclosure.AssociationId)
        .OrderBy(x => x.Petani.Nama).ThenBy(x => x.NoLegalitas)
        .Select(x => new {
          LahanId = x.Id, x.NoLegalitas, x.PetaniId, PetaniName = x.Petani.Nama,
          x.Petani.PoktanId, PoktanName = x.Petani.Poktan.Nama,
          BoundaryAvailable = x.BoundaryGeoJson != null,
          IsCertified = _db.CertificateLahan.Any(c => c.LahanId == x.Id),
          BaselineCompleted = x.BaselineAssessments.Count(a => a.Status == ProgressStatus.Completed),
          ParticipantId = x.Petani.CertificationParticipations
              .Where(p => p.CertificationCycleId == disclosure.CertificationCycleId)
              .Select(p => (Guid?)p.Id).SingleOrDefault(),
          ParticipantStatus = x.Petani.CertificationParticipations
              .Where(p => p.CertificationCycleId == disclosure.CertificationCycleId)
              .Select(p => (ParticipationStatus?)p.Status).SingleOrDefault(),
          ParticipantLahanId = x.CertificationParticipations
              .Where(l => l.CertificationParticipant.CertificationCycleId == disclosure.CertificationCycleId)
              .Select(l => (Guid?)l.Id).SingleOrDefault(),
          LahanStatus = x.CertificationParticipations
              .Where(l => l.CertificationParticipant.CertificationCycleId == disclosure.CertificationCycleId)
              .Select(l => (LahanParticipationStatus?)l.Status).SingleOrDefault()
        }).ToListAsync(ct);
    return Ok(rows.Select(x => {
      var blockers = new List<string>();
      if (!x.IsCertified && !x.BoundaryAvailable) blockers.Add("LAHAN_BOUNDARY_MISSING");
      if (!x.IsCertified && x.BaselineCompleted < required) blockers.Add("BASELINE_INCOMPLETE");
      return new {
        x.LahanId, LegalNumber = x.NoLegalitas, x.PetaniId, x.PetaniName, x.PoktanId, x.PoktanName,
        x.IsCertified, x.BoundaryAvailable, x.BaselineCompleted,
        BaselineRequired = x.IsCertified ? 0 : required, Eligible = blockers.Count == 0, Blockers = blockers,
        x.ParticipantId, x.ParticipantStatus, x.ParticipantLahanId, x.LahanStatus
      };
    }));
  }

  [HttpPost("disclosures/{id:guid}/review"), Authorize(Roles = AppRoles.SuperAdmin)]
  public async Task<IActionResult> ReviewDisclosure(Guid id, DisclosureReviewRequest request, CancellationToken ct) {
    var disclosure = await _db.Disclosures.Include(x => x.CertificationCycle).SingleOrDefaultAsync(x => x.Id == id, ct);
    if (disclosure is null) return NotFound();
    CertificationWorkflowService.EnsureCurrent(disclosure.CertificationCycle);
    if (disclosure.CertificationCycle.Status != CertificationCycleStatus.Active
        || disclosure.CertificationCycle.CurrentPhase >= CertificationPhase.InternalAudit)
      return Conflict(new { message = "Disclosure tidak dapat ditinjau setelah audit internal dimulai atau siklus tidak aktif." });
    if (disclosure.Status != DisclosureStatus.PendingSecondDisclosureApproval)
      return Conflict(new { message = "Hanya permintaan disclosure kedua yang dapat ditinjau." });
    disclosure.Status = request.Approve ? DisclosureStatus.ApprovedForSecondDisclosure : DisclosureStatus.Rejected;
    disclosure.ReviewedByUserId = AccessService.UserId(User);
    disclosure.ReviewedAt = DateTimeOffset.UtcNow;
    disclosure.ReviewNotes = request.Notes;
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  [HttpPut("disclosures/{id:guid}/lahan/{participantLahanId:guid}")]
  public async Task<IActionResult> SelectDisclosureLahan(Guid id, Guid participantLahanId,
      DisclosureLahanSelectionRequest request, CancellationToken ct) {
    var disclosure = await _db.Disclosures.Include(x => x.CertificationCycle).SingleOrDefaultAsync(x => x.Id == id, ct);
    if (disclosure is null) return NotFound();
    if (!await _access.CanManageCertificationCycleAsync(User, disclosure.CertificationCycleId, ct)) return Forbid();
    CertificationWorkflowService.EnsureCurrent(disclosure.CertificationCycle);
    if (disclosure.Status is not (DisclosureStatus.Draft or DisclosureStatus.ApprovedForSecondDisclosure))
      return Conflict(new { message = "Scope hanya dapat diubah pada disclosure yang aktif dan telah disetujui bila diperlukan." });
    if (request.Status is not (LahanParticipationStatus.Included or LahanParticipationStatus.Excluded))
      return ValidationProblem("Status scope harus Included atau Excluded.");
    if (request.Status == LahanParticipationStatus.Excluded && string.IsNullOrWhiteSpace(request.Reason))
      return ValidationProblem(new ValidationProblemDetails { Errors = {
        [nameof(request.Reason)] = new[] { "Alasan wajib diisi untuk Lahan yang tidak diikutkan." }
      } });
    var item = await _db.CertificationParticipantLahan.Include(x => x.CertificationParticipant)
        .SingleOrDefaultAsync(x => x.Id == participantLahanId
            && x.CertificationParticipant.CertificationCycleId == disclosure.CertificationCycleId, ct);
    if (item is null) return NotFound();
    await _workflow.EnsureScopeMutationAllowedAsync(disclosure.CertificationCycleId, ct);
    item.Status = request.Status;
    item.StatusReason = request.Status == LahanParticipationStatus.Excluded ? request.Reason!.Trim() : null;
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  [HttpPost("disclosures/{id:guid}/complete")]
  public async Task<IActionResult> CompleteDisclosure(Guid id, CancellationToken ct) {
    var disclosure = await _db.Disclosures.Include(x => x.CertificationCycle).SingleOrDefaultAsync(x => x.Id == id, ct);
    if (disclosure is null) return NotFound();
    if (!await _access.CanManageCertificationCycleAsync(User, disclosure.CertificationCycleId, ct)) return Forbid();
    await _workflow.EnsureScopeMutationAllowedAsync(disclosure.CertificationCycleId, ct);
    if (disclosure.Status is not (DisclosureStatus.Draft or DisclosureStatus.ApprovedForSecondDisclosure))
      return Conflict(new { message = "Disclosure belum dapat diselesaikan." });
    var unresolvedParticipants = await _db.CertificationParticipants.CountAsync(x => x.CertificationCycleId == disclosure.CertificationCycleId
        && (x.Status == ParticipationStatus.Selected || x.Status == ParticipationStatus.Confirmed), ct);
    var activeParticipantIds = await _db.CertificationParticipants.Where(x => x.CertificationCycleId == disclosure.CertificationCycleId
        && x.Status != ParticipationStatus.Excluded && x.Status != ParticipationStatus.Withdrawn).Select(x => x.Id).ToArrayAsync(ct);
    var pendingLahan = await _db.CertificationParticipantLahan.CountAsync(x => activeParticipantIds.Contains(x.CertificationParticipantId)
        && x.Status == LahanParticipationStatus.Pending, ct);
    if (unresolvedParticipants > 0 || pendingLahan > 0) {
      var blockers = new List<CertificationReadinessBlocker>();
      if (unresolvedParticipants > 0) blockers.Add(new("PARTICIPANTS_UNCONFIRMED",
          $"{unresolvedParticipants} Petani belum memiliki keputusan scope.", unresolvedParticipants));
      if (pendingLahan > 0) blockers.Add(new("LAHAN_PENDING",
          $"{pendingLahan} Lahan belum memiliki keputusan Included atau Excluded.", pendingLahan));
      return Conflict(new { message = "Semua Petani dan Lahan harus memiliki keputusan scope sebelum disclosure diselesaikan.", blockers });
    }
    var participants = await _db.CertificationParticipants.Where(x => x.CertificationCycleId == disclosure.CertificationCycleId
        && x.Status == ParticipationStatus.Included).ToListAsync(ct);
    var participantIds = participants.Select(x => x.Id).ToArray();
    var lahan = await _db.CertificationParticipantLahan.Include(x => x.Lahan)
        .Where(x => participantIds.Contains(x.CertificationParticipantId)
            && x.Status == LahanParticipationStatus.Included).ToListAsync(ct);
    var participantsWithoutLahan = participantIds.Count(x => !lahan.Any(l => l.CertificationParticipantId == x));
    if (participants.Count == 0 || lahan.Count == 0 || participantsWithoutLahan > 0)
      return Conflict(new { message = "Disclosure harus memiliki sedikitnya satu Petani dan Lahan, dan setiap Petani yang diikutkan harus memiliki Lahan yang diikutkan." });
    var newLahan = lahan.Where(x => x.EntryPath == LahanEntryPath.New).ToList();
    var newLahanIds = newLahan.Select(x => x.LahanId).ToArray();
    var baselineCounts = await _db.BaselineAssessments.Where(x => newLahanIds.Contains(x.LahanId)
        && x.Status == ProgressStatus.Completed).GroupBy(x => x.LahanId)
        .Select(x => new { Id = x.Key, Count = x.Select(a => a.Type).Distinct().Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
    var requiredBaselineCount = Enum.GetValues<BaselineAssessmentType>().Length;
    var missingBoundary = newLahan.Count(x => string.IsNullOrWhiteSpace(x.Lahan.BoundaryGeoJson));
    var baselineRemaining = newLahanIds.Sum(x => Math.Max(0, requiredBaselineCount - baselineCounts.GetValueOrDefault(x)));
    if (missingBoundary > 0 || baselineRemaining > 0) {
      var blockers = new List<CertificationReadinessBlocker>();
      if (missingBoundary > 0) blockers.Add(new("LAHAN_BOUNDARY_MISSING",
          $"{missingBoundary} Lahan baru belum memiliki batas yang valid.", missingBoundary));
      if (baselineRemaining > 0) blockers.Add(new("BASELINE_INCOMPLETE",
          $"{baselineRemaining} pemeriksaan baseline wajib belum selesai.", baselineRemaining));
      return Conflict(new {
        message = "Disclosure belum dapat diselesaikan karena batas atau baseline Lahan baru belum lengkap.",
        blockers
      });
    }
    disclosure.Participants = participants.Select(x => new DisclosureParticipant { CertificationParticipantId = x.Id }).ToList();
    disclosure.Lahan = lahan.Select(x => new DisclosureLahan { CertificationParticipantLahanId = x.Id }).ToList();
    disclosure.Status = DisclosureStatus.Completed;
    disclosure.SubmittedAt = DateTimeOffset.UtcNow;
    disclosure.CompletedAt = DateTimeOffset.UtcNow;
    var step = await _db.CycleStepProgress.SingleAsync(x => x.CertificationCycleId == disclosure.CertificationCycleId && x.Step == CertificationStep.Disclosure, ct);
    step.Status = ProgressStatus.Completed;
    step.CompletedAt = DateTimeOffset.UtcNow;
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  [HttpGet("cycles/{cycleId:guid}/training/candidates")]
  public async Task<IActionResult> TrainingCandidates(Guid cycleId, [FromQuery] string? search,
      [FromQuery] Guid? sessionId, CancellationToken ct) {
    if (!await _access.CanAccessCertificationCycleAsync(User, cycleId, ct)) return Forbid();
    var query = _db.CertificationParticipants.Where(x => x.CertificationCycleId == cycleId);
    if (sessionId.HasValue) {
      var attendingIds = _db.TrainingAttendance.Where(x => x.TrainingSessionId == sessionId.Value
          && x.TrainingSession.CertificationCycleId == cycleId)
          .Select(x => x.PetaniId);
      query = query.Where(x => !attendingIds.Contains(x.PetaniId));
    }
    if (!string.IsNullOrWhiteSpace(search)) {
      var pattern = $"%{search.Trim()}%";
      query = query.Where(x => EF.Functions.ILike(x.Petani.Nama, pattern)
          || x.Petani.Nik != null && EF.Functions.ILike(x.Petani.Nik, pattern)
          || EF.Functions.ILike(x.Petani.Poktan.Nama, pattern));
    }
    return Ok(await query.OrderBy(x => x.Petani.Nama).Take(20)
        .Select(x => new { x.PetaniId, PetaniName = x.Petani.Nama, x.Petani.Nik,
          PoktanName = x.Petani.Poktan.Nama }).ToListAsync(ct));
  }

  [HttpPost("cycles/{cycleId:guid}/training")]
  public async Task<IActionResult> CreateTraining(Guid cycleId, CreateTrainingRequest request, CancellationToken ct) {
    if (!await _access.CanManageCertificationCycleAsync(User, cycleId, ct)) return Forbid();
    var trainingCycle = await _db.CertificationCycles.Where(x => x.Id == cycleId)
        .Select(x => new { x.IsCurrent, x.Status, x.CurrentPhase }).SingleOrDefaultAsync(ct);
    if (trainingCycle is null) return NotFound();
    EnsureActiveCycle(trainingCycle.IsCurrent, trainingCycle.Status);
    var associationId = await _db.CertificationCycles.Where(x => x.Id == cycleId).Select(x => x.AssociationId).SingleAsync(ct);
    var validIds = await _db.Petani.Where(x => x.Poktan.AssociationId == associationId
        && request.PetaniIds.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct);
    if (validIds.Count != request.PetaniIds.Distinct().Count())
      return ValidationProblem("Salah satu Petani tidak berada dalam Association siklus.");
    var session = new TrainingSession { Id = Guid.NewGuid(), CertificationCycleId = cycleId, Title = request.Title.Trim(), Description = request.Description,
      ScheduledAt = request.ScheduledAt, CreatedByUserId = AccessService.UserId(User)!.Value,
      Attendance = validIds.Select(x => new TrainingAttendance { PetaniId = x }).ToList() };
    _db.TrainingSessions.Add(session);
    await _db.SaveChangesAsync(ct);
    return Created($"/api/certification/training/{session.Id}", new { session.Id });
  }

  [HttpGet("cycles/{cycleId:guid}/training")]
  public async Task<IActionResult> Training(Guid cycleId, CancellationToken ct) {
    if (!await _access.CanAccessCertificationCycleAsync(User, cycleId, ct)) return Forbid();
    var userId = AccessService.UserId(User);
    var isMember = User.IsInRole(AppRoles.MemberTaniBaik);
    return Ok(await _db.TrainingSessions.Where(x => x.CertificationCycleId == cycleId).OrderBy(x => x.ScheduledAt)
        .Select(x => new { x.Id, x.Title, x.Description, x.ScheduledAt, x.CompletedAt,
          Attendance = x.Attendance.Where(a => !isMember || a.Petani.ApplicationUserId == userId)
            .Select(a => new { a.PetaniId, PetaniName = a.Petani.Nama, a.Status, a.Notes }) }).ToListAsync(ct));
  }

  [HttpPost("training/{sessionId:guid}/participants")]
  public async Task<IActionResult> AddTrainingParticipants(Guid sessionId, AddTrainingParticipantsRequest request, CancellationToken ct) {
    var session = await _db.TrainingSessions.Include(x => x.CertificationCycle).Include(x => x.Attendance)
        .SingleOrDefaultAsync(x => x.Id == sessionId, ct);
    if (session is null) return NotFound();
    if (!await _access.CanManageCertificationCycleAsync(User, session.CertificationCycleId, ct)) return Forbid();
    EnsureActiveCycle(session.CertificationCycle.IsCurrent, session.CertificationCycle.Status);
    if (session.CompletedAt is not null)
      return Conflict(new { message = "Petani tidak dapat ditambahkan setelah sesi training diselesaikan." });

    var requestedIds = request.PetaniIds.Distinct().ToArray();
    if (requestedIds.Length == 0) return ValidationProblem("Pilih minimal satu Petani.");
    var validIds = await _db.Petani.Where(x => x.Poktan.AssociationId == session.CertificationCycle.AssociationId
        && requestedIds.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct);
    if (validIds.Count != requestedIds.Length)
      return ValidationProblem("Salah satu Petani tidak berada dalam Association siklus.");

    var existingIds = session.Attendance.Select(x => x.PetaniId).ToHashSet();
    foreach (var petaniId in validIds.Where(x => !existingIds.Contains(x)))
      session.Attendance.Add(new TrainingAttendance { PetaniId = petaniId });
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  [HttpPut("training/{sessionId:guid}/attendance/{petaniId:guid}")]
  public async Task<IActionResult> Attendance(Guid sessionId, Guid petaniId, AttendanceRequest request, CancellationToken ct) {
    if (!await _access.CanManagePetaniAsync(User, petaniId, ct)) return Forbid();
    var attendance = await _db.TrainingAttendance.Include(x => x.TrainingSession).ThenInclude(x => x.CertificationCycle)
        .SingleOrDefaultAsync(x => x.TrainingSessionId == sessionId && x.PetaniId == petaniId, ct);
    if (attendance is null) return NotFound();
    EnsureActiveCycle(attendance.TrainingSession.CertificationCycle.IsCurrent, attendance.TrainingSession.CertificationCycle.Status);
    if (attendance.TrainingSession.CompletedAt is not null)
      return Conflict(new { message = "Kehadiran tidak dapat diubah setelah sesi training diselesaikan." });
    attendance.Status = request.Status;
    attendance.Notes = request.Notes;
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  [HttpPost("training/{sessionId:guid}/complete")]
  public async Task<IActionResult> CompleteTraining(Guid sessionId, CancellationToken ct) {
    var session = await _db.TrainingSessions.Include(x => x.CertificationCycle).Include(x => x.Attendance)
        .SingleOrDefaultAsync(x => x.Id == sessionId, ct);
    if (session is null) return NotFound();
    if (!await _access.CanManageCertificationCycleAsync(User, session.CertificationCycleId, ct)) return Forbid();
    EnsureActiveCycle(session.CertificationCycle.IsCurrent, session.CertificationCycle.Status);
    if (session.CompletedAt is not null) return Conflict(new { message = "Sesi training sudah diselesaikan." });
    if (session.Attendance.Any(x => x.Status == AttendanceStatus.Invited))
      return Conflict(new { message = "Semua status kehadiran harus difinalisasi sebelum sesi training diselesaikan." });
    session.CompletedAt = DateTimeOffset.UtcNow;
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  [HttpPost("training/{sessionId:guid}/reopen")]
  public async Task<IActionResult> ReopenTraining(Guid sessionId, ReopenTrainingRequest request, CancellationToken ct) {
    var session = await _db.TrainingSessions.Include(x => x.CertificationCycle).SingleOrDefaultAsync(x => x.Id == sessionId, ct);
    if (session is null) return NotFound();
    if (!await _access.CanManageCertificationCycleAsync(User, session.CertificationCycleId, ct)) return Forbid();
    EnsureActiveCycle(session.CertificationCycle.IsCurrent, session.CertificationCycle.Status);
    if (session.CompletedAt is null) return Conflict(new { message = "Sesi training belum diselesaikan." });
    session.CompletedAt = null;
    _db.WorkflowTransitions.Add(new WorkflowTransition {
      Id = Guid.NewGuid(), CertificationCycleId = session.CertificationCycleId,
      FromPhase = session.CertificationCycle.CurrentPhase, ToPhase = session.CertificationCycle.CurrentPhase,
      UserId = AccessService.UserId(User)!.Value, Notes = $"Training '{session.Title}' dibuka kembali: {request.Reason.Trim()}"
    });
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  [HttpPost("monitoring")]
  public async Task<IActionResult> CreateMonitoring(MonitoringRequest request, CancellationToken ct) {
    if (!await _access.CanManagePetaniAsync(User, request.PetaniId, ct)) return Forbid();
    if (request.Status == ProgressStatus.NotApplicable) return ValidationProblem("Status NotApplicable tidak valid untuk catatan monitoring.");
    if (request.MonitoringMonth.Day != 1) return ValidationProblem("MonitoringMonth harus menggunakan tanggal pertama pada bulan tersebut.");
    if (request.LahanId is not null && !await _db.Lahan.AnyAsync(x => x.Id == request.LahanId && x.PetaniId == request.PetaniId, ct))
      return ValidationProblem("Lahan tidak dimiliki oleh Petani yang dipilih.");
    if (await MonitoringDuplicateAsync(request.PetaniId, request.LahanId, request.Category, request.MonitoringMonth, null, ct))
      return Conflict(new { message = "Catatan monitoring untuk peserta, Lahan, kategori, dan bulan tersebut sudah ada." });
    var record = new MonitoringRecord { Id = Guid.NewGuid(), PetaniId = request.PetaniId,
      LahanId = request.LahanId, Category = request.Category, MonitoringMonth = request.MonitoringMonth,
      Status = request.Status, Notes = request.Notes, ResponsibleUserId = AccessService.UserId(User)!.Value };
    _db.MonitoringRecords.Add(record);
    await _db.SaveChangesAsync(ct);
    return Created($"/api/certification/monitoring/{record.Id}", new { record.Id });
  }

  [HttpPut("monitoring/{id:guid}")]
  public async Task<IActionResult> UpdateMonitoring(Guid id, UpdateMonitoringRequest request, CancellationToken ct) {
    var record = await _db.MonitoringRecords.SingleOrDefaultAsync(x => x.Id == id, ct);
    if (record is null) return NotFound();
    if (!await _access.CanManagePetaniAsync(User, record.PetaniId, ct)) return Forbid();
    if (request.MonitoringMonth.Day != 1) return ValidationProblem("MonitoringMonth harus menggunakan tanggal pertama pada bulan tersebut.");
    if (request.Status == ProgressStatus.NotApplicable) return ValidationProblem("Status NotApplicable tidak valid untuk catatan monitoring.");
    if (request.LahanId is not null && !await _db.Lahan.AnyAsync(x => x.Id == request.LahanId && x.PetaniId == record.PetaniId, ct))
      return ValidationProblem("Lahan tidak dimiliki oleh Petani yang dipilih.");
    if (await MonitoringDuplicateAsync(record.PetaniId, request.LahanId, request.Category, request.MonitoringMonth, id, ct))
      return Conflict(new { message = "Catatan monitoring untuk peserta, Lahan, kategori, dan bulan tersebut sudah ada." });
    record.LahanId = request.LahanId;
    record.Category = request.Category;
    record.MonitoringMonth = request.MonitoringMonth;
    record.Status = request.Status;
    record.Notes = request.Notes;
    record.ResponsibleUserId = AccessService.UserId(User)!.Value;
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  [HttpDelete("monitoring/{id:guid}")]
  public async Task<IActionResult> DeleteMonitoring(Guid id, CancellationToken ct) {
    var record = await _db.MonitoringRecords.SingleOrDefaultAsync(x => x.Id == id, ct);
    if (record is null) return NotFound();
    if (!await _access.CanManagePetaniAsync(User, record.PetaniId, ct)) return Forbid();
    _db.MonitoringRecords.Remove(record);
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  [HttpGet("cycles/{cycleId:guid}/monitoring")]
  public async Task<IActionResult> Monitoring(Guid cycleId, CancellationToken ct) {
    if (!await _access.CanAccessCertificationCycleAsync(User, cycleId, ct)) return Forbid();
    var associationId = await _db.CertificationCycles.Where(x => x.Id == cycleId).Select(x => (Guid?)x.AssociationId).SingleOrDefaultAsync(ct);
    if (associationId is null) return NotFound();
    var query = _db.MonitoringRecords.Where(x => x.Petani.Poktan.AssociationId == associationId);
    if (User.IsInRole(AppRoles.MemberTaniBaik)) {
      var userId = AccessService.UserId(User);
      query = query.Where(x => x.Petani.ApplicationUserId == userId);
    }
    return Ok(await query.OrderByDescending(x => x.MonitoringMonth)
        .Select(x => new { x.Id, x.PetaniId, x.LahanId, x.Category, x.MonitoringMonth, x.Status, x.Notes }).ToListAsync(ct));
  }

  [HttpGet("monitoring")]
  public async Task<IActionResult> Monitoring([FromQuery] MonitoringQuery request, CancellationToken ct) {
    var userId = AccessService.UserId(User);
    var isMember = User.IsInRole(AppRoles.MemberTaniBaik);
    if (!await _access.CanAccessAssociationAsync(User, request.AssociationId, ct)
        && !(isMember && await _db.Petani.AnyAsync(x => x.ApplicationUserId == userId
            && x.Poktan.AssociationId == request.AssociationId, ct))) return Forbid();
    var query = _db.MonitoringRecords.Where(x => x.Petani.Poktan.AssociationId == request.AssociationId);
    if (request.PoktanId is not null) query = query.Where(x => x.Petani.PoktanId == request.PoktanId);
    if (request.PetaniId is not null) query = query.Where(x => x.PetaniId == request.PetaniId);
    if (request.Year is not null) query = query.Where(x => x.MonitoringMonth.Year == request.Year);
    if (request.Month is not null) query = query.Where(x => x.MonitoringMonth.Month == request.Month);
    if (isMember) {
      query = query.Where(x => x.Petani.ApplicationUserId == userId);
    }
    return Ok(await query.OrderByDescending(x => x.MonitoringMonth).ThenBy(x => x.Petani.Nama)
        .Select(x => new { x.Id, x.PetaniId, PetaniName = x.Petani.Nama,
          PoktanId = x.Petani.PoktanId, PoktanName = x.Petani.Poktan.Nama, x.LahanId,
          LegalNumber = x.Lahan == null ? null : x.Lahan.NoLegalitas,
          x.Category, x.MonitoringMonth, x.Status, x.Notes, x.ResponsibleUserId, x.CreatedAt }).ToListAsync(ct));
  }

  private Task<bool> MonitoringDuplicateAsync(Guid petaniId, Guid? lahanId, MonitoringCategory category,
      DateOnly month, Guid? exceptId, CancellationToken ct) => _db.MonitoringRecords.AnyAsync(x => x.Id != exceptId
          && x.PetaniId == petaniId && x.LahanId == lahanId
          && x.Category == category && x.MonitoringMonth == month, ct);

  private static void EnsureActiveCycle(bool isCurrent, CertificationCycleStatus status) {
    if (!isCurrent) throw new CertificationWorkflowException("Siklus historis hanya dapat dilihat.", "CERTIFICATION_CYCLE_READ_ONLY");
    if (status != CertificationCycleStatus.Active) throw new CertificationWorkflowException("Siklus tidak aktif.");
  }

  [HttpPost("cycles/{cycleId:guid}/audits")]
  public async Task<IActionResult> CreateAudit(Guid cycleId, CreateAuditRequest request, CancellationToken ct) {
    if (!await _access.CanManageCertificationCycleAsync(User, cycleId, ct)) return Forbid();
    var cycle = await _db.CertificationCycles.Include(x => x.Audits).ThenInclude(x => x.Findings).SingleOrDefaultAsync(x => x.Id == cycleId, ct);
    if (cycle is null) return NotFound();
    var requiredPhase = request.Type == AuditType.Internal ? CertificationPhase.InternalAudit : CertificationPhase.ExternalAudit;
    if (cycle.CurrentPhase != requiredPhase) return Conflict(new { message = $"Siklus belum berada pada tahap {requiredPhase}." });
    if (request.Type == AuditType.External && cycle.Type == CertificationCycleType.Surveillance
        && (cycle.TargetAuditStartDate is not null && request.ScheduledDate < cycle.TargetAuditStartDate
            || cycle.TargetAuditEndDate is not null && request.ScheduledDate > cycle.TargetAuditEndDate))
      return ValidationProblem("Jadwal audit surveillance harus berada 8-12 bulan setelah tanggal penerbitan sertifikat sebelumnya.");
    if (cycle.Audits.Any(x => x.Type == request.Type)) return Conflict(new { message = "Jenis audit ini sudah terdaftar." });
    if (request.Type == AuditType.External) {
      var internalAudit = cycle.Audits.SingleOrDefault(x => x.Type == AuditType.Internal);
      if (internalAudit?.Status != AuditStatus.Closed || internalAudit.Findings.Any(x => x.Status != FindingStatus.Closed))
        return Conflict(new { message = "Audit internal dan seluruh temuan harus ditutup terlebih dahulu." });
    }
    var audit = new CertificationAudit { Id = Guid.NewGuid(), CertificationCycleId = cycleId, Type = request.Type,
      ScheduledDate = request.ScheduledDate, CreatedByUserId = AccessService.UserId(User)!.Value };
    _db.CertificationAudits.Add(audit);
    await _db.SaveChangesAsync(ct);
    return Created($"/api/certification/audits/{audit.Id}", new { audit.Id });
  }

  [HttpGet("cycles/{cycleId:guid}/audits")]
  public async Task<IActionResult> Audits(Guid cycleId, CancellationToken ct) {
    var canAccessCycle = await _access.CanAccessCertificationCycleAsync(User, cycleId, ct);
    if (!canAccessCycle) {
      var cycleAssociationId = await _db.CertificationCycles.Where(x => x.Id == cycleId).Select(x => (Guid?)x.AssociationId).SingleOrDefaultAsync(ct);
      if (cycleAssociationId is null || !await _access.IsIcsAuditorForAnyPoktanInAssociationAsync(User, cycleAssociationId.Value, ct)) return Forbid();
    }
    if (User.IsInRole(AppRoles.MemberTaniBaik)) return Forbid();

    List<Guid>? icsPoktanIds = null;
    if (!canAccessCycle) {
      var userId = AccessService.UserId(User);
      icsPoktanIds = await _db.IcsAuditorAssignments.Where(x => x.UserId == userId).Select(x => x.PoktanId).ToListAsync(ct);
    }

    return Ok(await _db.CertificationAudits.Where(x => x.CertificationCycleId == cycleId).Select(x => new {
      x.Id, x.Type, x.Status, x.ScheduledDate, x.PerformedDate, x.FindingsDueDate, x.HasFindings, x.ClosedAt, x.ResultNotes,
      ReportDocumentId = _db.Documents.Where(d => d.CertificationCycleId == x.CertificationCycleId
          && d.DocumentType.Code == ExternalAuditReportDocumentTypeCode).Select(d => (Guid?)d.Id).SingleOrDefault(),
      ClosureReportDocumentId = _db.Documents.Where(d => d.CertificationCycleId == x.CertificationCycleId
          && d.DocumentType.Code == ExternalAuditClosureReportDocumentTypeCode).Select(d => (Guid?)d.Id).SingleOrDefault(),
      Findings = x.Findings.Where(f => icsPoktanIds == null || (f.PoktanId != null && icsPoktanIds.Contains(f.PoktanId.Value)))
          .Select(f => new { f.Id, f.PoktanId, f.Severity, f.Code, f.Description, f.PenyebabAnalisis, f.Corrections,
              f.Status, f.DueDate, f.CorrectiveAction, f.ClosureEvidence, f.ClosedAt })
    }).ToListAsync(ct));
  }

  [HttpPost("audits/{auditId:guid}/perform")]
  public async Task<IActionResult> PerformAudit(Guid auditId, PerformAuditRequest request, CancellationToken ct) {
    var audit = await _db.CertificationAudits.SingleOrDefaultAsync(x => x.Id == auditId, ct);
    if (audit is null) return NotFound();
    if (!await _access.CanManageCertificationCycleAsync(User, audit.CertificationCycleId, ct)) return Forbid();
    if (audit.Type == AuditType.External) return Conflict(new { message = "Audit eksternal dilaksanakan melalui unggah laporan audit." });
    if (audit.Status != AuditStatus.Scheduled) return Conflict(new { message = "Audit tidak berstatus Scheduled." });
    audit.PerformedDate = request.PerformedDate;
    audit.Status = request.Passed ? AuditStatus.Passed : AuditStatus.Failed;
    audit.ResultNotes = request.Notes;
    audit.FindingsDueDate = audit.Type == AuditType.External ? request.PerformedDate.AddMonths(3) : null;
    var stepType = audit.Type == AuditType.Internal ? CertificationStep.InternalAudit : CertificationStep.ExternalAudit;
    var step = await _db.CycleStepProgress.SingleAsync(x => x.CertificationCycleId == audit.CertificationCycleId && x.Step == stepType, ct);
    step.Status = request.Passed ? ProgressStatus.Completed : ProgressStatus.InProgress;
    step.StartedAt ??= DateTimeOffset.UtcNow;
    if (request.Passed) step.CompletedAt = DateTimeOffset.UtcNow;
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  [HttpPost("audits/{auditId:guid}/findings")]
  public async Task<IActionResult> AddFinding(Guid auditId, CreateFindingRequest request, CancellationToken ct) {
    var audit = await _db.CertificationAudits.SingleOrDefaultAsync(x => x.Id == auditId, ct);
    if (audit is null) return NotFound();

    var canManageCycle = await _access.CanManageCertificationCycleAsync(User, audit.CertificationCycleId, ct);
    if (!canManageCycle) {
      if (audit.Type != AuditType.Internal || request.PoktanId is null
          || !await _access.CanManagePoktanAuditAsync(User, request.PoktanId.Value, ct)) {
        return Forbid();
      }
    }

    if (audit.Type == AuditType.Internal) {
      if (request.PoktanId is null) return ValidationProblem("PoktanId wajib diisi untuk temuan audit internal.");
      var cycleAssociationId = await _db.CertificationCycles.Where(x => x.Id == audit.CertificationCycleId)
          .Select(x => x.AssociationId).SingleAsync(ct);
      if (!await _db.Poktan.AnyAsync(x => x.Id == request.PoktanId && x.AssociationId == cycleAssociationId, ct))
        return ValidationProblem("PoktanId tidak valid untuk Association siklus ini.");
    }

    if (audit.PerformedDate is null) return Conflict(new { message = "Audit harus dilaksanakan sebelum temuan dicatat." });
    var dueDate = request.DueDate ?? (audit.Type == AuditType.External
        ? audit.PerformedDate.Value.AddMonths(3) : audit.ScheduledDate);
    if (audit.Type == AuditType.External && dueDate > audit.PerformedDate.Value.AddMonths(3))
      return ValidationProblem("Batas temuan audit eksternal tidak boleh lebih dari tiga bulan setelah audit.");
    var finding = new AuditFinding { Id = Guid.NewGuid(), CertificationAuditId = auditId, Code = request.Code.Trim(),
      Description = request.Description.Trim(), PoktanId = request.PoktanId, Severity = request.Severity, DueDate = dueDate };
    audit.Status = AuditStatus.FindingsOpen;
    _db.AuditFindings.Add(finding);
    await _db.SaveChangesAsync(ct);
    return Created($"/api/certification/findings/{finding.Id}", new { finding.Id });
  }

  [HttpGet("audits/{auditId:guid}/findings/template")]
  public async Task<IActionResult> DownloadFindingTemplate(Guid auditId, CancellationToken ct) {
    var audit = await _db.CertificationAudits.Where(x => x.Id == auditId)
        .Select(x => new { x.Type, x.CertificationCycleId, x.CertificationCycle.AssociationId }).SingleOrDefaultAsync(ct);
    if (audit is null) return NotFound();
    if (audit.Type != AuditType.Internal) return Conflict(new { message = "Bulk temuan hanya tersedia untuk audit internal." });
    var canManageCycle = await _access.CanManageCertificationCycleAsync(User, audit.CertificationCycleId, ct);
    var userId = AccessService.UserId(User);
    var poktan = _db.Poktan.Where(x => x.AssociationId == audit.AssociationId);
    if (!canManageCycle) {
      var assigned = _db.IcsAuditorAssignments.Where(x => x.UserId == userId).Select(x => x.PoktanId)
          .Concat(_db.PoktanAdminAssignments.Where(x => x.UserId == userId).Select(x => x.PoktanId));
      poktan = poktan.Where(x => assigned.Contains(x.Id));
    }
    var rows = await poktan.OrderBy(x => x.Nama).Select(x => new { x.Id, x.Nama }).ToListAsync(ct);
    if (rows.Count == 0) return Forbid();
    return File(_findingExcel.CreateTemplate(rows.Select(x => (x.Id, x.Nama))),
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "template-temuan-audit-internal.xlsx");
  }

  [HttpPost("audits/{auditId:guid}/findings/import/preview"), RequestSizeLimit(AuditFindingExcelService.MaximumFileSize + 1024 * 1024)]
  public async Task<IActionResult> PreviewFindingImport(Guid auditId, [FromForm] IFormFile file, CancellationToken ct) {
    var parsed = await _findingExcel.ParseAsync(file, ct);
    var preview = await ValidateBulkFindingRowsAsync(auditId, parsed.Select(x => (x.RowNumber, x.Data, x.Errors)).ToList(), ct);
    return Ok(preview);
  }

  [HttpPost("audits/{auditId:guid}/findings/import")]
  public async Task<IActionResult> ImportFindings(Guid auditId, BulkImportAuditFindingsRequest request, CancellationToken ct) {
    var rows = request.Rows.Select((x, i) => (i + 2, (BulkAuditFindingRow?)x, new List<string>())).ToList();
    var preview = await ValidateBulkFindingRowsAsync(auditId, rows, ct);
    if (!preview.IsValid) return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> {
      ["rows"] = preview.Rows.Where(x => x.Errors.Count > 0).SelectMany(x => x.Errors.Select(e => $"Baris {x.RowNumber}: {e}")).ToArray()
    }));
    var audit = await _db.CertificationAudits.SingleAsync(x => x.Id == auditId, ct);
    foreach (var row in request.Rows) _db.AuditFindings.Add(new AuditFinding { Id = Guid.NewGuid(), CertificationAuditId = auditId,
      Code = row.Code.Trim(), PoktanId = row.PoktanId, Severity = row.Severity, Description = row.Description.Trim(),
      DueDate = row.DueDate ?? audit.ScheduledDate, PenyebabAnalisis = row.PenyebabAnalisis, Corrections = row.Corrections,
      CorrectiveAction = row.CorrectiveAction });
    audit.Status = AuditStatus.FindingsOpen; await _db.SaveChangesAsync(ct);
    return Ok(new { imported = request.Rows.Count });
  }

  [HttpPut("findings/{findingId:guid}")]
  public async Task<IActionResult> UpdateFinding(Guid findingId, UpdateFindingRequest request, CancellationToken ct) {
    var finding = await _db.AuditFindings.Include(x => x.CertificationAudit).SingleOrDefaultAsync(x => x.Id == findingId, ct);
    if (finding is null) return NotFound();
    if (finding.Status == FindingStatus.Closed) return Conflict(new { message = "Temuan yang sudah ditutup tidak dapat diubah." });

    var canManageCycle = await _access.CanManageCertificationCycleAsync(User, finding.CertificationAudit.CertificationCycleId, ct);
    if (!canManageCycle
        && (finding.PoktanId is null || !await _access.CanManagePoktanAuditAsync(User, finding.PoktanId.Value, ct))) {
      return Forbid();
    }

    finding.PenyebabAnalisis = request.PenyebabAnalisis;
    finding.Corrections = request.Corrections;
    finding.CorrectiveAction = request.CorrectiveAction;
    if (request.SubmitForReview) {
      finding.Status = FindingStatus.CorrectiveActionSubmitted;
    }

    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  [HttpPost("findings/{findingId:guid}/close")]
  public async Task<IActionResult> CloseFinding(Guid findingId, CloseFindingRequest request, CancellationToken ct) {
    var finding = await _db.AuditFindings.Include(x => x.CertificationAudit).ThenInclude(x => x.Findings).SingleOrDefaultAsync(x => x.Id == findingId, ct);
    if (finding is null) return NotFound();
    var canManageCycle = await _access.CanManageCertificationCycleAsync(User, finding.CertificationAudit.CertificationCycleId, ct);
    var canCloseForPoktan = finding.CertificationAudit.Type == AuditType.Internal && finding.PoktanId is not null
        && await _access.CanManagePoktanAsync(User, finding.PoktanId.Value, ct);
    if (!canManageCycle && !canCloseForPoktan) return Forbid();
    finding.CorrectiveAction = request.CorrectiveAction.Trim(); finding.ClosureEvidence = request.ClosureEvidence;
    finding.Status = FindingStatus.Closed; finding.ClosedByUserId = AccessService.UserId(User); finding.ClosedAt = DateTimeOffset.UtcNow;
    if (finding.CertificationAudit.Findings.All(x => x.Id == findingId || x.Status == FindingStatus.Closed)) {
      finding.CertificationAudit.Status = AuditStatus.Closed;
      finding.CertificationAudit.ClosedAt = DateTimeOffset.UtcNow;
      var stepType = finding.CertificationAudit.Type == AuditType.Internal ? CertificationStep.InternalAudit : CertificationStep.ExternalAudit;
      var step = await _db.CycleStepProgress.SingleAsync(x => x.CertificationCycleId == finding.CertificationAudit.CertificationCycleId && x.Step == stepType, ct);
      step.Status = ProgressStatus.Completed; step.CompletedAt = DateTimeOffset.UtcNow;
    }
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  [HttpPost("audits/{auditId:guid}/close")]
  public async Task<IActionResult> CloseAudit(Guid auditId, CancellationToken ct) {
    var audit = await _db.CertificationAudits.Include(x => x.Findings).SingleOrDefaultAsync(x => x.Id == auditId, ct);
    if (audit is null) return NotFound();
    if (!await _access.CanManageCertificationCycleAsync(User, audit.CertificationCycleId, ct)) return Forbid();
    if (audit.Type == AuditType.External) return Conflict(new { message = "Audit eksternal ditutup otomatis melalui unggah laporan audit atau laporan penutupan temuan." });
    if (audit.Status is not (AuditStatus.Passed or AuditStatus.FindingsOpen or AuditStatus.Failed)
        || audit.Findings.Any(x => x.Status != FindingStatus.Closed))
      return Conflict(new { message = "Audit harus sudah dilaksanakan dan seluruh temuannya ditutup." });
    if (audit.Status == AuditStatus.Failed && audit.Findings.Count == 0)
      return Conflict(new { message = "Audit yang gagal harus memiliki temuan dan tindakan perbaikan sebelum ditutup." });
    audit.Status = AuditStatus.Closed;
    audit.ClosedAt = DateTimeOffset.UtcNow;
    var stepType = audit.Type == AuditType.Internal ? CertificationStep.InternalAudit : CertificationStep.ExternalAudit;
    var step = await _db.CycleStepProgress.SingleAsync(x => x.CertificationCycleId == audit.CertificationCycleId && x.Step == stepType, ct);
    step.Status = ProgressStatus.Completed; step.CompletedAt = DateTimeOffset.UtcNow;
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  [HttpPost("audits/{auditId:guid}/report"), RequestSizeLimit(10 * 1024 * 1024 + 1024 * 1024)]
  public async Task<IActionResult> UploadExternalAuditReport(Guid auditId, [FromForm] ExternalAuditReportUploadRequest request, CancellationToken ct) {
    var upload = new UploadDocumentRequest { DocumentTypeId = request.DocumentTypeId, Catatan = request.Catatan, File = request.File };
    var result = await UploadExternalAuditDocumentAsync(auditId, ExternalAuditReportDocumentTypeCode, requireReportAlreadyUploaded: false, upload, ct);
    if (result is not CreatedAtActionResult && result is not CreatedResult) return result;
    var audit = await _db.CertificationAudits.SingleAsync(x => x.Id == auditId, ct);
    audit.PerformedDate ??= DateOnly.FromDateTime(DateTime.UtcNow);
    audit.HasFindings = request.HasFindings!.Value;
    audit.Status = request.HasFindings.Value ? AuditStatus.FindingsOpen : AuditStatus.Closed;
    audit.FindingsDueDate = request.HasFindings.Value ? audit.PerformedDate.Value.AddMonths(3) : null;
    audit.ClosedAt = request.HasFindings.Value ? null : DateTimeOffset.UtcNow;
    await SetAuditStepAsync(audit, request.HasFindings.Value ? ProgressStatus.InProgress : ProgressStatus.Completed, ct);
    await _db.SaveChangesAsync(ct); return result;
  }

  [HttpPost("audits/{auditId:guid}/closure-report"), RequestSizeLimit(10 * 1024 * 1024 + 1024 * 1024)]
  public async Task<IActionResult> UploadExternalAuditClosureReport(Guid auditId, [FromForm] UploadDocumentRequest request, CancellationToken ct) {
    var beforeUpload = await _db.CertificationAudits.Where(x => x.Id == auditId).Select(x => new { x.HasFindings }).SingleOrDefaultAsync(ct);
    if (beforeUpload is null) return NotFound();
    if (beforeUpload.HasFindings != true) return Conflict(new { message = "Laporan penutupan hanya diperlukan bila laporan audit menyatakan ada temuan." });
    var result = await UploadExternalAuditDocumentAsync(auditId, ExternalAuditClosureReportDocumentTypeCode, requireReportAlreadyUploaded: true, request, ct);
    if (result is not CreatedAtActionResult && result is not CreatedResult) return result;
    var audit = await _db.CertificationAudits.SingleAsync(x => x.Id == auditId, ct);
    audit.Status = AuditStatus.Closed; audit.ClosedAt = DateTimeOffset.UtcNow;
    await SetAuditStepAsync(audit, ProgressStatus.Completed, ct); await _db.SaveChangesAsync(ct); return result;
  }

  private async Task<AuditFindingImportPreview> ValidateBulkFindingRowsAsync(Guid auditId,
      List<(int RowNumber, BulkAuditFindingRow? Data, List<string> Errors)> rows, CancellationToken ct) {
    var audit = await _db.CertificationAudits.Where(x => x.Id == auditId)
        .Select(x => new { x.Type, x.PerformedDate, x.CertificationCycleId, x.CertificationCycle.AssociationId }).SingleOrDefaultAsync(ct)
        ?? throw new KeyNotFoundException("Audit tidak ditemukan.");
    if (audit.Type != AuditType.Internal) throw new CertificationWorkflowException("Bulk temuan hanya tersedia untuk audit internal.");
    if (audit.PerformedDate is null) throw new CertificationWorkflowException("Audit harus dilaksanakan sebelum temuan diimpor.");
    var canManageCycle = await _access.CanManageCertificationCycleAsync(User, audit.CertificationCycleId, ct);
    var existingCodes = await _db.AuditFindings.Where(x => x.CertificationAuditId == auditId).Select(x => x.Code).ToListAsync(ct);
    var repeated = rows.Where(x => x.Data is not null).GroupBy(x => x.Data!.Code.Trim(), StringComparer.OrdinalIgnoreCase)
        .Where(x => x.Count() > 1).Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
    foreach (var row in rows.Where(x => x.Data is not null)) {
      var data = row.Data!;
      if (existingCodes.Contains(data.Code.Trim(), StringComparer.OrdinalIgnoreCase)) row.Errors.Add("Code sudah ada pada audit ini.");
      if (repeated.Contains(data.Code.Trim())) row.Errors.Add("Code duplikat di dalam file.");
      if (!await _db.Poktan.AnyAsync(x => x.Id == data.PoktanId && x.AssociationId == audit.AssociationId, ct))
        row.Errors.Add("Poktan tidak berada dalam Association siklus.");
      else if (!canManageCycle && !await _access.CanManagePoktanAuditAsync(User, data.PoktanId, ct))
        row.Errors.Add("Tidak memiliki akses ke Poktan.");
    }
    var responseRows = rows.Select(x => new AuditFindingImportPreviewRow(x.RowNumber, x.Data, x.Errors)).ToList();
    return new AuditFindingImportPreview(responseRows.All(x => x.Errors.Count == 0), responseRows.Count,
        responseRows.Count(x => x.Errors.Count == 0), responseRows);
  }

  private async Task SetAuditStepAsync(CertificationAudit audit, ProgressStatus status, CancellationToken ct) {
    var step = await _db.CycleStepProgress.SingleAsync(x => x.CertificationCycleId == audit.CertificationCycleId
        && x.Step == CertificationStep.ExternalAudit, ct);
    step.Status = status; step.StartedAt ??= DateTimeOffset.UtcNow;
    step.CompletedAt = status == ProgressStatus.Completed ? DateTimeOffset.UtcNow : null;
  }

  private const string ExternalAuditReportDocumentTypeCode = "LAPORAN_AUDIT_EKSTERNAL";
  private const string ExternalAuditClosureReportDocumentTypeCode = "LAPORAN_PENUTUPAN_TEMUAN_EKSTERNAL";

  private async Task<IActionResult> UploadExternalAuditDocumentAsync(Guid auditId, string expectedDocumentTypeCode,
      bool requireReportAlreadyUploaded, UploadDocumentRequest request, CancellationToken ct) {
    var audit = await _db.CertificationAudits.SingleOrDefaultAsync(x => x.Id == auditId, ct);
    if (audit is null) return NotFound();
    if (!await _access.CanManageCertificationCycleAsync(User, audit.CertificationCycleId, ct)) return Forbid();
    if (audit.Type != AuditType.External) return Conflict(new { message = "Hanya audit eksternal yang dapat mengunggah laporan ini." });

    var cycle = await _db.CertificationCycles.SingleAsync(x => x.Id == audit.CertificationCycleId, ct);
    if (cycle.CurrentPhase != CertificationPhase.ExternalAudit)
      return Conflict(new { message = "Siklus belum berada pada tahap audit eksternal." });

    var expectedTypeId = await _db.DocumentTypes.Where(x => x.Code == expectedDocumentTypeCode).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
    if (expectedTypeId is null || request.DocumentTypeId != expectedTypeId)
      return ValidationProblem("DocumentTypeId tidak sesuai dengan jenis laporan yang diharapkan.");

    if (requireReportAlreadyUploaded) {
      var reportTypeId = await _db.DocumentTypes.Where(x => x.Code == ExternalAuditReportDocumentTypeCode).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
      if (reportTypeId is null || !await _db.Documents.AnyAsync(x => x.CertificationCycleId == audit.CertificationCycleId && x.DocumentTypeId == reportTypeId, ct))
        return Conflict(new { message = "Laporan audit eksternal harus diunggah terlebih dahulu." });
    }

    var document = await _uploads.CreateAsync(DocumentOwnerType.CertificationCycle, audit.CertificationCycleId, request, AccessService.UserId(User)!.Value, ct);
    return Created($"/api/documents/{document.Id}", new { document.Id });
  }

  [HttpGet("cycles/{cycleId:guid}/certificate-eligibility")]
  public async Task<IActionResult> CertificateEligibility(Guid cycleId, CancellationToken ct) {
    if (!await _access.CanAccessCertificationCycleAsync(User, cycleId, ct)) return Forbid();
    if (!await _db.CertificationCycles.AnyAsync(x => x.Id == cycleId, ct)) return NotFound();
    var participants = await _db.CertificationParticipants.Where(x => x.CertificationCycleId == cycleId
            && x.Status == ParticipationStatus.Included)
        .OrderBy(x => x.Petani.Nama)
        .Select(x => new { x.Id, x.PetaniId, PetaniName = x.Petani.Nama, x.PoktanIdSnapshot,
          PoktanName = x.Petani.Poktan.Nama, x.CertificateEligibilityStatus,
          x.CertificateEligibilityReason, x.CertificateEligibilityDecidedByUserId,
          x.CertificateEligibilityDecidedAt }).ToListAsync(ct);
    var petaniIds = participants.Select(x => x.PetaniId).ToArray();
    var attendance = await _db.TrainingAttendance
        .Where(x => x.TrainingSession.CertificationCycleId == cycleId && petaniIds.Contains(x.PetaniId))
        .OrderBy(x => x.TrainingSession.ScheduledAt)
        .Select(x => new { x.PetaniId, x.TrainingSessionId, x.TrainingSession.Title,
          x.TrainingSession.ScheduledAt, x.TrainingSession.CompletedAt, x.Status, x.Notes }).ToListAsync(ct);
    var result = new List<object>();
    foreach (var participant in participants) {
      var missingDocuments = await MissingDocumentsAsync(cycleId, participant.Id, ct);
      result.Add(new { participant.Id, participant.PetaniId, participant.PetaniName,
        participant.PoktanIdSnapshot, participant.PoktanName, participant.CertificateEligibilityStatus,
        participant.CertificateEligibilityReason, participant.CertificateEligibilityDecidedByUserId,
        participant.CertificateEligibilityDecidedAt,
        MissingRequiredDocumentCount = missingDocuments.Sum(x => x.RequiredCount - x.VerifiedCount),
        MissingDocuments = missingDocuments,
        Attendance = attendance.Where(x => x.PetaniId == participant.PetaniId) });
    }
    return Ok(result);
  }

  [HttpPut("participants/{participantId:guid}/certificate-eligibility")]
  public async Task<IActionResult> SetCertificateEligibility(
      Guid participantId, CertificateEligibilityRequest request, CancellationToken ct) {
    var participant = await _db.CertificationParticipants.Include(x => x.CertificationCycle)
        .SingleOrDefaultAsync(x => x.Id == participantId, ct);
    if (participant is null) return NotFound();
    if (!await _access.CanManageCertificationCycleAsync(User, participant.CertificationCycleId, ct)) return Forbid();
    EnsureActiveCycle(participant.CertificationCycle.IsCurrent, participant.CertificationCycle.Status);
    if (participant.Status != ParticipationStatus.Included)
      return Conflict(new { message = "Keputusan sertifikat hanya berlaku untuk Petani yang diikutkan dalam siklus." });
    if (request.Status == CertificateEligibilityStatus.Pending)
      return ValidationProblem("Keputusan akhir harus Qualified atau Excluded.");
    if (request.Status == CertificateEligibilityStatus.Excluded && string.IsNullOrWhiteSpace(request.Reason))
      return ValidationProblem("Alasan wajib diisi ketika Petani dikeluarkan dari sertifikat.");
    if (request.Status == CertificateEligibilityStatus.Qualified) {
      var missing = await MissingRequiredDocumentsAsync(participant.CertificationCycleId, participant.Id, ct);
      if (missing > 0) return Conflict(new { message = "Dokumen wajib Petani atau Lahan belum diverifikasi.", missingRequiredDocumentCount = missing });
    }
    participant.CertificateEligibilityStatus = request.Status;
    participant.CertificateEligibilityReason = request.Reason?.Trim();
    participant.CertificateEligibilityDecidedByUserId = AccessService.UserId(User);
    participant.CertificateEligibilityDecidedAt = DateTimeOffset.UtcNow;
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  [HttpPost("cycles/{cycleId:guid}/certificates")]
  public async Task<IActionResult> IssueCertificate(Guid cycleId, IssueCertificateRequest request, CancellationToken ct) {
    if (!await _access.CanManageCertificationCycleAsync(User, cycleId, ct)) return Forbid();
    var cycle = await _db.CertificationCycles.Include(x => x.Audits).Include(x => x.Certificates).SingleOrDefaultAsync(x => x.Id == cycleId, ct);
    if (cycle is null) return NotFound();
    if (cycle.Certificates.Count != 0) return Conflict(new { message = "Siklus sudah memiliki sertifikat." });
    var external = cycle.Audits.SingleOrDefault(x => x.Type == AuditType.External);
    if (external?.Status is not (AuditStatus.Passed or AuditStatus.Closed)) return Conflict(new { message = "Audit eksternal belum lulus atau ditutup." });
    var document = await _db.Documents.SingleOrDefaultAsync(x => x.Id == request.DocumentId && x.CertificationCycleId == cycleId, ct);
    if (document is null) return ValidationProblem("Dokumen sertifikat belum diunggah untuk siklus ini.");
    var allParticipants = await _db.CertificationParticipants.Include(x => x.Petani).ThenInclude(x => x.Poktan)
        .Include(x => x.Lahan).ThenInclude(x => x.Lahan)
        .Where(x => x.CertificationCycleId == cycleId && x.Status == ParticipationStatus.Included).ToListAsync(ct);
    if (allParticipants.Any(x => x.CertificateEligibilityStatus == CertificateEligibilityStatus.Pending))
      return Conflict(new { message = "Setiap Petani yang diikutkan harus diputuskan Qualified atau Excluded sebelum penerbitan sertifikat." });
    var participants = allParticipants.Where(x => x.CertificateEligibilityStatus == CertificateEligibilityStatus.Qualified).ToList();
    if (participants.Count == 0) return Conflict(new { message = "Sedikitnya satu Petani harus Qualified untuk penerbitan sertifikat." });
    if (!await AssociationDocumentsCompleteAsync(cycleId, cycle.AssociationId, ct))
      return Conflict(new { message = "Dokumen wajib Association belum diverifikasi." });
    foreach (var participant in participants) {
      var missing = await MissingRequiredDocumentsAsync(cycleId, participant.Id, ct);
      if (missing > 0) return Conflict(new { message = $"Dokumen wajib untuk Petani '{participant.Petani.Nama}' atau Lahannya belum diverifikasi.", participantId = participant.Id, missingRequiredDocumentCount = missing });
    }
    var certificate = new Certificate { Id = Guid.NewGuid(), CertificationCycleId = cycleId, DocumentId = document.Id,
      Number = request.Number.Trim(), CertificationBody = request.CertificationBody.Trim(), IssuedDate = request.IssuedDate,
      ExpiryDate = request.ExpiryDate, UploadedByUserId = AccessService.UserId(User)!.Value,
      Participants = participants.Select(x => new CertificateParticipant { PetaniId = x.PetaniId, PetaniName = x.Petani.Nama,
        PoktanId = x.PoktanIdSnapshot, PoktanName = x.Petani.Poktan.Nama }).ToList(),
      Lahan = participants.SelectMany(x => x.Lahan.Where(l => l.Status == LahanParticipationStatus.Included)
        .Select(l => new CertificateLahan { LahanId = l.LahanId, PetaniId = x.PetaniId,
          LegalNumber = l.Lahan.NoLegalitas, LegalArea = l.Lahan.LuasLegalitas })).ToList() };
    cycle.Certificates.Add(certificate);
    _db.Certificates.Add(certificate);
    var certStep = await _db.CycleStepProgress.SingleAsync(x => x.CertificationCycleId == cycleId && x.Step == CertificationStep.CertificateIssuance, ct);
    certStep.Status = ProgressStatus.Completed; certStep.CompletedAt = DateTimeOffset.UtcNow;
    CertificationWorkflowService.EnsurePhase(cycle, CertificationPhase.CertificateIssuance);
    if (cycle.CurrentPhase < CertificationPhase.CertificateIssuance) {
      var certBlockers = await _readiness.EvaluateAsync(cycleId, CertificationPhase.CertificateIssuance, ct);
      if (certBlockers.Count != 0) return Conflict(new { message = "Persyaratan penerbitan sertifikat belum terpenuhi.", blockers = certBlockers });
      _workflow.Transition(cycle, CertificationPhase.CertificateIssuance, AccessService.UserId(User)!.Value, "Sertifikat diterbitkan dan diunggah.");
    }
    await _db.SaveChangesAsync(ct);
    var completeBlockers = await _readiness.EvaluateAsync(cycleId, CertificationPhase.Completed, ct);
    if (completeBlockers.Count != 0) return Conflict(new { message = "Persyaratan penyelesaian siklus belum terpenuhi.", blockers = completeBlockers });
    _workflow.Transition(cycle, CertificationPhase.Completed, AccessService.UserId(User)!.Value, "Siklus selesai.");
    var next = _workflow.CreateNextCycle(cycle, AccessService.UserId(User)!.Value, request.IssuedDate);
    _db.CertificationCycles.Add(next);
    foreach (var type in await _db.DocumentTypes.Where(x => x.IsActive).ToListAsync(ct)) next.DocumentRequirements.Add(new CycleDocumentRequirement {
      Id = Guid.NewGuid(), DocumentTypeId = type.Id,
      OwnerType = type.OwnerType == DocumentOwnerType.Petani ? DocumentOwnerType.CertificationParticipant
          : type.OwnerType == DocumentOwnerType.Lahan ? DocumentOwnerType.CertificationParticipantLahan : type.OwnerType,
      IsRequired = type.IsRequired
    });
    await _db.SaveChangesAsync(ct);
    return Created($"/api/certification/certificates/{certificate.Id}", new { certificate.Id, NextCycleId = next.Id });
  }

  [HttpGet("cycles/{cycleId:guid}/certificates")]
  public async Task<IActionResult> Certificates(Guid cycleId, CancellationToken ct) {
    if (!await _access.CanAccessCertificationCycleAsync(User, cycleId, ct)) return Forbid();
    var userId = AccessService.UserId(User);
    var isMember = User.IsInRole(AppRoles.MemberTaniBaik);
    return Ok(await _db.Certificates.Where(x => x.CertificationCycleId == cycleId).Select(x => new {
      x.Id, x.Number, x.CertificationBody, x.IssuedDate, x.ExpiryDate, x.Status, x.DocumentId,
      Participants = x.Participants.Where(p => !isMember || _db.Petani.Any(f => f.Id == p.PetaniId && f.ApplicationUserId == userId))
        .Select(p => new { p.PetaniId, p.PetaniName, p.PoktanId, p.PoktanName }),
      Lahan = x.Lahan.Where(l => !isMember || _db.Petani.Any(f => f.Id == l.PetaniId && f.ApplicationUserId == userId))
        .Select(l => new { l.LahanId, l.PetaniId, l.LegalNumber, l.LegalArea })
    }).ToListAsync(ct));
  }

  private async Task<int> MissingRequiredDocumentsAsync(Guid cycleId, Guid participantId, CancellationToken ct) {
    var missing = await MissingDocumentsAsync(cycleId, participantId, ct);
    return missing.Sum(x => x.RequiredCount - x.VerifiedCount);
  }

  private async Task<List<MissingDocumentResponse>> MissingDocumentsAsync(Guid cycleId, Guid participantId, CancellationToken ct) {
    var participant = await _db.CertificationParticipants.Where(x => x.Id == participantId && x.CertificationCycleId == cycleId)
        .Select(x => new { x.PetaniId, Lahan = x.Lahan.Where(l => l.Status == LahanParticipationStatus.Included)
            .Select(l => new { l.Id, l.LahanId }).ToList() }).SingleAsync(ct);
    var participantRequirements = await _db.CycleDocumentRequirements.Include(x => x.DocumentType).Where(x => x.CertificationCycleId == cycleId
        && x.IsRequired && x.OwnerType == DocumentOwnerType.CertificationParticipant).ToListAsync(ct);
    var lahanRequirements = await _db.CycleDocumentRequirements.Include(x => x.DocumentType).Where(x => x.CertificationCycleId == cycleId
        && x.IsRequired && x.OwnerType == DocumentOwnerType.CertificationParticipantLahan).ToListAsync(ct);
    var missing = new List<MissingDocumentResponse>();
    foreach (var requirement in participantRequirements) {
      var count = await _db.Documents.CountAsync(x => x.Status == DocumentStatus.Verified && x.DocumentTypeId == requirement.DocumentTypeId
          && (x.CertificationParticipantId == participantId || x.PetaniId == participant.PetaniId), ct);
      if (count < requirement.RequiredCount)
        missing.Add(new MissingDocumentResponse(DocumentOwnerType.CertificationParticipant, null, null,
            requirement.DocumentTypeId, requirement.DocumentType.Code, requirement.DocumentType.Nama, requirement.RequiredCount, count));
    }
    foreach (var lahan in participant.Lahan) {
      foreach (var requirement in lahanRequirements) {
        var count = await _db.Documents.CountAsync(x => x.Status == DocumentStatus.Verified && x.DocumentTypeId == requirement.DocumentTypeId
            && (x.CertificationParticipantLahanId == lahan.Id || x.LahanId == lahan.LahanId), ct);
        if (count < requirement.RequiredCount)
          missing.Add(new MissingDocumentResponse(DocumentOwnerType.CertificationParticipantLahan, lahan.LahanId, lahan.Id,
              requirement.DocumentTypeId, requirement.DocumentType.Code, requirement.DocumentType.Nama, requirement.RequiredCount, count));
      }
    }
    return missing;
  }

  private async Task<bool> AssociationDocumentsCompleteAsync(Guid cycleId, Guid associationId, CancellationToken ct) {
    var requirements = await _db.CycleDocumentRequirements.Where(x => x.CertificationCycleId == cycleId
        && x.IsRequired && x.OwnerType == DocumentOwnerType.Association).ToListAsync(ct);
    var counts = await _db.Documents.Where(x => x.AssociationId == associationId && x.Status == DocumentStatus.Verified)
        .GroupBy(x => x.DocumentTypeId).Select(x => new { Id = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
    return requirements.All(x => counts.GetValueOrDefault(x.DocumentTypeId) >= x.RequiredCount);
  }
}
