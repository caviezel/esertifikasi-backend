using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models.Documents;
using Esertifikasi.Api.Security;
using Esertifikasi.Api.Services.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api.Controllers;

[ApiController, Route("api/documents"), Authorize]
public sealed class DocumentController : ControllerBase {
  private readonly AppDbContext _db;
  private readonly DocumentAccessService _access;
  private readonly DocumentUploadService _uploads;
  private readonly IFileStorage _storage;

  public DocumentController(
      AppDbContext db,
      DocumentAccessService access,
      DocumentUploadService uploads,
      IFileStorage storage) {
    _db = db;
    _access = access;
    _uploads = uploads;
    _storage = storage;
  }

  [HttpGet("{id:guid}")]
  public async Task<ActionResult<DocumentListItem>> Get(Guid id, CancellationToken ct) {
    if (!await _access.CanViewAsync(User, id, ct)) return Forbid();
    var document = await _db.Documents.Where(x => x.Id == id).ToListItems().SingleOrDefaultAsync(ct);
    return document is null ? NotFound() : Ok(document);
  }

  [HttpGet("{id:guid}/versions")]
  public async Task<IActionResult> Versions(Guid id, CancellationToken ct) {
    if (!await _access.CanViewAsync(User, id, ct)) return Forbid();
    var versions = await _db.DocumentVersions.Where(x => x.DocumentId == id)
        .OrderByDescending(x => x.VersionNumber)
        .Select(x => new DocumentVersionResponse(x.Id, x.VersionNumber, x.OriginalFileName, x.ContentType, x.FileSize, x.Sha256Hash, x.UploadedByUserId, x.UploadedAt))
        .ToListAsync(ct);
    return Ok(versions);
  }

  [HttpGet("{id:guid}/download")]
  public async Task<IActionResult> Download(Guid id, CancellationToken ct) {
    if (!await _access.CanViewAsync(User, id, ct)) return Forbid();
    var version = await LatestVersionAsync(id, ct);
    return version is null ? NotFound() : await FileResultAsync(version, download: true, ct);
  }

  [HttpGet("{id:guid}/preview")]
  public async Task<IActionResult> Preview(Guid id, CancellationToken ct) {
    if (!await _access.CanViewAsync(User, id, ct)) return Forbid();
    var version = await LatestVersionAsync(id, ct);
    return version is null ? NotFound() : await FileResultAsync(version, download: false, ct);
  }

  [HttpGet("{id:guid}/versions/{versionId:guid}/download")]
  public async Task<IActionResult> DownloadVersion(Guid id, Guid versionId, CancellationToken ct) {
    if (!await _access.CanViewAsync(User, id, ct)) return Forbid();
    var version = await _db.DocumentVersions.SingleOrDefaultAsync(x => x.Id == versionId && x.DocumentId == id, ct);
    return version is null ? NotFound() : await FileResultAsync(version, download: true, ct);
  }

  [HttpPost("{id:guid}/versions"), RequestSizeLimit(10 * 1024 * 1024 + 1024 * 1024)]
  public async Task<IActionResult> AddVersion(Guid id, [FromForm] UploadDocumentVersionRequest request, CancellationToken ct) {
    if (!await _access.CanManageAsync(User, id, ct)) return Forbid();
    await _access.EnsureCertificationDocumentMutableAsync(id, ct);
    var userId = AccessService.UserId(User);
    if (userId is null) return Unauthorized();
    var document = await _db.Documents.SingleOrDefaultAsync(x => x.Id == id, ct);
    if (document is null) return NotFound();

    var version = await _uploads.AddVersionAsync(document, request, userId.Value, ct);
    return Created($"/api/documents/{id}/versions/{version.Id}", new { version.Id, version.VersionNumber });
  }

  [HttpPost("{id:guid}/submit")]
  public async Task<IActionResult> Submit(Guid id, CancellationToken ct) {
    if (!await _access.CanManageAsync(User, id, ct)) return Forbid();
    await _access.EnsureCertificationDocumentMutableAsync(id, ct);
    var document = await _db.Documents.SingleOrDefaultAsync(x => x.Id == id, ct);
    if (document is null) return NotFound();
    if (document.Status is not (DocumentStatus.Draft or DocumentStatus.Rejected)) {
      return Conflict(new { message = "Hanya dokumen berstatus Draft atau Rejected yang dapat diajukan." });
    }

    document.Status = DocumentStatus.Submitted;
    document.RejectionReason = null;
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  [HttpPost("{id:guid}/verify")]
  public async Task<IActionResult> Verify(Guid id, CancellationToken ct) {
    if (!await _access.CanReviewAsync(User, id, ct)) return Forbid();
    await _access.EnsureCertificationDocumentMutableAsync(id, ct);
    var document = await _db.Documents.SingleOrDefaultAsync(x => x.Id == id, ct);
    if (document is null) return NotFound();
    if (document.Status != DocumentStatus.Submitted) {
      return Conflict(new { message = "Hanya dokumen berstatus Submitted yang dapat diverifikasi." });
    }

    document.Status = DocumentStatus.Verified;
    document.ReviewedByUserId = AccessService.UserId(User);
    document.ReviewedAt = DateTimeOffset.UtcNow;
    document.RejectionReason = null;
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  [HttpPost("{id:guid}/reject")]
  public async Task<IActionResult> Reject(Guid id, RejectDocumentRequest request, CancellationToken ct) {
    if (!await _access.CanReviewAsync(User, id, ct)) return Forbid();
    await _access.EnsureCertificationDocumentMutableAsync(id, ct);
    var document = await _db.Documents.SingleOrDefaultAsync(x => x.Id == id, ct);
    if (document is null) return NotFound();
    if (document.Status != DocumentStatus.Submitted) {
      return Conflict(new { message = "Hanya dokumen berstatus Submitted yang dapat ditolak." });
    }

    document.Status = DocumentStatus.Rejected;
    document.ReviewedByUserId = AccessService.UserId(User);
    document.ReviewedAt = DateTimeOffset.UtcNow;
    document.RejectionReason = request.Reason.Trim();
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  [HttpDelete("{id:guid}")]
  public async Task<IActionResult> Delete(Guid id, CancellationToken ct) {
    if (!await _access.CanManageAsync(User, id, ct)) return Forbid();
    await _access.EnsureCertificationDocumentMutableAsync(id, ct);
    var document = await _db.Documents.SingleOrDefaultAsync(x => x.Id == id, ct);
    if (document is null) return NotFound();
    if (await _db.AssociationDocumentSubmissions.AnyAsync(x => x.DocumentId == id, ct)) {
      return Conflict(new { message = "Dokumen tidak dapat dihapus karena versinya digunakan dalam pengajuan sertifikasi." });
    }
    document.IsDeleted = true;
    document.DeletedAt = DateTimeOffset.UtcNow;
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  private async Task<DocumentVersion?> LatestVersionAsync(Guid documentId, CancellationToken ct) {
    return await _db.DocumentVersions.Where(x => x.DocumentId == documentId)
        .OrderByDescending(x => x.VersionNumber)
        .FirstOrDefaultAsync(ct);
  }

  private async Task<IActionResult> FileResultAsync(DocumentVersion version, bool download, CancellationToken ct) {
    var stored = await _storage.OpenReadAsync(version.StorageKey, ct);
    if (stored is null) return NotFound(new { message = "Berkas tersimpan tidak ditemukan." });
    return download
        ? File(stored.Content, version.ContentType, version.OriginalFileName, enableRangeProcessing: true)
        : File(stored.Content, version.ContentType, enableRangeProcessing: true);
  }
}
