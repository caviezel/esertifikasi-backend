using System.ComponentModel.DataAnnotations;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models;
using Esertifikasi.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Esertifikasi.Api.Services;

namespace Esertifikasi.Api.Controllers;

[ApiController, Route("api/petani"), Authorize]
public sealed class PetaniController : ControllerBase {
  private readonly AppDbContext _db;
  private readonly AccessService _access;
  private readonly AdministrativeRegionService _regions;

  public PetaniController(AppDbContext db, AccessService access, AdministrativeRegionService regions) {
    _db = db;
    _access = access;
    _regions = regions;
  }

  [HttpGet]
  public async Task<ActionResult<PagedResult<PetaniListItem>>> GetAll(
      [FromQuery] PetaniQuery request,
      CancellationToken ct) {
    var query = _access.AccessiblePetani(User);

    if (request.AssociationId is not null) {
      query = query.Where(x => x.Poktan.AssociationId == request.AssociationId);
    }

    if (request.PoktanId is not null) {
      query = query.Where(x => x.PoktanId == request.PoktanId);
    }

    if (!string.IsNullOrWhiteSpace(request.Search)) {
      var search = request.Search.Trim();
      query = query.Where(x =>
          EF.Functions.ILike(x.Nama, $"%{search}%")
          || (x.Nik != null && EF.Functions.ILike(x.Nik, $"%{search}%"))
          || (x.Email != null && EF.Functions.ILike(x.Email, $"%{search}%"))
          || (x.NoTelepon != null && EF.Functions.ILike(x.NoTelepon, $"%{search}%"))
          || (x.Desa != null && (EF.Functions.ILike(x.Desa.Name, $"%{search}%")
              || EF.Functions.ILike(x.Desa.District.Name, $"%{search}%")
              || EF.Functions.ILike(x.Desa.District.Regency.Name, $"%{search}%")
              || EF.Functions.ILike(x.Desa.District.Regency.Province.Name, $"%{search}%"))));
    }

    query = (request.SortBy?.ToLowerInvariant(), request.Descending) switch {
      ("nik", false) => query.OrderBy(x => x.Nik),
      ("nik", true) => query.OrderByDescending(x => x.Nik),
      ("poktan", false) => query.OrderBy(x => x.Poktan.Nama).ThenBy(x => x.Nama),
      ("poktan", true) => query.OrderByDescending(x => x.Poktan.Nama).ThenByDescending(x => x.Nama),
      ("id", false) => query.OrderBy(x => x.Id),
      ("id", true) => query.OrderByDescending(x => x.Id),
      (_, true) => query.OrderByDescending(x => x.Nama),
      _ => query.OrderBy(x => x.Nama)
    };

    var result = await query
        .Select(x => new PetaniListItem(
            x.Id,
            x.PoktanId,
            x.Poktan.Nama,
            x.Poktan.AssociationId,
            x.Poktan.Association.Nama,
            x.Nama,
            x.Nik,
            x.Email,
            x.NoTelepon,
            x.DesaId,
            x.Desa != null ? x.Desa.Name : null,
            x.Desa != null ? x.Desa.District.Name : null,
            x.Desa != null ? x.Desa.District.Regency.Name : null,
            x.Desa != null ? x.Desa.District.Regency.Province.Name : null))
        .ToPagedResultAsync(request, ct);

    return Ok(result);
  }

  [HttpGet("me")]
  public async Task<IActionResult> Me(CancellationToken ct) {
    var userId = AccessService.UserId(User);
    if (userId is null) {
      return Unauthorized();
    }

    var row = await _db.Petani
        .Where(x => x.ApplicationUserId == userId)
        .Select(x => new { x.Id, x.PoktanId, x.Nama, x.Nik, x.Email, x.NoTelepon, x.Alamat })
        .SingleOrDefaultAsync(ct);

    return row is null ? NotFound() : Ok(row);
  }

  [HttpGet("by-poktan/{poktanId:guid}")]
  public async Task<IActionResult> GetByPoktan(Guid poktanId, CancellationToken ct) {
    if (!await _access.CanManagePoktanAsync(User, poktanId, ct)) {
      return Forbid();
    }

    var rows = await _db.Petani
        .Where(x => x.PoktanId == poktanId)
        .Select(x => new { x.Id, x.PoktanId, x.Nama, x.Nik })
        .ToListAsync(ct);

    return Ok(rows);
  }

  [HttpGet("{id:guid}")]
  public async Task<ActionResult<PetaniResponse>> Get(Guid id, CancellationToken ct) {
    if (!await _access.CanViewPetaniAsync(User, id, ct)) {
      return Forbid();
    }

    var entity = await _db.Petani.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);

    return entity is null ? NotFound() : Ok(PetaniResponse.From(entity));
  }

  [HttpGet("{petaniId:guid}/summary")]
  public async Task<ActionResult<PetaniSummaryResponse>> GetSummary(
      Guid petaniId,
      CancellationToken ct) {
    if (!await _access.CanViewPetaniAsync(User, petaniId, ct)) {
      return Forbid();
    }

    var summary = await _db.Petani
        .Where(x => x.Id == petaniId)
        .Select(x => new PetaniSummaryResponse(
            x.Lahan.Count(),
            x.Lahan.Sum(lahan => lahan.LuasLegalitas) ?? 0m))
        .SingleOrDefaultAsync(ct);

    return summary is null ? NotFound() : Ok(summary);
  }

  [HttpPost]
  public async Task<IActionResult> Create(PetaniRequest request, CancellationToken ct) {
    if (!await _access.CanManagePoktanAsync(User, request.PoktanId, ct)) {
      return Forbid();
    }

    if (!await _db.Poktan.AnyAsync(x => x.Id == request.PoktanId, ct)) {
      return ValidationProblem(new ValidationProblemDetails {
        Errors = { [nameof(request.PoktanId)] = new[] { "Poktan tidak ditemukan." } }
      });
    }

    if (request.DesaId is not null && !await _regions.IsActiveVillageAsync(request.DesaId.Value, ct)) {
      return ValidationProblem(new ValidationProblemDetails { Errors = { [nameof(request.DesaId)] = new[] { "DesaId tidak ditemukan atau tidak aktif." } } });
    }

    if (request.Nik is not null && await _db.Petani.AnyAsync(x => x.Nik == request.Nik, ct)) {
      return ValidationProblem(new ValidationProblemDetails {
        Errors = { [nameof(request.Nik)] = new[] { "NIK sudah terdaftar." } }
      });
    }

    var entity = new Petani { Id = Guid.NewGuid() };
    request.ApplyTo(entity);

    _db.Petani.Add(entity);
    await _db.SaveChangesAsync(ct);

    return Created($"/api/petani/{entity.Id}", new { entity.Id });
  }

  [HttpPut("{id:guid}")]
  public async Task<IActionResult> Update(Guid id, PetaniRequest request, CancellationToken ct) {
    var entity = await _db.Petani.SingleOrDefaultAsync(x => x.Id == id, ct);
    if (entity is null) {
      return NotFound();
    }

    if (!await _access.CanManagePoktanAsync(User, entity.PoktanId, ct)) {
      return Forbid();
    }

    if (entity.PoktanId != request.PoktanId
        && !await _access.CanManagePoktanAsync(User, request.PoktanId, ct)) {
      return Forbid();
    }

    if (!await _db.Poktan.AnyAsync(x => x.Id == request.PoktanId, ct)) {
      return ValidationProblem(new ValidationProblemDetails {
        Errors = { [nameof(request.PoktanId)] = new[] { "Poktan tidak ditemukan." } }
      });
    }


    if (request.DesaId is not null && !await _regions.IsActiveVillageAsync(request.DesaId.Value, ct)) {
      return ValidationProblem(new ValidationProblemDetails { Errors = { [nameof(request.DesaId)] = new[] { "DesaId tidak ditemukan atau tidak aktif." } } });
    }

    if (request.Nik is not null && await _db.Petani.AnyAsync(x => x.Id != id && x.Nik == request.Nik, ct)) {
      return ValidationProblem(new ValidationProblemDetails {
        Errors = { [nameof(request.Nik)] = new[] { "NIK sudah terdaftar." } }
      });
    }

    request.ApplyTo(entity);
    await _db.SaveChangesAsync(ct);

    return NoContent();
  }

  [HttpDelete("{id:guid}")]
  public async Task<IActionResult> Delete(Guid id, CancellationToken ct) {
    var entity = await _db.Petani.SingleOrDefaultAsync(x => x.Id == id, ct);
    if (entity is null) {
      return NotFound();
    }

    if (!await _access.CanManagePoktanAsync(User, entity.PoktanId, ct)) {
      return Forbid();
    }

    entity.IsDeleted = true;
    entity.DeletedAt = DateTimeOffset.UtcNow;
    await _db.SaveChangesAsync(ct);

    return NoContent();
  }
}

public class PetaniRequest {
  [NotEmptyGuid]
  public Guid PoktanId { get; set; }
  [Required, StringLength(255)] public string Nama { get; set; } = string.Empty;
  [Required, RegularExpression(@"^\d{16}$")] public string? Nik { get; set; }
  [Required, StringLength(255)] public string? TempatLahir { get; set; }
  [Required] public DateOnly? TanggalLahir { get; set; }
  [Required] public Gender? JenisKelamin { get; set; }
  [Required, Range(1, long.MaxValue)] public long? DesaId { get; set; }
  [Required, StringLength(20)] public string? RtRw { get; set; }
  [Required, StringLength(1000)] public string? Alamat { get; set; }
  [Required] public MaritalStatus? StatusPerkawinan { get; set; }
  [Required, StringLength(255)] public string? Pekerjaan { get; set; }
  [Required] public Citizenship? Kewarganegaraan { get; set; }
  [Required, StringLength(100)] public string? Suku { get; set; }
  [EmailAddress, StringLength(255)] public string? Email { get; set; }
  [Required, StringLength(30), RegularExpression(@"^[0-9+() .-]*$")] public string? NoTelepon { get; set; }
  [Required, RegularExpression(@"^\d{16}$")] public string? NoKk { get; set; }
  [Required, StringLength(255)] public string? NamaKepalaKeluarga { get; set; }
  [Required, Range(0, int.MaxValue)] public int? JumlahAnggotaKeluarga { get; set; }
  [Required, Range(0, int.MaxValue)] public int? JumlahAnak { get; set; }
  [StringLength(255)] public string? PekerjaanSampingan { get; set; }
  [Required] public EducationLevel? PendidikanTerakhir { get; set; }

  public void ApplyTo(Petani entity) {
    entity.PoktanId = PoktanId;
    entity.Nama = Nama.Trim();
    entity.Nik = Nik;
    entity.TempatLahir = TempatLahir!.Trim();
    entity.TanggalLahir = TanggalLahir!.Value;
    entity.JenisKelamin = JenisKelamin!.Value;
    entity.DesaId = DesaId;
    entity.RtRw = RtRw!.Trim();
    entity.Alamat = Alamat!.Trim();
    entity.StatusPerkawinan = StatusPerkawinan!.Value;
    entity.Pekerjaan = Pekerjaan!.Trim();
    entity.Kewarganegaraan = Kewarganegaraan!.Value;
    entity.Suku = Suku!.Trim();
    entity.Email = Email;
    entity.NoTelepon = NoTelepon!;
    entity.NoKk = NoKk!;
    entity.NamaKepalaKeluarga = NamaKepalaKeluarga!.Trim();
    entity.JumlahAnggotaKeluarga = JumlahAnggotaKeluarga!.Value;
    entity.JumlahAnak = JumlahAnak!.Value;
    entity.PekerjaanSampingan = PekerjaanSampingan;
    entity.PendidikanTerakhir = PendidikanTerakhir!.Value;
  }
}

public sealed class PetaniResponse : PetaniRequest {
  public Guid Id { get; init; }

  public static PetaniResponse From(Petani entity) {
    return new PetaniResponse {
      Id = entity.Id,
      PoktanId = entity.PoktanId,
      Nama = entity.Nama,
      Nik = entity.Nik,
      TempatLahir = entity.TempatLahir,
      TanggalLahir = entity.TanggalLahir,
      JenisKelamin = entity.JenisKelamin,
      DesaId = entity.DesaId,
      RtRw = entity.RtRw,
      Alamat = entity.Alamat,
      StatusPerkawinan = entity.StatusPerkawinan,
      Pekerjaan = entity.Pekerjaan,
      Kewarganegaraan = entity.Kewarganegaraan,
      Suku = entity.Suku,
      Email = entity.Email,
      NoTelepon = entity.NoTelepon,
      NoKk = entity.NoKk,
      NamaKepalaKeluarga = entity.NamaKepalaKeluarga,
      JumlahAnggotaKeluarga = entity.JumlahAnggotaKeluarga,
      JumlahAnak = entity.JumlahAnak,
      PekerjaanSampingan = entity.PekerjaanSampingan,
      PendidikanTerakhir = entity.PendidikanTerakhir
    };
  }
}

public sealed record PetaniListItem(
    Guid Id,
    Guid PoktanId,
    string PoktanNama,
    Guid AssociationId,
    string AssociationNama,
    string Nama,
    string? Nik,
    string? Email,
    string? NoTelepon,
    long? DesaId,
    string? DesaName,
    string? DistrictName,
    string? RegencyName,
    string? ProvinceName);

public sealed class PetaniQuery : PagedQuery {
  public Guid? AssociationId { get; set; }
  public Guid? PoktanId { get; set; }
}
