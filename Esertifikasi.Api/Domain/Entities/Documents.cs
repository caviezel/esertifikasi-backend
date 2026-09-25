namespace Esertifikasi.Api.Domain.Entities;

public sealed class DocumentType {
  public Guid Id { get; set; }
  public string Code { get; set; } = string.Empty;
  public string Nama { get; set; } = string.Empty;
  public DocumentOwnerType OwnerType { get; set; }
  public bool IsRequired { get; set; }
  public string AllowedExtensions { get; set; } = ".pdf,.jpg,.jpeg,.png";
  public long MaximumFileSize { get; set; } = 10 * 1024 * 1024;
  public bool IsActive { get; set; } = true;
  public ICollection<DocumentRecord> Documents { get; set; } = new List<DocumentRecord>();
}

public sealed class DocumentRecord : ISoftDeletable {
  public Guid Id { get; set; }
  public Guid DocumentTypeId { get; set; }
  public DocumentType DocumentType { get; set; } = null!;
  public Guid? PetaniId { get; set; }
  public Petani? Petani { get; set; }
  public Guid? LahanId { get; set; }
  public Lahan? Lahan { get; set; }
  public Guid? AssociationId { get; set; }
  public Association? Association { get; set; }
  public Guid? CertificationCycleId { get; set; }
  public CertificationCycle? CertificationCycle { get; set; }
  public Guid? CertificationParticipantId { get; set; }
  public CertificationParticipant? CertificationParticipant { get; set; }
  public Guid? CertificationParticipantLahanId { get; set; }
  public CertificationParticipantLahan? CertificationParticipantLahan { get; set; }
  public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
  public string? Catatan { get; set; }
  public Guid UploadedByUserId { get; set; }
  public ApplicationUser UploadedByUser { get; set; } = null!;
  public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
  public Guid? ReviewedByUserId { get; set; }
  public ApplicationUser? ReviewedByUser { get; set; }
  public DateTimeOffset? ReviewedAt { get; set; }
  public string? RejectionReason { get; set; }
  public bool IsDeleted { get; set; }
  public DateTimeOffset? DeletedAt { get; set; }
  public ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();
}

public sealed class DocumentVersion {
  public Guid Id { get; set; }
  public Guid DocumentId { get; set; }
  public DocumentRecord Document { get; set; } = null!;
  public int VersionNumber { get; set; }
  public string StorageKey { get; set; } = string.Empty;
  public string OriginalFileName { get; set; } = string.Empty;
  public string ContentType { get; set; } = string.Empty;
  public string FileExtension { get; set; } = string.Empty;
  public long FileSize { get; set; }
  public string Sha256Hash { get; set; } = string.Empty;
  public Guid UploadedByUserId { get; set; }
  public ApplicationUser UploadedByUser { get; set; } = null!;
  public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum DocumentOwnerType { Petani, Lahan, CertificationCycle, CertificationParticipant, CertificationParticipantLahan, Association }

public enum DocumentStatus { Draft, Submitted, Verified, Rejected, Expired }
