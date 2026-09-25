using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models.Documents;
using Esertifikasi.Api.Security;
using Esertifikasi.Api.Services.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api.Controllers;

[ApiController, Route("api/associations/{associationId:guid}/documents"), Authorize]
public sealed class AssociationDocumentsController : ControllerBase {
  private readonly AppDbContext _db;
  private readonly AccessService _access;
  private readonly DocumentUploadService _uploads;

  public AssociationDocumentsController(AppDbContext db, AccessService access, DocumentUploadService uploads) {
    _db = db;
    _access = access;
    _uploads = uploads;
  }

  [HttpGet]
  public async Task<IActionResult> List(Guid associationId, CancellationToken ct) {
    if (!await _access.CanAccessAssociationAsync(User, associationId, ct)) return Forbid();
    if (!await _db.Associations.AnyAsync(x => x.Id == associationId, ct)) return NotFound();
    return Ok(await _db.Documents.Where(x => x.AssociationId == associationId)
        .OrderBy(x => x.DocumentType.Nama).ToListItems().ToListAsync(ct));
  }

  [HttpPost, RequestSizeLimit(10 * 1024 * 1024 + 1024 * 1024)]
  public async Task<IActionResult> Upload(Guid associationId, [FromForm] UploadDocumentRequest request, CancellationToken ct) {
    if (!await _access.CanManageAssociationAsync(User, associationId, ct)) return Forbid();
    if (!await _db.Associations.AnyAsync(x => x.Id == associationId, ct)) return NotFound();
    var userId = AccessService.UserId(User);
    if (userId is null) return Unauthorized();
    var document = await _uploads.CreateAsync(DocumentOwnerType.Association, associationId, request, userId.Value, ct);
    return Created($"/api/documents/{document.Id}", new { document.Id });
  }

  [HttpGet("completeness")]
  public async Task<IActionResult> Completeness(Guid associationId, CancellationToken ct) {
    if (!await _access.CanAccessAssociationAsync(User, associationId, ct)) return Forbid();
    if (!await _db.Associations.AnyAsync(x => x.Id == associationId, ct)) return NotFound();
    var required = await _db.DocumentTypes.Where(x => x.IsActive && x.IsRequired && x.OwnerType == DocumentOwnerType.Association)
        .OrderBy(x => x.Nama).Select(x => new { x.Id, x.Code, x.Nama }).ToListAsync(ct);
    var documents = await _db.Documents.Where(x => x.AssociationId == associationId && x.DocumentType.IsActive)
        .Select(x => new { x.DocumentTypeId, x.Status }).ToListAsync(ct);
    var uploadedIds = documents.Select(x => x.DocumentTypeId).ToHashSet();
    var requiredDocuments = documents.Where(x => required.Any(r => r.Id == x.DocumentTypeId)).ToList();
    var submitted = requiredDocuments.Count(x => x.Status is DocumentStatus.Submitted or DocumentStatus.Verified);
    var verified = requiredDocuments.Count(x => x.Status == DocumentStatus.Verified);
    var missing = required.Where(x => !uploadedIds.Contains(x.Id))
        .Select(x => new MissingDocumentType(x.Id, x.Code, x.Nama)).ToList();
    var percentage = required.Count == 0 ? 100m : Math.Round(100m * submitted / required.Count, 2);
    return Ok(new AssociationDocumentCompletenessResponse(required.Count, requiredDocuments.Count, submitted, verified,
        requiredDocuments.Count(x => x.Status == DocumentStatus.Rejected), percentage, submitted == required.Count, missing));
  }
}
