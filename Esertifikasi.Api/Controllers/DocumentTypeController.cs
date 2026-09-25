using System.ComponentModel.DataAnnotations;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api.Controllers;

[ApiController, Route("api/document-types"), Authorize]
public sealed class DocumentTypeController : ControllerBase {
  private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase) {
    ".pdf", ".jpg", ".jpeg", ".png"
  };
  private readonly AppDbContext _db;

  public DocumentTypeController(AppDbContext db) {
    _db = db;
  }

  [HttpGet]
  public async Task<IActionResult> Get([FromQuery] DocumentOwnerType? ownerType, CancellationToken ct) {
    var query = _db.DocumentTypes.Where(x => x.IsActive);
    if (ownerType is not null) query = query.Where(x => x.OwnerType == ownerType);
    var rows = await query.OrderBy(x => x.OwnerType).ThenBy(x => x.Nama).ToListAsync(ct);
    return Ok(rows.Select(ToResponse));
  }

  [HttpGet("{id:guid}")]
  public async Task<IActionResult> GetById(Guid id, CancellationToken ct) {
    var entity = await _db.DocumentTypes.SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
    return entity is null ? NotFound() : Ok(ToResponse(entity));
  }

  [HttpPost, Authorize(Roles = AppRoles.SuperAdmin)]
  public async Task<IActionResult> Create(DocumentTypeRequest request, CancellationToken ct) {
    var extensions = NormalizeExtensions(request.AllowedExtensions);
    if (!extensions.All(SupportedExtensions.Contains)) {
      return ValidationProblem(UnsupportedExtensionProblem());
    }

    var code = request.Code.Trim().ToUpperInvariant();
    if (await _db.DocumentTypes.AnyAsync(
        x => x.IsActive && x.OwnerType == request.OwnerType && x.Code == code,
        ct)) {
      return Conflict(new { message = "Kode jenis dokumen sudah digunakan untuk tipe pemilik yang dipilih." });
    }

    var entity = new DocumentType {
      Id = Guid.NewGuid(),
      Code = code,
      Nama = request.Nama.Trim(),
      OwnerType = request.OwnerType,
      IsRequired = request.IsRequired,
      AllowedExtensions = string.Join(',', extensions),
      MaximumFileSize = request.MaximumFileSize,
      IsActive = true
    };
    _db.DocumentTypes.Add(entity);
    await _db.SaveChangesAsync(ct);
    return Created($"/api/document-types/{entity.Id}", ToResponse(entity));
  }

  [HttpPut("{id:guid}"), Authorize(Roles = AppRoles.SuperAdmin)]
  public async Task<IActionResult> Update(Guid id, DocumentTypeRequest request, CancellationToken ct) {
    var entity = await _db.DocumentTypes.SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
    if (entity is null) return NotFound();
    if (entity.OwnerType != request.OwnerType && await _db.Documents.AnyAsync(x => x.DocumentTypeId == id, ct)) {
      return Conflict(new { message = "Tipe pemilik tidak dapat diubah setelah dokumen diunggah." });
    }

    var extensions = NormalizeExtensions(request.AllowedExtensions);
    if (!extensions.All(SupportedExtensions.Contains)) {
      return ValidationProblem(UnsupportedExtensionProblem());
    }

    var code = request.Code.Trim().ToUpperInvariant();
    if (await _db.DocumentTypes.AnyAsync(
        x => x.IsActive && x.Id != id && x.OwnerType == request.OwnerType && x.Code == code,
        ct)) {
      return Conflict(new { message = "Kode jenis dokumen sudah digunakan untuk tipe pemilik yang dipilih." });
    }

    entity.Code = code;
    entity.Nama = request.Nama.Trim();
    entity.OwnerType = request.OwnerType;
    entity.IsRequired = request.IsRequired;
    entity.AllowedExtensions = string.Join(',', extensions);
    entity.MaximumFileSize = request.MaximumFileSize;
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  [HttpDelete("{id:guid}"), Authorize(Roles = AppRoles.SuperAdmin)]
  public async Task<IActionResult> Delete(Guid id, CancellationToken ct) {
    var entity = await _db.DocumentTypes.SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
    if (entity is null) return NotFound();
    entity.IsActive = false;
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  private static DocumentTypeResponse ToResponse(DocumentType entity) {
    return new DocumentTypeResponse(
        entity.Id,
        entity.Code,
        entity.Nama,
        entity.OwnerType,
        entity.IsRequired,
        entity.AllowedExtensions.Split(','),
        entity.MaximumFileSize);
  }

  private static string[] NormalizeExtensions(IEnumerable<string> extensions) {
    return extensions.Select(x => x.Trim().ToLowerInvariant())
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => x.StartsWith('.') ? x : $".{x}")
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
  }

  private static ValidationProblemDetails UnsupportedExtensionProblem() {
    return new ValidationProblemDetails {
      Errors = { [nameof(DocumentTypeRequest.AllowedExtensions)] = new[] { "Hanya ekstensi PDF, JPG, JPEG, dan PNG yang didukung." } }
    };
  }
}

public sealed class DocumentTypeRequest {
  [Required, StringLength(100), RegularExpression("^[A-Za-z0-9_]+$")]
  public string Code { get; set; } = string.Empty;

  [Required, StringLength(255)]
  public string Nama { get; set; } = string.Empty;

  [EnumDataType(typeof(DocumentOwnerType))]
  public DocumentOwnerType OwnerType { get; set; }

  public bool IsRequired { get; set; }

  [Required, MinLength(1)]
  public string[] AllowedExtensions { get; set; } = Array.Empty<string>();

  [Range(1, 10 * 1024 * 1024)]
  public long MaximumFileSize { get; set; } = 10 * 1024 * 1024;
}
