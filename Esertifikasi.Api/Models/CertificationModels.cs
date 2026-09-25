using System.ComponentModel.DataAnnotations;
using Esertifikasi.Api.Domain.Entities;

namespace Esertifikasi.Api.Models;

public sealed record CertificationCycleResponse(
    Guid Id, Guid AssociationId, string AssociationName, CertificationCycleType Type, int SequenceNumber,
    CertificationCycleStatus Status, CertificationPhase CurrentPhase, DateOnly StartDate,
    DateOnly? TargetAuditStartDate, DateOnly? TargetAuditEndDate, DateOnly? CompletedDate, bool IsCurrent);

public sealed record CurrentCertificationCycleResponse(
    CertificationCycleResponse Cycle, CycleProgressResponse Progress,
    CertificationCyclePermissions Permissions,
    IReadOnlyList<CertificationPageAvailability> Pages);

public sealed record CertificationCyclePermissions(
    bool CanManageCycle, bool CanManagePreparation, bool CanTransition, bool CanConfigure,
    bool CanViewParticipants, bool CanManageParticipants,
    bool CanViewLandMapping, bool CanManageLandMapping,
    bool CanViewBaseline, bool CanManageBaseline,
    bool CanViewDisclosure, bool CanManageDisclosure, bool CanReviewDisclosure,
    bool CanViewDocuments, bool CanManageDocuments, bool CanVerifyDocuments,
    bool CanViewInternalAudit, bool CanManageInternalAudit,
    bool CanViewExternalAudit, bool CanManageExternalAudit,
    bool CanViewCertificates, bool CanIssueCertificate,
    bool CanViewSettings, bool CanManageSettings,
    bool CanViewTraining, bool CanManageTraining,
    bool CanViewMonitoring, bool CanManageMonitoring);

public sealed record CertificationPageAvailability(
    string Key, CertificationPhase Phase, bool IsAvailable, bool IsCurrent, bool CanEdit);

public sealed record CertificationProgressResponse(decimal Percentage, IReadOnlyList<CertificationStepProgressResponse> Steps);
public sealed record CertificationStepProgressResponse(CertificationStep Step, int Applicable, int Completed, decimal Percentage);
public sealed record CertificationReadinessBlocker(string Code, string Message, int? Remaining = null);
public sealed record CertificationReadinessResponse(
    Guid CycleId, CertificationPhase CurrentPhase, CertificationPhase? NextPhase,
    bool CanTransition, IReadOnlyList<CertificationReadinessBlocker> Blockers);

// Computed progress (items 3-5): derived from activity records on demand and enriched with
// overdue information calculated from existing fields (audit-finding due dates, the cycle
// audit target window, and remaining readiness requirements). No new columns are required.
public sealed record CycleProgressResponse(
    decimal Percentage, IReadOnlyList<CertificationStepProgressResponse> Steps, CycleOverdueInfo? Overdue);
public sealed record CycleOverdueInfo(
    DateOnly? AuditWindowDueDate, bool IsAuditWindowOverdue, int AuditWindowOverdueDays,
    int OverdueFindingsCount, int IncompleteRequirementCount);

// Requirement-management (item 6): safe endpoints built on the existing
// CycleDocumentRequirement and CycleStepProgress configuration tables.
public sealed class UpdateDocumentRequirementRequest {
  public bool? IsRequired { get; set; }
  [Range(1, int.MaxValue)] public int? RequiredCount { get; set; }
}
public sealed record CycleConfigurationResponse(
    CertificationPhase CurrentPhase, CertificationPhase? NextPhase, bool CanTransition,
    IReadOnlyList<CertificationReadinessBlocker> Blockers,
    IReadOnlyList<StepConfigResponse> Steps,
    IReadOnlyList<DocumentRequirementConfigResponse> DocumentRequirements);
public sealed record StepConfigResponse(CertificationStep Step, int? RequiredTarget, decimal? AuditPerformedPercentage);
public sealed record DocumentRequirementConfigResponse(
    Guid Id, Guid DocumentTypeId, string Code, string Name, DocumentOwnerType OwnerType, bool IsRequired, int RequiredCount);

public enum DocumentVerificationStatus { Missing, Draft, Submitted, Verified, Rejected, Expired }
public sealed record DocumentVerificationSummary(int Required, int Submitted, int Verified, int Rejected, int Missing);
public sealed record DocumentVerificationSubmission(
    Guid Id, Guid DocumentId, Guid DocumentVersionId, string OriginalFileName,
    DateTimeOffset SubmittedAt, DateTimeOffset? ReviewedAt, string? RejectionReason);
public sealed record DocumentVerificationRequirement(
    Guid RequirementId, Guid DocumentTypeId, string Code, string Name, bool IsRequired,
    int RequiredCount, int SubmittedCount, int VerifiedCount, DocumentVerificationStatus Status,
    DocumentVerificationSubmission? Submission);
public sealed record DocumentVerificationOwner(
    Guid? ParticipantId, Guid? ParticipantLahanId, Guid? PetaniId, Guid? LahanId,
    string? PetaniName, string? LegalNumber, IReadOnlyList<DocumentVerificationRequirement> Requirements);
public sealed record DocumentVerificationResponse(
    DocumentVerificationSummary Summary, DocumentVerificationOwner Association,
    IReadOnlyList<DocumentVerificationOwner> Participants,
    IReadOnlyList<DocumentVerificationOwner> Lahan,
    DocumentVerificationOwner Cycle);

public sealed class EnrollParticipantRequest {
  [NotEmptyGuid] public Guid PetaniId { get; set; }
  [EnumDataType(typeof(ParticipantEntryPath))] public ParticipantEntryPath EntryPath { get; set; }
  public Guid[] LahanIds { get; set; } = Array.Empty<Guid>();
}

public sealed class AddParticipantLahanRequest {
  [MinLength(1)] public Guid[] LahanIds { get; set; } = Array.Empty<Guid>();
}

public sealed class ParticipantStatusRequest {
  [EnumDataType(typeof(ParticipationStatus))] public ParticipationStatus Status { get; set; }
  [StringLength(2000)] public string? Reason { get; set; }
}

public sealed class ParticipantLahanStatusRequest {
  [EnumDataType(typeof(LahanParticipationStatus))] public LahanParticipationStatus Status { get; set; }
  [StringLength(2000)] public string? Reason { get; set; }
}

public sealed class StepProgressRequest {
  [EnumDataType(typeof(ProgressStatus))] public ProgressStatus Status { get; set; }
  [StringLength(2000)] public string? Notes { get; set; }
}

public sealed class TransitionRequest {
  [EnumDataType(typeof(CertificationPhase))] public CertificationPhase TargetPhase { get; set; }
  [StringLength(2000)] public string? Notes { get; set; }
}

public sealed class ConfigureStepRequest {
  [Range(1, int.MaxValue)] public int? RequiredTarget { get; set; }
  [Range(typeof(decimal), "0", "100")] public decimal? AuditPerformedPercentage { get; set; }
}

public sealed class CreateDisclosureRequest {
  [StringLength(2000)] public string? Notes { get; set; }
}

public sealed class DisclosureReviewRequest {
  public bool Approve { get; set; }
  [StringLength(2000)] public string? Notes { get; set; }
}

public sealed class DisclosureLahanSelectionRequest {
  [EnumDataType(typeof(LahanParticipationStatus))] public LahanParticipationStatus Status { get; set; }
  [StringLength(2000)] public string? Reason { get; set; }
}

public enum DisclosureEligibilityFilter { All, Eligible, Ineligible }
public enum DisclosureSelectionFilter { All, Selected, Unselected, Included, Excluded }

public sealed class DisclosureWorkspaceQuery {
  private int _page = 1;
  private int _pageSize = 20;
  public int Page { get => _page; set => _page = Math.Max(1, value); }
  public int PageSize { get => _pageSize; set => _pageSize = Math.Clamp(value, 1, 100); }
  public string? Search { get; set; }
  public DisclosureEligibilityFilter Eligibility { get; set; }
  public DisclosureSelectionFilter SelectionStatus { get; set; }
  public Guid? PoktanId { get; set; }
  public string? SortBy { get; set; }
  public bool Descending { get; set; }
}

public class BulkIncludeDisclosureLahanRequest {
  [MinLength(1), MaxLength(100)] public Guid[] LahanIds { get; set; } = Array.Empty<Guid>();
}

public sealed class BulkExcludeDisclosureLahanRequest : BulkIncludeDisclosureLahanRequest {
  [Required, StringLength(2000)] public string Reason { get; set; } = string.Empty;
}

public sealed record DisclosureWorkspaceDisclosure(
    Guid? Id, int VersionNumber, DisclosureStatus? Status, int SelectedLahan);
public sealed record DisclosureWorkspaceSummary(
    int TotalLahan, int ReadyLahan, int IncompleteLahan, int SelectedLahan, decimal ProgressPercentage);
public sealed record DisclosureWorkspaceItem(
    Guid LahanId, string? LegalNumber, Guid PetaniId, string PetaniName, Guid PoktanId, string PoktanName,
    int RequiredDocuments, int UploadedDocuments, int VerifiedDocuments, DocumentCompletionStatus DocumentsStatus,
    bool BoundaryAvailable, BaselineSummaryStatus BaselineStatus, bool IsCertified,
    bool Eligible, IReadOnlyList<string> Blockers,
    Guid? ParticipantId, Guid? ParticipantLahanId, ParticipationStatus? ParticipantStatus,
    LahanParticipationStatus? LahanStatus, LahanParticipationStatus? DisclosureStatus);
public sealed record DisclosureWorkspaceResponse(
    DisclosureWorkspaceDisclosure Disclosure, DisclosureWorkspaceSummary Summary,
    IReadOnlyList<DisclosureWorkspaceItem> Items, int Page, int PageSize, int TotalCount, int TotalPages);
public sealed record DisclosureWorkspaceMutationResponse(
    DisclosureWorkspaceDisclosure Disclosure, DisclosureWorkspaceSummary Summary,
    IReadOnlyList<DisclosureWorkspaceItem> AffectedRows);

public enum BaselineSummaryStatus { NotAssessed, Incomplete, Complete, Certified }
public sealed record BaselineAssessmentSummaryResponse(
    BaselineAssessmentType Type, ProgressStatus Status, string? Result, string? Source,
    string? Notes, DateTimeOffset? AssessedAt);
public sealed record BaselineLahanSummaryResponse(
    Guid LahanId, string? LegalNumber, Guid PetaniId, string PetaniName,
    bool IsCertified, bool BoundaryAvailable,
    BaselineSummaryStatus SummaryStatus, int RequiredChecks, int CompletedChecks,
    IReadOnlyList<BaselineAssessmentSummaryResponse> Assessments);
public sealed record AssociationBaselineSummaryResponse(
    Guid AssociationId, int TotalLahan, int ReadyLahan, int IncompleteLahan,
    IReadOnlyList<BaselineLahanSummaryResponse> Lahan);

public sealed class CreateTrainingRequest {
  [Required, StringLength(255)] public string Title { get; set; } = string.Empty;
  [StringLength(2000)] public string? Description { get; set; }
  public DateTimeOffset ScheduledAt { get; set; }
  public Guid[] PetaniIds { get; set; } = Array.Empty<Guid>();
}

public sealed class AddTrainingParticipantsRequest {
  public Guid[] PetaniIds { get; set; } = Array.Empty<Guid>();
}

public sealed class AttendanceRequest {
  [EnumDataType(typeof(AttendanceStatus))] public AttendanceStatus Status { get; set; }
  [StringLength(2000)] public string? Notes { get; set; }
}

public sealed class ReopenTrainingRequest {
  [Required, StringLength(2000)] public string Reason { get; set; } = string.Empty;
}

public sealed class MonitoringRequest {
  [NotEmptyGuid] public Guid PetaniId { get; set; }
  public Guid? LahanId { get; set; }
  [EnumDataType(typeof(MonitoringCategory))] public MonitoringCategory Category { get; set; }
  public DateOnly MonitoringMonth { get; set; }
  [EnumDataType(typeof(ProgressStatus))] public ProgressStatus Status { get; set; }
  [StringLength(2000)] public string? Notes { get; set; }
}

public sealed class UpdateMonitoringRequest {
  public Guid? LahanId { get; set; }
  [EnumDataType(typeof(MonitoringCategory))] public MonitoringCategory Category { get; set; }
  public DateOnly MonitoringMonth { get; set; }
  [EnumDataType(typeof(ProgressStatus))] public ProgressStatus Status { get; set; }
  [StringLength(2000)] public string? Notes { get; set; }
}

public sealed class MonitoringQuery {
  [NotEmptyGuid] public Guid AssociationId { get; set; }
  public Guid? PoktanId { get; set; }
  public Guid? PetaniId { get; set; }
  [Range(1, 9999)] public int? Year { get; set; }
  [Range(1, 12)] public int? Month { get; set; }
}

public sealed class BaselineAssessmentRequest {
  [EnumDataType(typeof(BaselineAssessmentType))] public BaselineAssessmentType Type { get; set; }
  [EnumDataType(typeof(ProgressStatus))] public ProgressStatus Status { get; set; }
  [StringLength(4000)] public string? Result { get; set; }
  [StringLength(1000)] public string? Source { get; set; }
  [StringLength(2000)] public string? Notes { get; set; }
}

public sealed class CreateAuditRequest {
  [EnumDataType(typeof(AuditType))] public AuditType Type { get; set; }
  public DateOnly ScheduledDate { get; set; }
}

public sealed class PerformAuditRequest {
  public DateOnly PerformedDate { get; set; }
  public bool Passed { get; set; }
  [StringLength(4000)] public string? Notes { get; set; }
}

public sealed class CreateFindingRequest {
  [Required, StringLength(100)] public string Code { get; set; } = string.Empty;
  [Required, StringLength(4000)] public string Description { get; set; } = string.Empty;
  public Guid? PoktanId { get; set; }
  [EnumDataType(typeof(FindingSeverity))] public FindingSeverity Severity { get; set; } = FindingSeverity.Minor;
  public DateOnly? DueDate { get; set; }
}

public sealed class UpdateFindingRequest {
  [StringLength(4000)] public string? PenyebabAnalisis { get; set; }
  [StringLength(4000)] public string? Corrections { get; set; }
  [StringLength(4000)] public string? CorrectiveAction { get; set; }
  public bool SubmitForReview { get; set; }
}

public sealed class CloseFindingRequest {
  [Required, StringLength(4000)] public string CorrectiveAction { get; set; } = string.Empty;
  [StringLength(4000)] public string? ClosureEvidence { get; set; }
}

public sealed class ExternalAuditReportUploadRequest {
  [NotEmptyGuid] public Guid DocumentTypeId { get; set; }
  [StringLength(2000)] public string? Catatan { get; set; }
  [Required] public bool? HasFindings { get; set; }
  [Required] public IFormFile File { get; set; } = null!;
}

public sealed class BulkAuditFindingRow {
  [Required, StringLength(100)] public string Code { get; set; } = string.Empty;
  [NotEmptyGuid] public Guid PoktanId { get; set; }
  [EnumDataType(typeof(FindingSeverity))] public FindingSeverity Severity { get; set; } = FindingSeverity.Minor;
  [Required, StringLength(4000)] public string Description { get; set; } = string.Empty;
  public DateOnly? DueDate { get; set; }
  [StringLength(4000)] public string? PenyebabAnalisis { get; set; }
  [StringLength(4000)] public string? Corrections { get; set; }
  [StringLength(4000)] public string? CorrectiveAction { get; set; }
}

public sealed class BulkImportAuditFindingsRequest {
  [Required, MinLength(1)] public List<BulkAuditFindingRow> Rows { get; set; } = new();
}

public sealed record AuditFindingImportPreviewRow(int RowNumber, BulkAuditFindingRow? Data, IReadOnlyList<string> Errors);
public sealed record AuditFindingImportPreview(bool IsValid, int TotalRows, int ValidRows, IReadOnlyList<AuditFindingImportPreviewRow> Rows);

public sealed class IssueCertificateRequest {
  [NotEmptyGuid] public Guid DocumentId { get; set; }
  [Required, StringLength(255)] public string Number { get; set; } = string.Empty;
  [Required, StringLength(255)] public string CertificationBody { get; set; } = string.Empty;
  public DateOnly IssuedDate { get; set; }
  public DateOnly? ExpiryDate { get; set; }
}

public sealed class CertificateEligibilityRequest {
  [EnumDataType(typeof(CertificateEligibilityStatus))]
  public CertificateEligibilityStatus Status { get; set; }
  [StringLength(2000)] public string? Reason { get; set; }
}

public sealed record MissingDocumentResponse(
    DocumentOwnerType OwnerType, Guid? LahanId, Guid? ParticipantLahanId,
    Guid DocumentTypeId, string DocumentTypeCode, string DocumentTypeName,
    int RequiredCount, int VerifiedCount);
