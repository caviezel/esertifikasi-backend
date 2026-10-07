using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models;
using Esertifikasi.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Esertifikasi.Api.Services.OperationsAccess;
namespace Esertifikasi.Api.Controllers;
public sealed partial class FieldLogsController {
  [HttpGet("table")]
  public async Task<IActionResult> Table([FromQuery] FieldLogQuery r, CancellationToken ct) {
    Require(r.Activity.HasValue && r.Year.HasValue, "Activity dan Year wajib.");
    var farmers = scope.Farmers(User).Where(x => x.Poktan.AssociationId == r.AssociationId && (!r.PoktanId.HasValue || x.PoktanId == r.PoktanId)).Select(x => x.Id);
    var q = db.Lahan.Where(x => farmers.Contains(x.PetaniId) && (!r.LahanId.HasValue || x.Id == r.LahanId));
    if (!string.IsNullOrWhiteSpace(r.Search)) q = q.Where(x => x.Petani.Nama.Contains(r.Search) || x.NoLegalitas != null && x.NoLegalitas.Contains(r.Search));
    var page = await q.OrderBy(x => x.Petani.Nama).ThenBy(x => x.Id).Select(x => new { LahanId = x.Id, x.PetaniId, FarmerName = x.Petani.Nama, x.Petani.Nik, NoSertifikat = x.NoLegalitas, NoLahan = x.NoPetaLahan, AreaHa = x.LuasLegalitas }).ToPagedResultAsync(r, ct);
    var ids = page.Items.Select(x => x.LahanId).ToArray(); var logs = await db.Set<FieldLog>().Where(x => ids.Contains(x.LahanId) && x.Activity == r.Activity && x.ActivityDate.Year == r.Year && (!r.Status.HasValue || x.Status == r.Status) && (!r.RotationId.HasValue || x.RotationId == r.RotationId)).OrderByDescending(x => x.ActivityDate).ThenBy(x => x.SequenceNumber).ToListAsync(ct);
    return Ok(new { page.Page, page.PageSize, page.TotalCount, page.TotalPages, Items = page.Items.Select(land => new { Land = land, RecordCount = logs.Count(x => x.LahanId == land.LahanId), LatestActivityDate = logs.Where(x => x.LahanId == land.LahanId).Select(x => (DateOnly?)x.ActivityDate).FirstOrDefault(), Records = logs.Where(x => x.LahanId == land.LahanId) }) });
  }
}
