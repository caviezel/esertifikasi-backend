using Esertifikasi.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api.Services;

public sealed class CertificationWorkflowService {
  private static readonly CertificationStep[] CycleSteps = Enum.GetValues<CertificationStep>();
  private static readonly CertificationStep[] NewParticipantSteps = {
    CertificationStep.Disclosure, CertificationStep.DatabaseAndDocumentManagement
  };
  private readonly AppDbContext _db;

  public CertificationWorkflowService(AppDbContext db) => _db = db;

  public CertificationCycle CreateInitialCycle(Association association, Guid userId, DateOnly? startDate = null) {
    var now = DateTimeOffset.UtcNow;
    var cycle = new CertificationCycle {
      Id = Guid.NewGuid(), Association = association, AssociationId = association.Id,
      Type = CertificationCycleType.InitialCertification, SequenceNumber = 0,
      Status = CertificationCycleStatus.Active, CurrentPhase = CertificationPhase.Disclosure,
      StartDate = startDate ?? DateOnly.FromDateTime(now.UtcDateTime), IsCurrent = true,
      CreatedByUserId = userId, CreatedAt = now
    };
    InitializeCycle(cycle, userId, now);
    return cycle;
  }

  public CertificationCycle CreateNextCycle(CertificationCycle completedCycle, Guid userId, DateOnly issuedDate) {
    if (completedCycle.Status != CertificationCycleStatus.Completed) {
      throw new CertificationWorkflowException("Siklus sebelumnya harus selesai sebelum siklus berikutnya dibuat.");
    }
    // Sequence increments monotonically per association. Surveillance occupies
    // sequences 1-4 and every subsequent cycle is a recertification (sequence >= 5),
    // which satisfies the database check constraint. The previous implementation
    // forced every recertification back to sequence 5, which collided with the unique
    // index on (AssociationId, SequenceNumber) as soon as a second recertification was
    // created. Deriving the next sequence from the completed cycle keeps it unique.
    var sequence = completedCycle.SequenceNumber + 1;
    var type = sequence <= 4 ? CertificationCycleType.Surveillance : CertificationCycleType.Recertification;
    var now = DateTimeOffset.UtcNow;
    var cycle = new CertificationCycle {
      Id = Guid.NewGuid(), AssociationId = completedCycle.AssociationId, Type = type,
      SequenceNumber = sequence, Status = CertificationCycleStatus.Active,
      CurrentPhase = CertificationPhase.Disclosure, StartDate = issuedDate,
      TargetAuditStartDate = issuedDate.AddMonths(8), TargetAuditEndDate = issuedDate.AddMonths(12),
      IsCurrent = true, CreatedByUserId = userId, CreatedAt = now
    };
    completedCycle.IsCurrent = false;
    InitializeCycle(cycle, userId, now);
    return cycle;
  }

  public async Task<CertificationParticipant> EnrollParticipantAsync(
      CertificationCycle cycle, Guid petaniId, ParticipantEntryPath entryPath, IEnumerable<Guid> lahanIds,
      CancellationToken ct) {
    EnsureActiveBeforeAudit(cycle);
    var disclosureCompleted = await _db.Disclosures.AnyAsync(x => x.CertificationCycleId == cycle.Id && x.Status == DisclosureStatus.Completed, ct);
    var secondDisclosureOpen = await _db.Disclosures.AnyAsync(x => x.CertificationCycleId == cycle.Id && x.VersionNumber > 1
        && x.Status == DisclosureStatus.ApprovedForSecondDisclosure, ct);
    if (disclosureCompleted && !secondDisclosureOpen) {
      throw new CertificationWorkflowException("Petani atau Lahan hanya dapat ditambahkan sebelum disclosure selesai, kecuali disclosure kedua telah disetujui SuperAdmin.");
    }
    if (await _db.CertificationParticipants.AnyAsync(x => x.CertificationCycleId == cycle.Id && x.PetaniId == petaniId, ct)) {
      throw new CertificationWorkflowException("Petani sudah terdaftar pada siklus ini.");
    }
    var petani = await _db.Petani.Include(x => x.Poktan).Include(x => x.Lahan).SingleOrDefaultAsync(x => x.Id == petaniId, ct)
        ?? throw new KeyNotFoundException("Petani tidak ditemukan.");
    if (petani.Poktan.AssociationId != cycle.AssociationId) {
      // Poktan is not necessarily loaded; query the association when needed.
      var associationId = await _db.Poktan.Where(x => x.Id == petani.PoktanId).Select(x => x.AssociationId).SingleAsync(ct);
      if (associationId != cycle.AssociationId) throw new CertificationWorkflowException("Petani tidak berada dalam Association siklus ini.");
    }
    var selected = lahanIds.Distinct().ToHashSet();
    if (selected.Count == 0) selected = petani.Lahan.Select(x => x.Id).ToHashSet();
    if (selected.Except(petani.Lahan.Select(x => x.Id)).Any()) {
      throw new CertificationWorkflowException("Salah satu Lahan tidak dimiliki oleh Petani yang dipilih.");
    }
    var participant = new CertificationParticipant {
      Id = Guid.NewGuid(), CertificationCycleId = cycle.Id, PetaniId = petani.Id,
      PoktanIdSnapshot = petani.PoktanId, EntryPath = entryPath,
      StartingStep = CertificationStep.Disclosure
    };
    var priorCertifiedLahan = await _db.CertificateLahan.Where(x => selected.Contains(x.LahanId)).Select(x => x.LahanId).Distinct().ToListAsync(ct);
    foreach (var step in NewParticipantSteps) {
      participant.StepProgress.Add(new ParticipantStepProgress {
        Id = Guid.NewGuid(), Step = step, Status = ProgressStatus.NotStarted
      });
    }
    foreach (var lahanId in selected) {
      var existing = priorCertifiedLahan.Contains(lahanId);
      participant.Lahan.Add(new CertificationParticipantLahan {
        Id = Guid.NewGuid(), LahanId = lahanId,
        EntryPath = existing ? LahanEntryPath.PreviouslyCertified : LahanEntryPath.New,
        Status = LahanParticipationStatus.Pending
      });
    }
    _db.CertificationParticipants.Add(participant);
    return participant;
  }

  public async Task<IReadOnlyList<CertificationParticipantLahan>> AddParticipantLahanAsync(
      CertificationParticipant participant, IEnumerable<Guid> lahanIds, CancellationToken ct) {
    EnsureActiveBeforeAudit(participant.CertificationCycle);
    await EnsureScopeMutationAllowedAsync(participant.CertificationCycleId, ct);
    var selected = lahanIds.Distinct().ToArray();
    if (selected.Length == 0)
      throw new CertificationWorkflowException("Sedikitnya satu Lahan harus dipilih.");
    var owned = await _db.Lahan.Where(x => x.PetaniId == participant.PetaniId && selected.Contains(x.Id))
        .Select(x => x.Id).ToArrayAsync(ct);
    if (owned.Length != selected.Length)
      throw new CertificationWorkflowException("Salah satu Lahan tidak dimiliki oleh Petani yang dipilih.");
    var existing = await _db.CertificationParticipantLahan
        .Where(x => x.CertificationParticipantId == participant.Id && selected.Contains(x.LahanId))
        .Select(x => x.LahanId).ToArrayAsync(ct);
    if (existing.Length > 0)
      throw new CertificationWorkflowException("Salah satu Lahan sudah terdaftar pada siklus ini.");
    var certified = await _db.CertificateLahan.Where(x => selected.Contains(x.LahanId))
        .Select(x => x.LahanId).Distinct().ToArrayAsync(ct);
    var result = selected.Select(lahanId => new CertificationParticipantLahan {
      Id = Guid.NewGuid(), CertificationParticipantId = participant.Id, LahanId = lahanId,
      EntryPath = certified.Contains(lahanId) ? LahanEntryPath.PreviouslyCertified : LahanEntryPath.New,
      Status = LahanParticipationStatus.Pending
    }).ToList();
    _db.CertificationParticipantLahan.AddRange(result);
    return result;
  }

  public async Task EnsureScopeMutationAllowedAsync(Guid cycleId, CancellationToken ct) {
    var cycle = await _db.CertificationCycles.Where(x => x.Id == cycleId)
        .Select(x => new { x.IsCurrent, x.Status, x.CurrentPhase }).SingleOrDefaultAsync(ct)
        ?? throw new KeyNotFoundException("Siklus sertifikasi tidak ditemukan.");
    if (!cycle.IsCurrent)
      throw new CertificationWorkflowException(
          "Siklus historis hanya dapat dilihat.", "CERTIFICATION_CYCLE_READ_ONLY");
    if (cycle.Status != CertificationCycleStatus.Active)
      throw new CertificationWorkflowException("Scope peserta dan Lahan tidak dapat diubah pada siklus yang tidak aktif.");
    var hasCompletedDisclosure = await _db.Disclosures.AnyAsync(x => x.CertificationCycleId == cycleId
        && x.Status == DisclosureStatus.Completed, ct);
    if (!hasCompletedDisclosure) return;
    var approvedSecondDisclosure = await _db.Disclosures.AnyAsync(x => x.CertificationCycleId == cycleId
        && x.VersionNumber > 1 && x.Status == DisclosureStatus.ApprovedForSecondDisclosure, ct);
    if (!approvedSecondDisclosure)
      throw new CertificationWorkflowException("Scope yang telah didisclosure hanya dapat diubah melalui disclosure kedua yang disetujui SuperAdmin.");
  }

  public void Transition(CertificationCycle cycle, CertificationPhase target, Guid userId, string? notes) {
    EnsureValidTransition(cycle, target);
    var previous = cycle.CurrentPhase;
    cycle.CurrentPhase = target;
    if (target == CertificationPhase.Completed) {
      cycle.Status = CertificationCycleStatus.Completed;
      cycle.CompletedDate = DateOnly.FromDateTime(DateTime.UtcNow);
    }
    var transition = new WorkflowTransition {
      Id = Guid.NewGuid(), FromPhase = previous, ToPhase = target, UserId = userId, Notes = notes
    };
    cycle.Transitions.Add(transition);
    _db.WorkflowTransitions.Add(transition);
  }

  // Enforces that a cycle is active and currently sitting on the expected phase before
  // a phase-specific operation (mapping, baseline, training, monitoring, disclosure,
  // certificate issuance) is allowed. Throws CertificationWorkflowException, which the
  // global exception handler maps to HTTP 409 Conflict.
  public static void EnsurePhase(CertificationCycle cycle, CertificationPhase expected)
  {
    EnsureCurrent(cycle);
    EnsurePhase(cycle.Status, cycle.CurrentPhase, expected);
  }

  public static void EnsureCurrent(CertificationCycle cycle) {
    if (!cycle.IsCurrent)
      throw new CertificationWorkflowException(
          "Siklus historis hanya dapat dilihat.", "CERTIFICATION_CYCLE_READ_ONLY");
  }

  public static void EnsurePhase(CertificationCycleStatus status, CertificationPhase current, CertificationPhase expected) {
    if (status != CertificationCycleStatus.Active)
      throw new CertificationWorkflowException("Siklus tidak aktif.");
    if (current != expected)
      throw new CertificationWorkflowException($"Tindakan ini hanya dapat dilakukan pada tahap {expected}.");
  }

  public static void EnsurePhase(bool isCurrent, CertificationCycleStatus status,
      CertificationPhase current, CertificationPhase expected) {
    if (!isCurrent)
      throw new CertificationWorkflowException(
          "Siklus historis hanya dapat dilihat.", "CERTIFICATION_CYCLE_READ_ONLY");
    EnsurePhase(status, current, expected);
  }

  private static void InitializeCycle(CertificationCycle cycle, Guid userId, DateTimeOffset now) {
    foreach (var step in CycleSteps) cycle.StepProgress.Add(new CycleStepProgress { Id = Guid.NewGuid(), Step = step });
    cycle.Transitions.Add(new WorkflowTransition {
      Id = Guid.NewGuid(), FromPhase = CertificationPhase.Disclosure, ToPhase = CertificationPhase.Disclosure,
      UserId = userId, ChangedAt = now, Notes = "Siklus dibuat."
    });
  }

  private static void EnsureActiveBeforeAudit(CertificationCycle cycle) {
    EnsureCurrent(cycle);
    if (cycle.Status != CertificationCycleStatus.Active) {
      throw new CertificationWorkflowException("Peserta tidak dapat ditambahkan pada siklus yang tidak aktif.");
    }
  }

  private static void EnsureValidTransition(CertificationCycle cycle, CertificationPhase target) {
    EnsureCurrent(cycle);
    if (cycle.Status != CertificationCycleStatus.Active) throw new CertificationWorkflowException("Siklus tidak aktif.");
    var isFinalizationJump = target == CertificationPhase.CertificateIssuance
        && cycle.CurrentPhase < CertificationPhase.CertificateIssuance;
    if (!isFinalizationJump && (int)target != (int)cycle.CurrentPhase + 1)
      throw new CertificationWorkflowException("Tahap workflow harus dijalankan secara berurutan.");
    if (target == CertificationPhase.InternalAudit && !cycle.Disclosures.Any(x => x.Status == DisclosureStatus.Completed)) {
      throw new CertificationWorkflowException("Disclosure harus diselesaikan sebelum audit internal dimulai.");
    }
    if (target == CertificationPhase.ExternalAudit) {
      var internalAudit = cycle.Audits.SingleOrDefault(x => x.Type == AuditType.Internal);
      if (internalAudit?.Status != AuditStatus.Closed || internalAudit.Findings.Any(x => x.Status != FindingStatus.Closed)) {
        throw new CertificationWorkflowException("Audit internal dan seluruh temuannya harus ditutup sebelum audit eksternal.");
      }
    }
    if (target == CertificationPhase.Completed && !cycle.Certificates.Any()) {
      throw new CertificationWorkflowException("Sertifikat dan berkasnya harus tersedia sebelum siklus diselesaikan.");
    }
    if (target == CertificationPhase.CertificateIssuance) {
      var externalAudit = cycle.Audits.SingleOrDefault(x => x.Type == AuditType.External);
      if (externalAudit?.Status is not (AuditStatus.Passed or AuditStatus.Closed))
        throw new CertificationWorkflowException("Audit eksternal harus lulus atau ditutup sebelum penerbitan sertifikat.");
    }
  }
}

public sealed class CertificationWorkflowException : Exception {
  public CertificationWorkflowException(string message, string code = "CERTIFICATION_WORKFLOW_CONFLICT") : base(message) {
    Code = code;
  }

  public string Code { get; }
}
