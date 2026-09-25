using System.ComponentModel.DataAnnotations;
using Esertifikasi.Api.Domain.Entities;

namespace Esertifikasi.Api.Models.Documents;

public sealed class DocumentQuery : PagedQuery {
  public Guid? DocumentTypeId { get; set; }
  public DocumentStatus? Status { get; set; }
}

public sealed class UploadDocumentRequest {
  [NotEmptyGuid]
  public Guid DocumentTypeId { get; set; }

  [StringLength(2000)]
  public string? Catatan { get; set; }

  [Required]
  public IFormFile File { get; set; } = null!;
}

public sealed class UploadDocumentVersionRequest {
  [StringLength(2000)]
  public string? Catatan { get; set; }

  [Required]
  public IFormFile File { get; set; } = null!;
}

public sealed class RejectDocumentRequest {
  [Required, StringLength(2000)]
  public string Reason { get; set; } = string.Empty;
}

public sealed record DocumentTypeResponse(
    Guid Id,
    string Code,
    string Nama,
    DocumentOwnerType OwnerType,
    bool IsRequired,
    string[] AllowedExtensions,
    long MaximumFileSize);

public sealed record DocumentListItem(
    Guid Id,
    Guid DocumentTypeId,
    string DocumentTypeCode,
    string DocumentTypeNama,
    DocumentStatus Status,
    string? Catatan,
    int LatestVersion,
    string? OriginalFileName,
    long? FileSize,
    DateTimeOffset UploadedAt,
    DateTimeOffset? ReviewedAt,
    string? RejectionReason);

public sealed record DocumentVersionResponse(
    Guid Id,
    int VersionNumber,
    string OriginalFileName,
    string ContentType,
    long FileSize,
    string Sha256Hash,
    Guid UploadedByUserId,
    DateTimeOffset UploadedAt);

public sealed record MissingDocumentType(Guid Id, string Code, string Nama);

public sealed record DocumentCompletenessResponse(
    int Required,
    int Uploaded,
    int Verified,
    int Rejected,
    decimal Percentage,
    IReadOnlyList<MissingDocumentType> MissingDocuments);

public sealed record AssociationDocumentCompletenessResponse(
    int Required,
    int Uploaded,
    int Submitted,
    int Verified,
    int Rejected,
    decimal Percentage,
    bool IsComplete,
    IReadOnlyList<MissingDocumentType> MissingDocuments);

public sealed class AttachAssociationDocumentRequest {
  [NotEmptyGuid]
  public Guid DocumentVersionId { get; set; }
}

public sealed class AttachMasterDocumentVersionRequest {
  [NotEmptyGuid]
  public Guid DocumentVersionId { get; set; }
}

public sealed record AssociationDocumentSubmissionResponse(
    Guid Id,
    Guid DocumentTypeId,
    string DocumentTypeCode,
    string DocumentTypeName,
    Guid DocumentId,
    Guid DocumentVersionId,
    int VersionNumber,
    string OriginalFileName,
    AssociationDocumentSubmissionStatus Status,
    DateTimeOffset AttachedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ReviewedAt,
    string? RejectionReason);

public sealed record AssociationDocumentRequirementResponse(
    Guid DocumentTypeId,
    string Code,
    string Name,
    bool IsRequired,
    int RequiredCount,
    AssociationDocumentSubmissionResponse? Submission);

public sealed record AssociationCycleDocumentStatusResponse(
    int Required,
    int Submitted,
    int Verified,
    bool IsSubmittedComplete,
    bool IsVerifiedComplete,
    IReadOnlyList<AssociationDocumentRequirementResponse> Requirements);

public static class DocumentModelExtensions {
  public static IQueryable<DocumentListItem> ToListItems(this IQueryable<DocumentRecord> query) {
    return query.Select(x => new DocumentListItem(
        x.Id,
        x.DocumentTypeId,
        x.DocumentType.Code,
        x.DocumentType.Nama,
        x.Status,
        x.Catatan,
        x.Versions.Max(v => (int?)v.VersionNumber) ?? 0,
        x.Versions.OrderByDescending(v => v.VersionNumber).Select(v => v.OriginalFileName).FirstOrDefault(),
        x.Versions.OrderByDescending(v => v.VersionNumber).Select(v => (long?)v.FileSize).FirstOrDefault(),
        x.UploadedAt,
        x.ReviewedAt,
        x.RejectionReason));
  }
}
