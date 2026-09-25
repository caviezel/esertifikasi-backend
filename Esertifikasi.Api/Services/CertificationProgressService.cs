using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api.Services;

// Derives certification step progress directly from the underlying activity records
// (mapping, baseline assessments, disclosures, verified documents, training attendance,
// monitoring records, audit results, and the issued certificate) instead of trusting the
// manually maintained ParticipantStepProgress.Status rows. Because every value is computed
// on demand from the live data, any document upload/verify/reject/replace/delete or
// activity update is reflected immediately by the API without extra synchronization work.
// ParticipantStepProgress is intentionally left in place for assignment, notes, deadlines,
// and other manual administrative metadata.
public sealed class CertificationProgressService {
  private readonly AppDbContext _db;
  private readonly CertificationReadinessService _readiness;

  public CertificationProgressService(AppDbContext db, CertificationReadinessService readiness) {
    _db = db;
    _readiness = readiness;
  }


  public async Task<CycleProgressResponse?> ComputeAsync(Guid cycleId, Guid? scopeParticipantId, CancellationToken ct) {
    var cycle = await _db.CertificationCycles.Where(x => x.Id == cycleId)
        .Select(x => new { x.Status, x.CurrentPhase, x.TargetAuditEndDate })
        .SingleOrDefaultAsync(ct);
    if (cycle is null) return null;

    var participants = await ActiveParticipants(cycleId, scopeParticipantId)
        .Select(x => new { x.Id, x.Status, HasIncludedLahan = x.Lahan.Any(l => l.Status == LahanParticipationStatus.Included) })
        .ToListAsync(ct);
    var participantIds = participants.Select(x => x.Id).ToList();

    var includedLahanIds = await _db.CertificationParticipantLahan
        .Where(x => x.CertificationParticipant.CertificationCycleId == cycleId
            && _db.Set<DisclosureLahan>().Any(dl => dl.CertificationParticipantLahanId == x.Id
                && dl.Disclosure.Status == DisclosureStatus.Completed
                && dl.Disclosure.VersionNumber == _db.Disclosures.Where(d => d.CertificationCycleId == cycleId
                    && d.Status == DisclosureStatus.Completed).Max(d => (int?)d.VersionNumber))
            && (scopeParticipantId == null || x.CertificationParticipantId == scopeParticipantId))
        .Select(x => x.Id).ToListAsync(ct);

    var steps = new List<CertificationStepProgressResponse>();
    var disclosureCompleted = await _db.Disclosures
        .AnyAsync(x => x.CertificationCycleId == cycleId && x.Status == DisclosureStatus.Completed, ct);
    steps.Add(Build(CertificationStep.Disclosure, 1, disclosureCompleted ? 1 : 0));

    var trainingSessions = await _db.TrainingSessions.CountAsync(x => x.CertificationCycleId == cycleId, ct);
    var completedTrainingSessions = await _db.TrainingSessions.CountAsync(x => x.CertificationCycleId == cycleId && x.CompletedAt != null, ct);
    steps.Add(Build(CertificationStep.Training, trainingSessions, completedTrainingSessions));

    var (docApplicable, docCompleted) = await ComputeDocumentStepAsync(cycleId, participantIds, includedLahanIds, ct);
    steps.Add(Build(CertificationStep.DatabaseAndDocumentManagement, docApplicable, docCompleted));

    // Monitoring is a continuous master-data activity and is not cycle progress.
    steps.Add(Build(CertificationStep.Monitoring, 0, 0));

    var internalStep = await CycleStepAsync(cycleId, CertificationStep.InternalAudit, ct);
    var externalStep = await CycleStepAsync(cycleId, CertificationStep.ExternalAudit, ct);
    steps.Add(AuditStep(CertificationStep.InternalAudit, internalStep));
    steps.Add(AuditStep(CertificationStep.ExternalAudit, externalStep));
    var hasCertificate = await _db.Certificates.AnyAsync(x => x.CertificationCycleId == cycleId, ct);
    steps.Add(Build(CertificationStep.CertificateIssuance, 1, hasCertificate ? 1 : 0));

    var applicableSteps = steps.Where(x => x.Applicable > 0
        && x.Step is not (CertificationStep.Training or CertificationStep.Monitoring)).ToList();
    var percentage = applicableSteps.Count == 0 ? 0m : decimal.Round(applicableSteps.Average(x => x.Percentage), 2);
    var overdue = await ComputeOverdueAsync(cycleId, cycle.Status, cycle.CurrentPhase, cycle.TargetAuditEndDate, ct);
    return new CycleProgressResponse(percentage, steps, overdue);
  }


  private static CertificationStepProgressResponse Build(CertificationStep step, int applicable, int completed) {
    var percentage = applicable == 0 ? 0m : decimal.Round(100m * completed / applicable, 2);
    return new(step, applicable, completed, percentage);
  }

  private static CertificationStepProgressResponse AuditStep(CertificationStep step, CycleStepState state) {
    var percentage = state.Status == ProgressStatus.Completed ? 100m
        : state.Status == ProgressStatus.InProgress ? (state.AuditPerformedPercentage ?? 0m) : 0m;
    return new(step, 1, state.Status == ProgressStatus.Completed ? 1 : 0, decimal.Round(percentage, 2));
  }

  private async Task<(int Applicable, int Completed)> ComputeDocumentStepAsync(
      Guid cycleId, IReadOnlyList<Guid> participantIds, IReadOnlyList<Guid> lahanIds, CancellationToken ct) {
    var assocReq = await _db.CycleDocumentRequirements
        .Where(x => x.CertificationCycleId == cycleId && x.OwnerType == DocumentOwnerType.Association && x.IsRequired).ToListAsync(ct);
    var partReq = await _db.CycleDocumentRequirements
        .Where(x => x.CertificationCycleId == cycleId && x.OwnerType == DocumentOwnerType.CertificationParticipant && x.IsRequired).ToListAsync(ct);
    var lahanReq = await _db.CycleDocumentRequirements
        .Where(x => x.CertificationCycleId == cycleId && x.OwnerType == DocumentOwnerType.CertificationParticipantLahan && x.IsRequired).ToListAsync(ct);

    var assocApplicable = assocReq.Sum(x => x.RequiredCount);
    var partApplicable = partReq.Count == 0 ? 0 : participantIds.Count * partReq.Sum(x => x.RequiredCount);
    var lahanApplicable = lahanReq.Count == 0 ? 0 : lahanIds.Count * lahanReq.Sum(x => x.RequiredCount);
    var applicable = assocApplicable + partApplicable + lahanApplicable;
    if (applicable == 0) return (0, 0);

    var associationId = await _db.CertificationCycles.Where(x => x.Id == cycleId)
        .Select(x => x.AssociationId).SingleAsync(ct);
    var assocVerified = await _db.Documents
        .Where(x => x.AssociationId == associationId && x.Status == DocumentStatus.Verified)
        .GroupBy(x => x.DocumentTypeId).Select(g => new { TypeId = g.Key, Count = g.Count() })
        .ToDictionaryAsync(x => x.TypeId, x => x.Count, ct);
    var assocCompleted = assocReq.Sum(r => Math.Min(r.RequiredCount, assocVerified.GetValueOrDefault(r.DocumentTypeId)));

    var participantOwners = await _db.CertificationParticipants.Where(x => participantIds.Contains(x.Id))
        .Select(x => new { x.Id, x.PetaniId }).ToListAsync(ct);
    var petaniIds = participantOwners.Select(x => x.PetaniId).ToArray();
    var participantDocuments = await _db.Documents.Where(x => x.Status == DocumentStatus.Verified
            && (x.CertificationParticipantId != null && participantIds.Contains(x.CertificationParticipantId.Value)
                || x.PetaniId != null && petaniIds.Contains(x.PetaniId.Value)))
        .Select(x => new { x.CertificationParticipantId, x.PetaniId, x.DocumentTypeId }).ToListAsync(ct);
    var partVerified = participantDocuments.Select(x => new {
      Owner = x.CertificationParticipantId ?? participantOwners.Single(o => o.PetaniId == x.PetaniId).Id,
      x.DocumentTypeId
    }).GroupBy(x => new { x.Owner, x.DocumentTypeId })
        .Select(g => new { g.Key.Owner, g.Key.DocumentTypeId, Count = g.Count() }).ToList();
    var partLookup = partVerified.ToDictionary(x => (x.Owner, x.DocumentTypeId), x => x.Count);
    var partCompleted = participantIds.Sum(owner => partReq.Sum(r => Math.Min(r.RequiredCount, partLookup.GetValueOrDefault((owner, r.DocumentTypeId)))));

    var lahanOwners = await _db.CertificationParticipantLahan.Where(x => lahanIds.Contains(x.Id))
        .Select(x => new { x.Id, x.LahanId }).ToListAsync(ct);
    var masterLahanIds = lahanOwners.Select(x => x.LahanId).ToArray();
    var lahanDocuments = await _db.Documents.Where(x => x.Status == DocumentStatus.Verified
            && (x.CertificationParticipantLahanId != null && lahanIds.Contains(x.CertificationParticipantLahanId.Value)
                || x.LahanId != null && masterLahanIds.Contains(x.LahanId.Value)))
        .Select(x => new { x.CertificationParticipantLahanId, x.LahanId, x.DocumentTypeId }).ToListAsync(ct);
    var lahanVerified = lahanDocuments.Select(x => new {
      Owner = x.CertificationParticipantLahanId ?? lahanOwners.Single(o => o.LahanId == x.LahanId).Id,
      x.DocumentTypeId
    }).GroupBy(x => new { x.Owner, x.DocumentTypeId })
        .Select(g => new { g.Key.Owner, g.Key.DocumentTypeId, Count = g.Count() }).ToList();
    var lahanLookup = lahanVerified.ToDictionary(x => (x.Owner, x.DocumentTypeId), x => x.Count);
    var lahanCompleted = lahanIds.Sum(owner => lahanReq.Sum(r => Math.Min(r.RequiredCount, lahanLookup.GetValueOrDefault((owner, r.DocumentTypeId)))));

    return (applicable, assocCompleted + partCompleted + lahanCompleted);
  }


  private async Task<CycleOverdueInfo?> ComputeOverdueAsync(
      Guid cycleId, CertificationCycleStatus status, CertificationPhase current, DateOnly? targetAuditEnd, CancellationToken ct) {
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var overdueFindings = await _db.AuditFindings
        .Where(x => x.CertificationAudit.CertificationCycleId == cycleId && x.Status != FindingStatus.Closed && x.DueDate < today)
        .Select(x => (DateOnly?)x.DueDate).ToListAsync(ct);
    var overdueFindingsCount = overdueFindings.Count;

    DateOnly? auditWindowDueDate = targetAuditEnd;
    var isAuditWindowOverdue = false;
    var auditWindowOverdueDays = 0;
    if (targetAuditEnd is not null && today > targetAuditEnd.Value) {
      var externalPerformed = await _db.CertificationAudits
          .AnyAsync(x => x.CertificationCycleId == cycleId && x.Type == AuditType.External && x.PerformedDate != null, ct);
      isAuditWindowOverdue = !externalPerformed;
      if (isAuditWindowOverdue) auditWindowOverdueDays = today.DayNumber - targetAuditEnd.Value.DayNumber;
    }

    var incompleteRequirementCount = 0;
    if (status == CertificationCycleStatus.Active && current != CertificationPhase.Completed) {
      var next = (CertificationPhase)((int)current + 1);
      var blockers = await _readiness.EvaluateAsync(cycleId, next, ct);
      incompleteRequirementCount = blockers.Sum(b => b.Remaining ?? 0);
    }

    if (auditWindowDueDate is null && overdueFindingsCount == 0 && incompleteRequirementCount == 0 && !isAuditWindowOverdue)
      return null;
    return new CycleOverdueInfo(auditWindowDueDate, isAuditWindowOverdue, auditWindowOverdueDays, overdueFindingsCount, incompleteRequirementCount);
  }

  private IQueryable<CertificationParticipant> ActiveParticipants(Guid cycleId, Guid? scope) => _db.CertificationParticipants
      .Where(x => x.CertificationCycleId == cycleId
          && _db.Set<DisclosureParticipant>().Any(dp => dp.CertificationParticipantId == x.Id
              && dp.Disclosure.Status == DisclosureStatus.Completed
              && dp.Disclosure.VersionNumber == _db.Disclosures.Where(d => d.CertificationCycleId == cycleId
                  && d.Status == DisclosureStatus.Completed).Max(d => (int?)d.VersionNumber))
          && (scope == null || x.Id == scope));
  private Task<int?> StepTargetAsync(Guid cycleId, CertificationStep step, CancellationToken ct) => _db.CycleStepProgress
      .Where(x => x.CertificationCycleId == cycleId && x.Step == step).Select(x => x.RequiredTarget).SingleAsync(ct);
  private async Task<CycleStepState> CycleStepAsync(Guid cycleId, CertificationStep step, CancellationToken ct) {
    var row = await _db.CycleStepProgress.Where(x => x.CertificationCycleId == cycleId && x.Step == step)
        .Select(x => new { x.Status, x.AuditPerformedPercentage }).SingleAsync(ct);
    return new(row.Status, row.AuditPerformedPercentage);
  }

  private sealed record CycleStepState(ProgressStatus Status, decimal? AuditPerformedPercentage);
}
