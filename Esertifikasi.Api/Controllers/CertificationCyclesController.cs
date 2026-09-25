using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models;
using Esertifikasi.Api.Security;
using Esertifikasi.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api.Controllers;

[ApiController, Route("api/certification-cycles"), Authorize]
public sealed class CertificationCyclesController : ControllerBase {
  private readonly AppDbContext _db;
  private readonly AccessService _access;
  private readonly CertificationWorkflowService _workflow;
  private readonly CertificationReadinessService _readiness;
  private readonly CertificationProgressService _progress;

  public CertificationCyclesController(AppDbContext db, AccessService access, CertificationWorkflowService workflow,
      CertificationReadinessService readiness, CertificationProgressService progress) {
    _db = db;
    _access = access;
    _workflow = workflow;
    _readiness = readiness;
    _progress = progress;
  }

  [HttpGet]
  public async Task<IActionResult> List([FromQuery] Guid? associationId, CancellationToken ct) {
    var query = _access.AccessibleCertificationCycles(User);
    if (associationId is not null) query = query.Where(x => x.AssociationId == associationId);
    return Ok(await query.OrderBy(x => x.Association.Nama).ThenByDescending(x => x.IsCurrent)
        .ThenByDescending(x => x.SequenceNumber)
        .Select(x => new CertificationCycleResponse(x.Id, x.AssociationId, x.Association.Nama, x.Type,
            x.SequenceNumber, x.Status, x.CurrentPhase, x.StartDate, x.TargetAuditStartDate,
            x.TargetAuditEndDate, x.CompletedDate, x.IsCurrent)).ToListAsync(ct));
  }

  [HttpGet("{id:guid}")]
  public async Task<IActionResult> Get(Guid id, CancellationToken ct) {
    if (!await _access.CanAccessCertificationCycleAsync(User, id, ct)) return Forbid();
    var cycle = await _db.CertificationCycles.Where(x => x.Id == id)
        .Select(x => new CertificationCycleResponse(x.Id, x.AssociationId, x.Association.Nama, x.Type,
            x.SequenceNumber, x.Status, x.CurrentPhase, x.StartDate, x.TargetAuditStartDate,
            x.TargetAuditEndDate, x.CompletedDate, x.IsCurrent)).SingleOrDefaultAsync(ct);
    return cycle is null ? NotFound() : Ok(cycle);
  }

  [HttpPost("backfill/{associationId:guid}"), Authorize(Roles = AppRoles.SuperAdmin)]
  public async Task<IActionResult> Backfill(Guid associationId, CancellationToken ct) {
    var association = await _db.Associations.Include(x => x.CertificationCycles).SingleOrDefaultAsync(x => x.Id == associationId, ct);
    if (association is null) return NotFound();
    if (association.CertificationCycles.Count != 0) return Conflict(new { message = "Association sudah memiliki siklus sertifikasi." });
    var cycle = _workflow.CreateInitialCycle(association, AccessService.UserId(User)!.Value);
    _db.CertificationCycles.Add(cycle);
    foreach (var type in await _db.DocumentTypes.Where(x => x.IsActive).ToListAsync(ct)) cycle.DocumentRequirements.Add(new CycleDocumentRequirement {
      Id = Guid.NewGuid(), DocumentTypeId = type.Id,
      OwnerType = type.OwnerType == DocumentOwnerType.Petani ? DocumentOwnerType.CertificationParticipant
          : type.OwnerType == DocumentOwnerType.Lahan ? DocumentOwnerType.CertificationParticipantLahan : type.OwnerType,
      IsRequired = type.IsRequired
    });
    await _db.SaveChangesAsync(ct);
    return Created($"/api/certification-cycles/{cycle.Id}", new { cycle.Id });
  }

  [HttpPost("{id:guid}/participants")]
  public async Task<IActionResult> Enroll(Guid id, EnrollParticipantRequest request, CancellationToken ct) {
    if (!await _access.CanManageCertificationCycleAsync(User, id, ct)) return Forbid();
    var cycle = await _db.CertificationCycles.SingleOrDefaultAsync(x => x.Id == id, ct);
    if (cycle is null) return NotFound();
    var participant = await _workflow.EnrollParticipantAsync(cycle, request.PetaniId, request.EntryPath, request.LahanIds, ct);
    await _db.SaveChangesAsync(ct);
    return Created($"/api/certification-cycles/{id}/participants/{participant.Id}", new { participant.Id });
  }

  [HttpGet("{id:guid}/participants")]
  public async Task<IActionResult> Participants(Guid id, CancellationToken ct) {
    if (!await _access.CanAccessCertificationCycleAsync(User, id, ct)) return Forbid();
    var userId = AccessService.UserId(User);
    var isMember = User.IsInRole(AppRoles.MemberTaniBaik);
    var query = _db.CertificationParticipants.Where(x => x.CertificationCycleId == id);
    if (isMember) query = query.Where(x => x.Petani.ApplicationUserId == userId);
    return Ok(await query.OrderBy(x => x.Petani.Nama).Select(x => new {
      x.Id, x.PetaniId, PetaniName = x.Petani.Nama, x.PoktanIdSnapshot, x.EntryPath, x.Status,
      x.StatusReason, x.JoinedAt, x.StartingStep,
      Lahan = x.Lahan.Select(l => new { l.Id, l.LahanId, l.EntryPath, l.Status, l.StatusReason,
        LegalNumber = l.Lahan.NoLegalitas, Commodity = l.Lahan.Komoditas,
        LegalArea = l.Lahan.LuasLegalitas, PlantedArea = l.Lahan.LuasTertanam,
        MapNumber = l.Lahan.NoPetaLahan })
    }).ToListAsync(ct));
  }

  [HttpPost("participants/{participantId:guid}/lahan")]
  public async Task<IActionResult> AddParticipantLahan(
      Guid participantId, AddParticipantLahanRequest request, CancellationToken ct) {
    var participant = await _db.CertificationParticipants.Include(x => x.CertificationCycle)
        .SingleOrDefaultAsync(x => x.Id == participantId, ct);
    if (participant is null) return NotFound();
    if (!await _access.CanManageCertificationCycleAsync(User, participant.CertificationCycleId, ct)) return Forbid();
    var added = await _workflow.AddParticipantLahanAsync(participant, request.LahanIds, ct);
    await _db.SaveChangesAsync(ct);
    return Created($"/api/certification-cycles/participants/{participantId}/lahan",
        added.Select(x => new { participantLahanId = x.Id, x.LahanId, x.EntryPath, x.Status }));
  }

  [HttpPost("participants/{participantId:guid}/status")]
  public async Task<IActionResult> ParticipantStatus(Guid participantId, ParticipantStatusRequest request, CancellationToken ct) {
    var participant = await _db.CertificationParticipants.Include(x => x.CertificationCycle)
        .SingleOrDefaultAsync(x => x.Id == participantId, ct);
    if (participant is null) return NotFound();
    if (!await _access.CanManageCertificationCycleAsync(User, participant.CertificationCycleId, ct)) return Forbid();
    if (!participant.CertificationCycle.IsCurrent) return HistoricalCycleConflict();
    if (participant.Status == request.Status && participant.StatusReason == request.Reason?.Trim()) return NoContent();
    await _workflow.EnsureScopeMutationAllowedAsync(participant.CertificationCycleId, ct);
    if (request.Status is ParticipationStatus.Excluded or ParticipationStatus.Withdrawn && string.IsNullOrWhiteSpace(request.Reason)) {
      return ValidationProblem(new ValidationProblemDetails { Errors = { [nameof(request.Reason)] = new[] { "Alasan wajib diisi untuk peserta yang dikeluarkan atau mengundurkan diri." } } });
    }
    participant.Status = request.Status;
    participant.StatusReason = request.Reason?.Trim();
    participant.CertificateEligibilityStatus = CertificateEligibilityStatus.Pending;
    participant.CertificateEligibilityReason = null;
    participant.CertificateEligibilityDecidedByUserId = null;
    participant.CertificateEligibilityDecidedAt = null;
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  [HttpPost("participants/{participantId:guid}/steps/{step}")]
  public async Task<IActionResult> SetParticipantStep(Guid participantId, CertificationStep step, StepProgressRequest request, CancellationToken ct) {
    if (!await _access.CanManageParticipantPreparationAsync(User, participantId, ct)) return Forbid();
    var progress = await _db.ParticipantStepProgress.Include(x => x.CertificationParticipant).ThenInclude(x => x.CertificationCycle)
        .SingleOrDefaultAsync(x => x.CertificationParticipantId == participantId && x.Step == step, ct);
    if (progress is null) return NotFound();
    if (!progress.CertificationParticipant.CertificationCycle.IsCurrent) return HistoricalCycleConflict();
    if (progress.Status == ProgressStatus.NotApplicable) return Conflict(new { message = "Tahap ini tidak berlaku untuk peserta." });
    var now = DateTimeOffset.UtcNow;
    progress.Status = request.Status;
    progress.Notes = request.Notes;
    progress.ResponsibleUserId = AccessService.UserId(User);
    if (request.Status == ProgressStatus.InProgress) progress.StartedAt ??= now;
    if (request.Status == ProgressStatus.Completed) { progress.StartedAt ??= now; progress.CompletedAt = now; }
    else progress.CompletedAt = null;
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  [HttpPost("{id:guid}/transition")]
  public async Task<IActionResult> Transition(Guid id, TransitionRequest request, CancellationToken ct) {
    if (!await _access.CanManageCertificationCycleAsync(User, id, ct)) return Forbid();
    var cycle = await _db.CertificationCycles.Include(x => x.Disclosures).Include(x => x.Certificates)
        .Include(x => x.Audits).ThenInclude(x => x.Findings).SingleOrDefaultAsync(x => x.Id == id, ct);
    if (cycle is null) return NotFound();
    CertificationWorkflowService.EnsureCurrent(cycle);
    if ((int)request.TargetPhase == (int)cycle.CurrentPhase + 1) {
      var blockers = await _readiness.EvaluateAsync(id, request.TargetPhase, ct);
      if (blockers.Count > 0) return Conflict(new CertificationReadinessResponse(
          id, cycle.CurrentPhase, request.TargetPhase, false, blockers));
    }
    _workflow.Transition(cycle, request.TargetPhase, AccessService.UserId(User)!.Value, request.Notes);
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  [HttpGet("{id:guid}/readiness")]
  public async Task<IActionResult> Readiness(Guid id, CancellationToken ct) {
    if (!await _access.CanAccessCertificationCycleAsync(User, id, ct)) return Forbid();
    var readiness = await _readiness.EvaluateNextAsync(id, ct);
    return readiness is null ? NotFound() : Ok(readiness);
  }

  [HttpGet("{id:guid}/transitions")]
  public async Task<IActionResult> Transitions(Guid id, CancellationToken ct) {
    if (!await _access.CanAccessCertificationCycleAsync(User, id, ct)) return Forbid();
    return Ok(await _db.WorkflowTransitions.Where(x => x.CertificationCycleId == id).OrderBy(x => x.ChangedAt)
        .Select(x => new { x.Id, x.FromPhase, x.ToPhase, x.UserId, x.ChangedAt, x.Notes }).ToListAsync(ct));
  }

  [HttpPost("participant-lahan/{participantLahanId:guid}/status")]
  public async Task<IActionResult> LahanStatus(Guid participantLahanId, ParticipantLahanStatusRequest request, CancellationToken ct) {
    var item = await _db.CertificationParticipantLahan.Include(x => x.CertificationParticipant).SingleOrDefaultAsync(x => x.Id == participantLahanId, ct);
    if (item is null) return NotFound();
    if (!await _access.CanManageCertificationCycleAsync(User, item.CertificationParticipant.CertificationCycleId, ct)) return Forbid();
    var isCurrent = await _db.CertificationCycles.AnyAsync(
        x => x.Id == item.CertificationParticipant.CertificationCycleId && x.IsCurrent, ct);
    if (!isCurrent) return HistoricalCycleConflict();
    if (item.Status == request.Status && item.StatusReason == request.Reason?.Trim()) return NoContent();
    await _workflow.EnsureScopeMutationAllowedAsync(item.CertificationParticipant.CertificationCycleId, ct);
    if (request.Status is LahanParticipationStatus.Excluded or LahanParticipationStatus.Withdrawn && string.IsNullOrWhiteSpace(request.Reason))
      return ValidationProblem(new ValidationProblemDetails { Errors = { [nameof(request.Reason)] = new[] { "Alasan wajib diisi." } } });
    item.Status = request.Status; item.StatusReason = request.Reason?.Trim();
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  [HttpPut("{id:guid}/step-config/{step}"), Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.AssociationAdmin}")]
  public async Task<IActionResult> ConfigureStep(Guid id, CertificationStep step, ConfigureStepRequest request, CancellationToken ct) {
    if (!await _access.CanManageCertificationCycleAsync(User, id, ct)) return Forbid();
    var cycle = await _db.CertificationCycles.Where(x => x.Id == id)
        .Select(x => new { x.Status, x.CurrentPhase, x.IsCurrent }).SingleOrDefaultAsync(ct);
    if (cycle is null) return NotFound();
    if (!cycle.IsCurrent) return HistoricalCycleConflict();
    if (cycle.Status != CertificationCycleStatus.Active || cycle.CurrentPhase >= CertificationPhase.InternalAudit)
      return Conflict(new { message = "Konfigurasi target dikunci setelah Audit Internal dimulai." });
    var config = await _db.CycleStepProgress.SingleOrDefaultAsync(x => x.CertificationCycleId == id && x.Step == step, ct);
    if (config is null) return NotFound();
    config.RequiredTarget = request.RequiredTarget;
    config.AuditPerformedPercentage = request.AuditPerformedPercentage;
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  [HttpGet("{id:guid}/document-requirements")]
  public async Task<IActionResult> DocumentRequirements(Guid id, CancellationToken ct) {
    if (!await _access.CanAccessCertificationCycleAsync(User, id, ct)) return Forbid();
    return Ok(await _db.CycleDocumentRequirements.Where(x => x.CertificationCycleId == id)
        .Select(x => new { x.Id, x.DocumentTypeId, Code = x.DocumentType.Code, Name = x.DocumentType.Nama,
          x.OwnerType, x.IsRequired, x.RequiredCount }).ToListAsync(ct));
  }

  [HttpGet("{id:guid}/progress")]
  public async Task<IActionResult> Progress(Guid id, CancellationToken ct) {
    if (!await _access.CanAccessCertificationCycleAsync(User, id, ct)) return Forbid();
    if (!await _db.CertificationCycles.AnyAsync(x => x.Id == id, ct)) return NotFound();
    Guid? scopeParticipantId = null;
    if (User.IsInRole(AppRoles.MemberTaniBaik)) {
      scopeParticipantId = await _db.CertificationParticipants
          .Where(x => x.CertificationCycleId == id && x.Petani.ApplicationUserId == AccessService.UserId(User))
          .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
      if (scopeParticipantId is null) return Ok(new CycleProgressResponse(0m, Array.Empty<CertificationStepProgressResponse>(), null));
    }
    var progress = await _progress.ComputeAsync(id, scopeParticipantId, ct);
    return progress is null ? NotFound() : Ok(progress);
  }

  [HttpPut("{id:guid}/document-requirements/{requirementId:guid}")]
  public async Task<IActionResult> UpdateDocumentRequirement(Guid id, Guid requirementId, UpdateDocumentRequirementRequest request, CancellationToken ct) {
    if (!await _access.CanManageCertificationCycleAsync(User, id, ct)) return Forbid();
    var cycle = await _db.CertificationCycles.Where(x => x.Id == id)
        .Select(x => new { x.Status, x.CurrentPhase, x.IsCurrent }).SingleOrDefaultAsync(ct);
    if (cycle is null) return NotFound();
    if (!cycle.IsCurrent) return HistoricalCycleConflict();
    if (cycle.Status != CertificationCycleStatus.Active || cycle.CurrentPhase >= CertificationPhase.InternalAudit)
      return Conflict(new { message = "Persyaratan dokumen dikunci setelah Audit Internal dimulai." });
    var requirement = await _db.CycleDocumentRequirements
        .SingleOrDefaultAsync(x => x.Id == requirementId && x.CertificationCycleId == id, ct);
    if (requirement is null) return NotFound();
    if (request.IsRequired.HasValue) requirement.IsRequired = request.IsRequired.Value;
    if (request.RequiredCount.HasValue) requirement.RequiredCount = request.RequiredCount.Value;
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  [HttpGet("{id:guid}/configuration")]
  public async Task<IActionResult> Configuration(Guid id, CancellationToken ct) {
    if (!await _access.CanManageCertificationCycleAsync(User, id, ct)) return Forbid();
    var cycle = await _db.CertificationCycles.Where(x => x.Id == id)
        .Select(x => new { x.Status, x.CurrentPhase }).SingleOrDefaultAsync(ct);
    if (cycle is null) return NotFound();
    var readiness = await _readiness.EvaluateNextAsync(id, ct);
    var steps = await _db.CycleStepProgress.Where(x => x.CertificationCycleId == id)
        .Select(x => new StepConfigResponse(x.Step, x.RequiredTarget, x.AuditPerformedPercentage)).ToListAsync(ct);
    var requirements = await _db.CycleDocumentRequirements.Where(x => x.CertificationCycleId == id)
        .Select(x => new DocumentRequirementConfigResponse(x.Id, x.DocumentTypeId, x.DocumentType.Code, x.DocumentType.Nama,
            x.OwnerType, x.IsRequired, x.RequiredCount)).ToListAsync(ct);
    return Ok(new CycleConfigurationResponse(cycle.CurrentPhase, readiness?.NextPhase, readiness?.CanTransition ?? false,
        readiness?.Blockers ?? Array.Empty<CertificationReadinessBlocker>(), steps, requirements));
  }

  private ConflictObjectResult HistoricalCycleConflict() => Conflict(new {
    code = "CERTIFICATION_CYCLE_READ_ONLY", message = "Siklus historis hanya dapat dilihat."
  });
}
