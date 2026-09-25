using System.ComponentModel.DataAnnotations;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models;
using Esertifikasi.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api.Controllers;

[ApiController, Route("api/poktan"), Authorize]
public sealed class PoktanController : ControllerBase {
  private readonly AppDbContext _db;
  private readonly AccessService _access;

  public PoktanController(AppDbContext db, AccessService access) {
    _db = db;
    _access = access;
  }

  [HttpGet]
  public async Task<ActionResult<PagedResult<PoktanResponse>>> GetAll(
      [FromQuery] PoktanQuery request,
      CancellationToken ct) {
    var query = _access.AccessiblePoktan(User);

    if (request.AssociationId is not null) {
      query = query.Where(x => x.AssociationId == request.AssociationId);
    }

    if (!string.IsNullOrWhiteSpace(request.Search)) {
      var search = request.Search.Trim();
      query = query.Where(x =>
          EF.Functions.ILike(x.Nama, $"%{search}%")
          || EF.Functions.ILike(x.Association.Nama, $"%{search}%"));
    }

    query = (request.SortBy?.ToLowerInvariant(), request.Descending) switch {
      ("association", false) => query.OrderBy(x => x.Association.Nama).ThenBy(x => x.Nama),
      ("association", true) => query.OrderByDescending(x => x.Association.Nama).ThenByDescending(x => x.Nama),
      ("id", false) => query.OrderBy(x => x.Id),
      ("id", true) => query.OrderByDescending(x => x.Id),
      (_, true) => query.OrderByDescending(x => x.Nama),
      _ => query.OrderBy(x => x.Nama)
    };

    var result = await query
        .Select(x => new PoktanResponse(x.Id, x.AssociationId, x.Association.Nama, x.Nama,
            x.JumlahPetaniPekebunSwadaya, x.BatasMaksimumLuasSawit, x.NomorKeanggotaanRspo,
            x.Negara, x.ProvinceId, x.RegencyId, x.LuasAreaSawit))
        .ToPagedResultAsync(request, ct);

    return Ok(result);
  }

  [HttpGet("by-association/{associationId:guid}")]
  public async Task<IActionResult> Get(Guid associationId, CancellationToken ct) {
    var rows = await _access.AccessiblePoktan(User)
        .Where(x => x.AssociationId == associationId)
        .Select(x => new PoktanResponse(x.Id, x.AssociationId, x.Association.Nama, x.Nama,
            x.JumlahPetaniPekebunSwadaya, x.BatasMaksimumLuasSawit, x.NomorKeanggotaanRspo,
            x.Negara, x.ProvinceId, x.RegencyId, x.LuasAreaSawit))
        .ToListAsync(ct);

    return Ok(rows);
  }

  [HttpGet("{id:guid}")]
  public async Task<ActionResult<PoktanResponse>> GetById(Guid id, CancellationToken ct) {
    var row = await _access.AccessiblePoktan(User)
        .Where(x => x.Id == id)
        .Select(x => new PoktanResponse(x.Id, x.AssociationId, x.Association.Nama, x.Nama,
            x.JumlahPetaniPekebunSwadaya, x.BatasMaksimumLuasSawit, x.NomorKeanggotaanRspo,
            x.Negara, x.ProvinceId, x.RegencyId, x.LuasAreaSawit))
        .SingleOrDefaultAsync(ct);

    return row is null ? NotFound() : Ok(row);
  }

  [HttpGet("{poktanId:guid}/summary")]
  public async Task<ActionResult<PoktanSummaryResponse>> GetSummary(
      Guid poktanId,
      CancellationToken ct) {
    if (!await _access.CanManagePoktanAsync(User, poktanId, ct)) {
      return Forbid();
    }

    var summary = await _db.Poktan
        .Where(x => x.Id == poktanId)
        .Select(x => new PoktanSummaryResponse(
            x.Petani.Count(),
            x.Petani.SelectMany(petani => petani.Lahan).Count(),
            x.Petani.SelectMany(petani => petani.Lahan)
                .Sum(lahan => lahan.LuasLegalitas) ?? 0m))
        .SingleOrDefaultAsync(ct);

    return summary is null ? NotFound() : Ok(summary);
  }

  [HttpPost]
  public async Task<IActionResult> Create(PoktanRequest request, CancellationToken ct) {
    if (!await _access.CanManageAssociationAsync(User, request.AssociationId, ct)) {
      return Forbid();
    }

    var association = await _db.Associations.FindAsync(new object[] { request.AssociationId }, ct);
    if (association is null) {
      return ValidationProblem(new ValidationProblemDetails {
        Errors = { [nameof(request.AssociationId)] = new[] { "Association tidak ditemukan." } }
      });
    }
    if (!await HasValidRegionHierarchyAsync(request, ct))
      return ValidationProblem("Provinsi atau kabupaten Poktan tidak ditemukan, tidak aktif, atau hierarkinya tidak sesuai.");

    if (await _db.Poktan.AnyAsync(x => x.AssociationId == request.AssociationId && x.Nama == request.Nama.Trim(), ct)) {
      return ValidationProblem(new ValidationProblemDetails {
        Errors = { [nameof(request.Nama)] = new[] { "Nama Poktan sudah digunakan dalam Association ini." } }
      });
    }

    var entity = new Poktan {
      Id = Guid.NewGuid(),
      AssociationId = request.AssociationId,
      Association = association,
      Nama = request.Nama.Trim()
    };
    request.ApplyTo(entity);

    _db.Poktan.Add(entity);
    await _db.SaveChangesAsync(ct);

    return Created($"/api/poktan/{entity.Id}", PoktanResponse.From(entity));
  }

  [HttpPut("{id:guid}")]
  public async Task<IActionResult> Update(Guid id, UpdatePoktanRequest request, CancellationToken ct) {
    if (!await _access.CanManagePoktanAsync(User, id, ct)) {
      return Forbid();
    }

    var entity = await _db.Poktan.FindAsync(new object[] { id }, ct);
    if (entity is null) {
      return NotFound();
    }
    if (!await HasValidRegionHierarchyAsync(request, ct))
      return ValidationProblem("Provinsi atau kabupaten Poktan tidak ditemukan, tidak aktif, atau hierarkinya tidak sesuai.");

    if (entity.AssociationId != request.AssociationId) {
      if (!await _access.CanManageAssociationAsync(User, entity.AssociationId, ct)
          || !await _access.CanManageAssociationAsync(User, request.AssociationId, ct)) {
        return Forbid();
      }

      if (!await _db.Associations.AnyAsync(x => x.Id == request.AssociationId, ct)) {
        return ValidationProblem(new ValidationProblemDetails {
          Errors = { [nameof(request.AssociationId)] = new[] { "Association tidak ditemukan." } }
        });
      }

      entity.AssociationId = request.AssociationId;
    }

    if (await _db.Poktan.AnyAsync(x => x.Id != id && x.AssociationId == request.AssociationId && x.Nama == request.Nama.Trim(), ct)) {
      return ValidationProblem(new ValidationProblemDetails {
        Errors = { [nameof(request.Nama)] = new[] { "Nama Poktan sudah digunakan dalam Association ini." } }
      });
    }

    request.ApplyTo(entity);
    await _db.SaveChangesAsync(ct);

    return NoContent();
  }

  private Task<bool> HasValidRegionHierarchyAsync(PoktanRequest request, CancellationToken ct) =>
      _db.Regencies.AnyAsync(x => x.Id == request.RegencyId && x.ProvinceId == request.ProvinceId
          && x.IsActive && x.Province.IsActive, ct);

  [HttpDelete("{id:guid}")]
  public async Task<IActionResult> Delete(Guid id, CancellationToken ct) {
    if (!await _access.CanManagePoktanAsync(User, id, ct)) {
      return Forbid();
    }

    var entity = await _db.Poktan.FindAsync(new object[] { id }, ct);
    if (entity is null) {
      return NotFound();
    }

    entity.IsDeleted = true;
    entity.DeletedAt = DateTimeOffset.UtcNow;
    await _db.SaveChangesAsync(ct);

    return NoContent();
  }
}

public class PoktanRequest {
  [NotEmptyGuid]
  public Guid AssociationId { get; set; }

  [Required, StringLength(255)]
  public string Nama { get; set; } = string.Empty;

  [Range(0, int.MaxValue)] public int? JumlahPetaniPekebunSwadaya { get; set; }
  [Range(typeof(decimal), "0", "9999999999999999")] public decimal? BatasMaksimumLuasSawit { get; set; }
  [StringLength(100)] public string? NomorKeanggotaanRspo { get; set; }
  [Required, StringLength(100)] public string Negara { get; set; } = "Indonesia";
  [Required, Range(1, long.MaxValue)] public long? ProvinceId { get; set; }
  [Required, Range(1, long.MaxValue)] public long? RegencyId { get; set; }
  [Range(typeof(decimal), "0", "9999999999999999")] public decimal? LuasAreaSawit { get; set; }

  public void ApplyTo(Poktan entity) {
    entity.AssociationId = AssociationId;
    entity.Nama = Nama.Trim();
    entity.JumlahPetaniPekebunSwadaya = JumlahPetaniPekebunSwadaya;
    entity.BatasMaksimumLuasSawit = BatasMaksimumLuasSawit;
    entity.NomorKeanggotaanRspo = NomorKeanggotaanRspo;
    entity.Negara = Negara.Trim();
    entity.ProvinceId = ProvinceId;
    entity.RegencyId = RegencyId;
    entity.LuasAreaSawit = LuasAreaSawit;
  }
}

public sealed class UpdatePoktanRequest : PoktanRequest { }

public sealed record PoktanResponse(Guid Id, Guid AssociationId, string AssociationNama, string Nama,
    int? JumlahPetaniPekebunSwadaya, decimal? BatasMaksimumLuasSawit,
    string? NomorKeanggotaanRspo, string Negara, long? ProvinceId, long? RegencyId,
    decimal? LuasAreaSawit) {
  public static PoktanResponse From(Poktan entity) => new(entity.Id, entity.AssociationId,
      entity.Association.Nama, entity.Nama, entity.JumlahPetaniPekebunSwadaya,
      entity.BatasMaksimumLuasSawit, entity.NomorKeanggotaanRspo, entity.Negara,
      entity.ProvinceId, entity.RegencyId, entity.LuasAreaSawit);
}

public sealed class PoktanQuery : PagedQuery {
  public Guid? AssociationId { get; set; }
}
