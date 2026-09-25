namespace Esertifikasi.Api.Domain.Entities;

public sealed class CertificationCycle {
  public Guid Id { get; set; }
  public Guid AssociationId { get; set; }
  public Association Association { get; set; } = null!;
  public CertificationCycleType Type { get; set; }
  public int SequenceNumber { get; set; }
  public CertificationCycleStatus Status { get; set; } = CertificationCycleStatus.Active;
  public CertificationPhase CurrentPhase { get; set; } = CertificationPhase.Disclosure;
  public DateOnly StartDate { get; set; }
  public DateOnly? TargetAuditStartDate { get; set; }
  public DateOnly? TargetAuditEndDate { get; set; }
  public DateOnly? CompletedDate { get; set; }
  public bool IsCurrent { get; set; }
  public Guid CreatedByUserId { get; set; }
  public ApplicationUser CreatedByUser { get; set; } = null!;
  public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
  public ICollection<CertificationParticipant> Participants { get; set; } = new List<CertificationParticipant>();
  public ICollection<CycleStepProgress> StepProgress { get; set; } = new List<CycleStepProgress>();
  public ICollection<WorkflowTransition> Transitions { get; set; } = new List<WorkflowTransition>();
  public ICollection<Disclosure> Disclosures { get; set; } = new List<Disclosure>();
  public ICollection<TrainingSession> TrainingSessions { get; set; } = new List<TrainingSession>();
  public ICollection<CertificationAudit> Audits { get; set; } = new List<CertificationAudit>();
  public ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();
  public ICollection<DocumentRecord> Documents { get; set; } = new List<DocumentRecord>();
  public ICollection<CycleDocumentRequirement> DocumentRequirements { get; set; } = new List<CycleDocumentRequirement>();
  public ICollection<AssociationDocumentSubmission> AssociationDocumentSubmissions { get; set; } = new List<AssociationDocumentSubmission>();
}

public sealed class CycleDocumentRequirement {
  public Guid Id { get; set; }
  public Guid CertificationCycleId { get; set; }
  public CertificationCycle CertificationCycle { get; set; } = null!;
  public Guid DocumentTypeId { get; set; }
  public DocumentType DocumentType { get; set; } = null!;
  public DocumentOwnerType OwnerType { get; set; }
  public bool IsRequired { get; set; }
  public int RequiredCount { get; set; } = 1;
}

public sealed class AssociationDocumentSubmission {
  public Guid Id { get; set; }
  public Guid CertificationCycleId { get; set; }
  public CertificationCycle CertificationCycle { get; set; } = null!;
  public Guid DocumentTypeId { get; set; }
  public DocumentType DocumentType { get; set; } = null!;
  public Guid DocumentId { get; set; }
  public DocumentRecord Document { get; set; } = null!;
  public Guid DocumentVersionId { get; set; }
  public DocumentVersion DocumentVersion { get; set; } = null!;
  public AssociationDocumentSubmissionStatus Status { get; set; } = AssociationDocumentSubmissionStatus.Draft;
  public Guid AttachedByUserId { get; set; }
  public ApplicationUser AttachedByUser { get; set; } = null!;
  public DateTimeOffset AttachedAt { get; set; } = DateTimeOffset.UtcNow;
  public Guid? SubmittedByUserId { get; set; }
  public ApplicationUser? SubmittedByUser { get; set; }
  public DateTimeOffset? SubmittedAt { get; set; }
  public Guid? ReviewedByUserId { get; set; }
  public ApplicationUser? ReviewedByUser { get; set; }
  public DateTimeOffset? ReviewedAt { get; set; }
  public string? RejectionReason { get; set; }
}

public enum AssociationDocumentSubmissionStatus { Draft, Submitted, Verified, Rejected }

public sealed class CertificationParticipant {
  public Guid Id { get; set; }
  public Guid CertificationCycleId { get; set; }
  public CertificationCycle CertificationCycle { get; set; } = null!;
  public Guid PetaniId { get; set; }
  public Petani Petani { get; set; } = null!;
  public Guid PoktanIdSnapshot { get; set; }
  public ParticipantEntryPath EntryPath { get; set; }
  public ParticipationStatus Status { get; set; } = ParticipationStatus.Selected;
  public string? StatusReason { get; set; }
  public CertificateEligibilityStatus CertificateEligibilityStatus { get; set; } = CertificateEligibilityStatus.Pending;
  public string? CertificateEligibilityReason { get; set; }
  public Guid? CertificateEligibilityDecidedByUserId { get; set; }
  public ApplicationUser? CertificateEligibilityDecidedByUser { get; set; }
  public DateTimeOffset? CertificateEligibilityDecidedAt { get; set; }
  public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
  public CertificationStep StartingStep { get; set; }
  public ICollection<CertificationParticipantLahan> Lahan { get; set; } = new List<CertificationParticipantLahan>();
  public ICollection<ParticipantStepProgress> StepProgress { get; set; } = new List<ParticipantStepProgress>();
}

public sealed class CertificationParticipantLahan {
  public Guid Id { get; set; }
  public Guid CertificationParticipantId { get; set; }
  public CertificationParticipant CertificationParticipant { get; set; } = null!;
  public Guid LahanId { get; set; }
  public Lahan Lahan { get; set; } = null!;
  public LahanParticipationStatus Status { get; set; } = LahanParticipationStatus.Pending;
  public LahanEntryPath EntryPath { get; set; }
  public string? StatusReason { get; set; }
  public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
  public LandMappingRecord? LandMapping { get; set; }
  public ICollection<DocumentRecord> Documents { get; set; } = new List<DocumentRecord>();
}

public sealed class ParticipantStepProgress {
  public Guid Id { get; set; }
  public Guid CertificationParticipantId { get; set; }
  public CertificationParticipant CertificationParticipant { get; set; } = null!;
  public CertificationStep Step { get; set; }
  public ProgressStatus Status { get; set; } = ProgressStatus.NotStarted;
  public DateTimeOffset? StartedAt { get; set; }
  public DateTimeOffset? CompletedAt { get; set; }
  public string? Notes { get; set; }
  public Guid? ResponsibleUserId { get; set; }
  public ApplicationUser? ResponsibleUser { get; set; }
}

public sealed class CycleStepProgress {
  public Guid Id { get; set; }
  public Guid CertificationCycleId { get; set; }
  public CertificationCycle CertificationCycle { get; set; } = null!;
  public CertificationStep Step { get; set; }
  public ProgressStatus Status { get; set; } = ProgressStatus.NotStarted;
  public int? RequiredTarget { get; set; }
  public decimal? AuditPerformedPercentage { get; set; }
  public DateTimeOffset? StartedAt { get; set; }
  public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class WorkflowTransition {
  public Guid Id { get; set; }
  public Guid CertificationCycleId { get; set; }
  public CertificationCycle CertificationCycle { get; set; } = null!;
  public CertificationPhase FromPhase { get; set; }
  public CertificationPhase ToPhase { get; set; }
  public Guid UserId { get; set; }
  public ApplicationUser User { get; set; } = null!;
  public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
  public string? Notes { get; set; }
}

public sealed class Disclosure {
  public Guid Id { get; set; }
  public Guid CertificationCycleId { get; set; }
  public CertificationCycle CertificationCycle { get; set; } = null!;
  public int VersionNumber { get; set; }
  public DisclosureStatus Status { get; set; } = DisclosureStatus.Draft;
  public Guid CreatedByUserId { get; set; }
  public ApplicationUser CreatedByUser { get; set; } = null!;
  public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
  public DateTimeOffset? SubmittedAt { get; set; }
  public DateTimeOffset? CompletedAt { get; set; }
  public Guid? ReviewedByUserId { get; set; }
  public ApplicationUser? ReviewedByUser { get; set; }
  public DateTimeOffset? ReviewedAt { get; set; }
  public string? ReviewNotes { get; set; }
  public ICollection<DisclosureParticipant> Participants { get; set; } = new List<DisclosureParticipant>();
  public ICollection<DisclosureLahan> Lahan { get; set; } = new List<DisclosureLahan>();
}

public sealed class DisclosureParticipant {
  public Guid DisclosureId { get; set; }
  public Disclosure Disclosure { get; set; } = null!;
  public Guid CertificationParticipantId { get; set; }
  public CertificationParticipant CertificationParticipant { get; set; } = null!;
}

public sealed class DisclosureLahan {
  public Guid DisclosureId { get; set; }
  public Disclosure Disclosure { get; set; } = null!;
  public Guid CertificationParticipantLahanId { get; set; }
  public CertificationParticipantLahan CertificationParticipantLahan { get; set; } = null!;
}

public sealed class TrainingSession {
  public Guid Id { get; set; }
  public Guid CertificationCycleId { get; set; }
  public CertificationCycle CertificationCycle { get; set; } = null!;
  public string Title { get; set; } = string.Empty;
  public string? Description { get; set; }
  public DateTimeOffset ScheduledAt { get; set; }
  public DateTimeOffset? CompletedAt { get; set; }
  public Guid CreatedByUserId { get; set; }
  public ApplicationUser CreatedByUser { get; set; } = null!;
  public ICollection<TrainingAttendance> Attendance { get; set; } = new List<TrainingAttendance>();
}

public sealed class TrainingAttendance {
  public Guid TrainingSessionId { get; set; }
  public TrainingSession TrainingSession { get; set; } = null!;
  public Guid PetaniId { get; set; }
  public Petani Petani { get; set; } = null!;
  public AttendanceStatus Status { get; set; } = AttendanceStatus.Invited;
  public string? Notes { get; set; }
}

public sealed class MonitoringRecord {
  public Guid Id { get; set; }
  public Guid PetaniId { get; set; }
  public Petani Petani { get; set; } = null!;
  public Guid? LahanId { get; set; }
  public Lahan? Lahan { get; set; }
  public MonitoringCategory Category { get; set; }
  public DateOnly MonitoringMonth { get; set; }
  public ProgressStatus Status { get; set; } = ProgressStatus.NotStarted;
  public string? Notes { get; set; }
  public Guid ResponsibleUserId { get; set; }
  public ApplicationUser ResponsibleUser { get; set; } = null!;
  public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class BaselineAssessment {
  public Guid Id { get; set; }
  public Guid LahanId { get; set; }
  public Lahan Lahan { get; set; } = null!;
  public BaselineAssessmentType Type { get; set; }
  public ProgressStatus Status { get; set; } = ProgressStatus.NotStarted;
  public string? Result { get; set; }
  public string? Source { get; set; }
  public string? Notes { get; set; }
  public Guid? AssessedByUserId { get; set; }
  public ApplicationUser? AssessedByUser { get; set; }
  public DateTimeOffset? AssessedAt { get; set; }
}

public sealed class LandMappingRecord {
  public Guid Id { get; set; }
  public Guid CertificationParticipantLahanId { get; set; }
  public CertificationParticipantLahan CertificationParticipantLahan { get; set; } = null!;
  public ProgressStatus Status { get; set; } = ProgressStatus.NotStarted;
  public string? GeoJson { get; set; }
  public decimal? MappedArea { get; set; }
  public string? Notes { get; set; }
  public Guid? MappedByUserId { get; set; }
  public ApplicationUser? MappedByUser { get; set; }
  public DateTimeOffset? MappedAt { get; set; }
}

public sealed class CertificationAudit {
  public Guid Id { get; set; }
  public Guid CertificationCycleId { get; set; }
  public CertificationCycle CertificationCycle { get; set; } = null!;
  public AuditType Type { get; set; }
  public AuditStatus Status { get; set; } = AuditStatus.Scheduled;
  public DateOnly ScheduledDate { get; set; }
  public DateOnly? PerformedDate { get; set; }
  public DateOnly? FindingsDueDate { get; set; }
  public bool? HasFindings { get; set; }
  public DateTimeOffset? ClosedAt { get; set; }
  public string? ResultNotes { get; set; }
  public Guid CreatedByUserId { get; set; }
  public ApplicationUser CreatedByUser { get; set; } = null!;
  public ICollection<AuditFinding> Findings { get; set; } = new List<AuditFinding>();
}

public sealed class AuditFinding {
  public Guid Id { get; set; }
  public Guid CertificationAuditId { get; set; }
  public CertificationAudit CertificationAudit { get; set; } = null!;
  public string Code { get; set; } = string.Empty;
  public string Description { get; set; } = string.Empty;
  public Guid? PoktanId { get; set; }
  public Poktan? Poktan { get; set; }
  public FindingSeverity Severity { get; set; } = FindingSeverity.Minor;
  public string? PenyebabAnalisis { get; set; }
  public string? Corrections { get; set; }
  public FindingStatus Status { get; set; } = FindingStatus.Open;
  public DateOnly DueDate { get; set; }
  public string? CorrectiveAction { get; set; }
  public string? ClosureEvidence { get; set; }
  public Guid? ClosedByUserId { get; set; }
  public ApplicationUser? ClosedByUser { get; set; }
  public DateTimeOffset? ClosedAt { get; set; }
}

public sealed class Certificate {
  public Guid Id { get; set; }
  public Guid CertificationCycleId { get; set; }
  public CertificationCycle CertificationCycle { get; set; } = null!;
  public string Number { get; set; } = string.Empty;
  public string CertificationBody { get; set; } = string.Empty;
  public DateOnly IssuedDate { get; set; }
  public DateOnly? ExpiryDate { get; set; }
  public CertificateStatus Status { get; set; } = CertificateStatus.Issued;
  public Guid DocumentId { get; set; }
  public DocumentRecord Document { get; set; } = null!;
  public Guid UploadedByUserId { get; set; }
  public ApplicationUser UploadedByUser { get; set; } = null!;
  public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
  public ICollection<CertificateParticipant> Participants { get; set; } = new List<CertificateParticipant>();
  public ICollection<CertificateLahan> Lahan { get; set; } = new List<CertificateLahan>();
}

public sealed class CertificateParticipant {
  public Guid CertificateId { get; set; }
  public Certificate Certificate { get; set; } = null!;
  public Guid PetaniId { get; set; }
  public string PetaniName { get; set; } = string.Empty;
  public Guid PoktanId { get; set; }
  public string PoktanName { get; set; } = string.Empty;
}

public sealed class CertificateLahan {
  public Guid CertificateId { get; set; }
  public Certificate Certificate { get; set; } = null!;
  public Guid LahanId { get; set; }
  public Guid PetaniId { get; set; }
  public string? LegalNumber { get; set; }
  public decimal? LegalArea { get; set; }
}

public enum CertificationCycleType { InitialCertification, Surveillance, Recertification }
public enum CertificationCycleStatus { Planned, Active, Completed, Cancelled }
public enum CertificationPhase { Disclosure, Preparation, InternalAudit, ExternalAudit, CertificateIssuance, Completed }
public enum CertificationStep { Disclosure, Training, DatabaseAndDocumentManagement, Monitoring, InternalAudit, ExternalAudit, CertificateIssuance }
public enum ParticipantEntryPath { New, Existing }
public enum ParticipationStatus { Selected, Confirmed, Included, Excluded, Withdrawn }
public enum CertificateEligibilityStatus { Pending, Qualified, Excluded }
public enum LahanEntryPath { New, PreviouslyCertified }
public enum LahanParticipationStatus { Pending, Included, Excluded, Withdrawn }
public enum ProgressStatus { NotApplicable, NotStarted, InProgress, Completed }
public enum DisclosureStatus { Draft, PendingSecondDisclosureApproval, ApprovedForSecondDisclosure, Completed, Rejected }
public enum AttendanceStatus { Invited, Attended, Absent, Excused }
public enum MonitoringCategory { Budidaya, Ics }
public enum BaselineAssessmentType { Nkt, Rtrw, Gambut, StatusKawasan, StatusKonsesi, Pippip, Perairan }
public enum AuditType { Internal, External }
public enum AuditStatus { Scheduled, Performed, FindingsOpen, Passed, Failed, Closed }
public enum FindingStatus { Open, CorrectiveActionSubmitted, Closed }
public enum FindingSeverity { Minor, Major }
public enum CertificateStatus { Issued, Expired, Revoked, Superseded }
