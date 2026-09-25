using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models;
using Esertifikasi.Api.Models.Documents;
using Esertifikasi.Api.Security;
using Esertifikasi.Api.Services.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api.Controllers;

[ApiController, Route("api/lahan/{lahanId:guid}/documents"), Authorize]
public sealed class LahanDocumentController : ControllerBase {
  private readonly AppDbContext _db;
  private readonly AccessService _access;
  private readonly DocumentAccessService _documentAccess;
  private readonly DocumentUploadService _uploads;

  public LahanDocumentController(
      AppDbContext db,
      AccessService access,
      DocumentAccessService documentAccess,
      DocumentUploadService uploads) {
    _db = db;
    _access = access;
    _documentAccess = documentAccess;
    _uploads = uploads;
  }

  [HttpGet("requirements")]
  public async Task<IActionResult> Requirements(Guid lahanId, CancellationToken ct) {
    var petaniId = await _db.Lahan.Where(x => x.Id == lahanId).Select(x => (Guid?)x.PetaniId).SingleOrDefaultAsync(ct);
    if (petaniId is null || !await _access.CanViewPetaniAsync(User, petaniId.Value, ct)) return Forbid();

    var rows = await _db.DocumentTypes.Where(x => x.OwnerType == DocumentOwnerType.Lahan && x.IsActive)
        .OrderBy(x => x.Nama)
        .Select(x => new { x.Id, x.Code, x.Nama, x.OwnerType, x.IsRequired, x.AllowedExtensions, x.MaximumFileSize })
        .ToListAsync(ct);
    var types = rows.Select(x => new DocumentTypeResponse(x.Id, x.Code, x.Nama, x.OwnerType, x.IsRequired, x.AllowedExtensions.Split(','), x.MaximumFileSize));
    return Ok(types);
  }

  [HttpGet]
  public async Task<ActionResult<PagedResult<DocumentListItem>>> Get(
      Guid lahanId,
      [FromQuery] DocumentQuery request,
      CancellationToken ct) {
    var petaniId = await _db.Lahan.Where(x => x.Id == lahanId).Select(x => (Guid?)x.PetaniId).SingleOrDefaultAsync(ct);
    if (petaniId is null || !await _access.CanViewPetaniAsync(User, petaniId.Value, ct)) return Forbid();

    var query = _db.Documents.Where(x => x.LahanId == lahanId);
    if (request.DocumentTypeId is not null) query = query.Where(x => x.DocumentTypeId == request.DocumentTypeId);
    if (request.Status is not null) query = query.Where(x => x.Status == request.Status);
    if (!string.IsNullOrWhiteSpace(request.Search)) {
      var search = request.Search.Trim();
      query = query.Where(x => EF.Functions.ILike(x.DocumentType.Nama, $"%{search}%") || EF.Functions.ILike(x.DocumentType.Code, $"%{search}%"));
    }

    query = request.Descending ? query.OrderByDescending(x => x.UploadedAt) : query.OrderBy(x => x.UploadedAt);
    var result = await query.ToListItems().ToPagedResultAsync(request, ct);
    return Ok(result);
  }

  [HttpGet("completeness")]
  public async Task<IActionResult> Completeness(Guid lahanId, CancellationToken ct) {
    var petaniId = await _db.Lahan.Where(x => x.Id == lahanId).Select(x => (Guid?)x.PetaniId).SingleOrDefaultAsync(ct);
    if (petaniId is null || !await _access.CanViewPetaniAsync(User, petaniId.Value, ct)) {
      return Forbid();
    }

    var requiredTypes = await _db.DocumentTypes
        .Where(x => x.OwnerType == DocumentOwnerType.Lahan && x.IsActive && x.IsRequired)
        .Select(x => new MissingDocumentType(x.Id, x.Code, x.Nama))
        .ToListAsync(ct);
    var documents = await _db.Documents
        .Where(x => x.LahanId == lahanId)
        .Select(x => new { x.DocumentTypeId, x.Status })
        .ToListAsync(ct);
    var requiredIds = requiredTypes.Select(x => x.Id).ToHashSet();
    var requiredDocuments = documents.Where(x => requiredIds.Contains(x.DocumentTypeId)).ToList();
    var uploadedIds = requiredDocuments.Select(x => x.DocumentTypeId).ToHashSet();
    var missing = requiredTypes.Where(x => !uploadedIds.Contains(x.Id)).ToList();
    var verified = requiredDocuments.Count(x => x.Status == DocumentStatus.Verified);
    var percentage = requiredTypes.Count == 0 ? 100 : Math.Round(verified * 100m / requiredTypes.Count, 2);

    return Ok(new DocumentCompletenessResponse(
        requiredTypes.Count,
        requiredDocuments.Count,
        verified,
        requiredDocuments.Count(x => x.Status == DocumentStatus.Rejected),
        percentage,
        missing));
  }

  [HttpPost, RequestSizeLimit(10 * 1024 * 1024 + 1024 * 1024)]
  public async Task<IActionResult> Upload(Guid lahanId, [FromForm] UploadDocumentRequest request, CancellationToken ct) {
    if (!await _documentAccess.CanManageLahanAsync(User, lahanId, ct)) return Forbid();
    var userId = AccessService.UserId(User);
    if (userId is null) return Unauthorized();

    var document = await _uploads.CreateAsync(DocumentOwnerType.Lahan, lahanId, request, userId.Value, ct);
    return Created($"/api/documents/{document.Id}", new { document.Id, document.Status });
  }
}
