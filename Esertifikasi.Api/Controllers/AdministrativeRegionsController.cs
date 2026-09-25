using Esertifikasi.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api.Controllers;

[ApiController, Route("api/regions"), Authorize]
public sealed class AdministrativeRegionsController : ControllerBase {
  private readonly AppDbContext _db;

  public AdministrativeRegionsController(AppDbContext db) => _db = db;

  [HttpGet("provinces")]
  public async Task<IActionResult> Provinces(CancellationToken ct) => Ok(await _db.Provinces
      .Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new RegionItem(x.Id, x.Code, x.Name)).ToListAsync(ct));

  [HttpGet("provinces/{provinceId:long}/regencies")]
  public async Task<IActionResult> Regencies(long provinceId, CancellationToken ct) => Ok(await _db.Regencies
      .Where(x => x.ProvinceId == provinceId && x.IsActive).OrderBy(x => x.Name)
      .Select(x => new RegionItem(x.Id, x.Code, x.Name)).ToListAsync(ct));

  [HttpGet("regencies/{regencyId:long}/districts")]
  public async Task<IActionResult> Districts(long regencyId, CancellationToken ct) => Ok(await _db.Districts
      .Where(x => x.RegencyId == regencyId && x.IsActive).OrderBy(x => x.Name)
      .Select(x => new RegionItem(x.Id, x.Code, x.Name)).ToListAsync(ct));

  [HttpGet("districts/{districtId:long}/villages")]
  public async Task<IActionResult> Villages(long districtId, CancellationToken ct) => Ok(await _db.Villages
      .Where(x => x.DistrictId == districtId && x.IsActive).OrderBy(x => x.Name)
      .Select(x => new RegionItem(x.Id, x.Code, x.Name)).ToListAsync(ct));

  [HttpGet("villages/{villageId:long}")]
  public async Task<IActionResult> Village(long villageId, CancellationToken ct) {
    var row = await _db.Villages.Where(x => x.Id == villageId).Select(x => new VillageResponse(
        x.Id, x.Code, x.Name, x.DistrictId, x.District.Code, x.District.Name,
        x.District.RegencyId, x.District.Regency.Code, x.District.Regency.Name,
        x.District.Regency.ProvinceId, x.District.Regency.Province.Code, x.District.Regency.Province.Name, x.IsActive,
        x.SourceUpdatedAt, x.SyncedAt)).SingleOrDefaultAsync(ct);
    return row is null ? NotFound() : Ok(row);
  }
}
