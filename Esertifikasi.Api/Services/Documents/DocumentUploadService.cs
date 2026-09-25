using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models.Documents;
using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api.Services.Documents;

public sealed class DocumentUploadService {
  private readonly AppDbContext _db;
  private readonly FileValidationService _validation;
  private readonly IFileStorage _storage;

  public DocumentUploadService(AppDbContext db, FileValidationService validation, IFileStorage storage) {
    _db = db;
    _validation = validation;
    _storage = storage;
  }

  public async Task<DocumentRecord> CreateAsync(
      DocumentOwnerType ownerType,
      Guid ownerId,
      UploadDocumentRequest request,
      Guid userId,
      CancellationToken ct) {
    var type = await _db.DocumentTypes.SingleOrDefaultAsync(x => x.Id == request.DocumentTypeId && x.IsActive
        && (x.OwnerType == ownerType
            || ownerType == DocumentOwnerType.CertificationParticipant && x.OwnerType == DocumentOwnerType.Petani
            || ownerType == DocumentOwnerType.CertificationParticipantLahan && x.OwnerType == DocumentOwnerType.Lahan), ct);
    if (type is null) {
      throw new DocumentOperationException("Jenis dokumen yang dipilih tidak valid atau tidak aktif.");
    }

    var duplicateExists = ownerType switch {
      DocumentOwnerType.Petani => await _db.Documents.AnyAsync(x => x.PetaniId == ownerId && x.DocumentTypeId == type.Id, ct),
      DocumentOwnerType.Lahan => await _db.Documents.AnyAsync(x => x.LahanId == ownerId && x.DocumentTypeId == type.Id, ct),
      DocumentOwnerType.Association => await _db.Documents.AnyAsync(x => x.AssociationId == ownerId && x.DocumentTypeId == type.Id, ct),
      DocumentOwnerType.CertificationCycle => await _db.Documents.AnyAsync(x => x.CertificationCycleId == ownerId && x.DocumentTypeId == type.Id, ct),
      DocumentOwnerType.CertificationParticipant => await _db.Documents.AnyAsync(x => x.CertificationParticipantId == ownerId && x.DocumentTypeId == type.Id, ct),
      DocumentOwnerType.CertificationParticipantLahan => await _db.Documents.AnyAsync(x => x.CertificationParticipantLahanId == ownerId && x.DocumentTypeId == type.Id, ct),
      _ => throw new ArgumentOutOfRangeException(nameof(ownerType))
    };
    if (duplicateExists) {
      throw new DocumentConflictException("Pemilik sudah memiliki jenis dokumen ini. Unggah versi baru pada dokumen yang sudah ada.");
    }

    await using var file = await _validation.ValidateAsync(request.File, type, ct);
    var storageKey = await _storage.SaveAsync(file.Content, file.Extension, ct);
    var now = DateTimeOffset.UtcNow;
    var document = new DocumentRecord {
      Id = Guid.NewGuid(),
      DocumentTypeId = type.Id,
      PetaniId = ownerType == DocumentOwnerType.Petani ? ownerId : null,
      LahanId = ownerType == DocumentOwnerType.Lahan ? ownerId : null,
      AssociationId = ownerType == DocumentOwnerType.Association ? ownerId : null,
      CertificationCycleId = ownerType == DocumentOwnerType.CertificationCycle ? ownerId : null,
      CertificationParticipantId = ownerType == DocumentOwnerType.CertificationParticipant ? ownerId : null,
      CertificationParticipantLahanId = ownerType == DocumentOwnerType.CertificationParticipantLahan ? ownerId : null,
      Catatan = request.Catatan,
      UploadedByUserId = userId,
      UploadedAt = now,
      Status = DocumentStatus.Draft
    };
    document.Versions.Add(CreateVersion(document.Id, 1, storageKey, file, userId, now));
    _db.Documents.Add(document);

    try {
      await _db.SaveChangesAsync(ct);
      return document;
    }
    catch {
      await _storage.DeleteAsync(storageKey, CancellationToken.None);
      throw;
    }
  }

  public async Task<DocumentVersion> AddVersionAsync(
      DocumentRecord document,
      UploadDocumentVersionRequest request,
      Guid userId,
      CancellationToken ct) {
    var type = await _db.DocumentTypes.SingleAsync(x => x.Id == document.DocumentTypeId, ct);
    if (!type.IsActive) {
      throw new DocumentOperationException("Jenis dokumen tidak aktif dan tidak dapat menerima versi baru.");
    }
    await using var file = await _validation.ValidateAsync(request.File, type, ct);
    var storageKey = await _storage.SaveAsync(file.Content, file.Extension, ct);
    var versionNumber = await _db.DocumentVersions
        .Where(x => x.DocumentId == document.Id)
        .MaxAsync(x => (int?)x.VersionNumber, ct) ?? 0;
    var now = DateTimeOffset.UtcNow;
    var version = CreateVersion(document.Id, versionNumber + 1, storageKey, file, userId, now);

    document.Catatan = request.Catatan;
    document.UploadedByUserId = userId;
    document.UploadedAt = now;
    document.Status = DocumentStatus.Draft;
    document.ReviewedByUserId = null;
    document.ReviewedAt = null;
    document.RejectionReason = null;
    _db.DocumentVersions.Add(version);

    try {
      await _db.SaveChangesAsync(ct);
      return version;
    }
    catch {
      await _storage.DeleteAsync(storageKey, CancellationToken.None);
      throw;
    }
  }

  public async Task<DocumentRecord> AttachMasterVersionAsync(
      DocumentOwnerType ownerType,
      Guid ownerId,
      Guid sourceVersionId,
      Guid userId,
      CancellationToken ct) {
    if (ownerType is not (DocumentOwnerType.CertificationParticipant or DocumentOwnerType.CertificationParticipantLahan))
      throw new ArgumentOutOfRangeException(nameof(ownerType));

    var source = await _db.DocumentVersions.Include(x => x.Document).ThenInclude(x => x.DocumentType)
        .SingleOrDefaultAsync(x => x.Id == sourceVersionId, ct);
    if (source is null) throw new KeyNotFoundException("Versi dokumen sumber tidak ditemukan.");

    var sourceBelongsToOwner = ownerType == DocumentOwnerType.CertificationParticipant
        ? await _db.CertificationParticipants.AnyAsync(x => x.Id == ownerId && source.Document.PetaniId == x.PetaniId, ct)
        : await _db.CertificationParticipantLahan.AnyAsync(x => x.Id == ownerId && source.Document.LahanId == x.LahanId, ct);
    var expectedSourceType = ownerType == DocumentOwnerType.CertificationParticipant
        ? DocumentOwnerType.Petani : DocumentOwnerType.Lahan;
    if (!sourceBelongsToOwner || source.Document.DocumentType.OwnerType != expectedSourceType)
      throw new DocumentOperationException("Versi dokumen tidak berasal dari data induk pemilik yang sesuai.");
    if (!source.Document.DocumentType.IsActive)
      throw new DocumentOperationException("Jenis dokumen sumber tidak aktif.");

    var duplicateExists = ownerType == DocumentOwnerType.CertificationParticipant
        ? await _db.Documents.AnyAsync(x => x.CertificationParticipantId == ownerId && x.DocumentTypeId == source.Document.DocumentTypeId, ct)
        : await _db.Documents.AnyAsync(x => x.CertificationParticipantLahanId == ownerId && x.DocumentTypeId == source.Document.DocumentTypeId, ct);
    if (duplicateExists)
      throw new DocumentConflictException("Pemilik sudah memiliki jenis dokumen ini. Ganti versi dokumen yang sudah ada.");

    var now = DateTimeOffset.UtcNow;
    var document = new DocumentRecord {
      Id = Guid.NewGuid(), DocumentTypeId = source.Document.DocumentTypeId,
      CertificationParticipantId = ownerType == DocumentOwnerType.CertificationParticipant ? ownerId : null,
      CertificationParticipantLahanId = ownerType == DocumentOwnerType.CertificationParticipantLahan ? ownerId : null,
      Catatan = source.Document.Catatan, UploadedByUserId = userId, UploadedAt = now, Status = DocumentStatus.Draft
    };
    document.Versions.Add(new DocumentVersion {
      Id = Guid.NewGuid(), DocumentId = document.Id, VersionNumber = 1,
      StorageKey = source.StorageKey, OriginalFileName = source.OriginalFileName,
      ContentType = source.ContentType, FileExtension = source.FileExtension,
      FileSize = source.FileSize, Sha256Hash = source.Sha256Hash,
      UploadedByUserId = userId, UploadedAt = now
    });
    _db.Documents.Add(document);
    await _db.SaveChangesAsync(ct);
    return document;
  }

  private static DocumentVersion CreateVersion(
      Guid documentId,
      int versionNumber,
      string storageKey,
      ValidatedFile file,
      Guid userId,
      DateTimeOffset now) {
    return new DocumentVersion {
      Id = Guid.NewGuid(),
      DocumentId = documentId,
      VersionNumber = versionNumber,
      StorageKey = storageKey,
      OriginalFileName = file.OriginalFileName,
      ContentType = file.ContentType,
      FileExtension = file.Extension,
      FileSize = file.Size,
      Sha256Hash = file.Sha256Hash,
      UploadedByUserId = userId,
      UploadedAt = now
    };
  }
}

public class DocumentOperationException : Exception {
  public DocumentOperationException(string message) : base(message) { }
}

public sealed class DocumentConflictException : DocumentOperationException {
  public DocumentConflictException(string message) : base(message) { }
}
