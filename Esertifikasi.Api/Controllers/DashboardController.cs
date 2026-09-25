using Esertifikasi.Api.Models;
using Esertifikasi.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api.Controllers;

[ApiController, Route("api/dashboard"), Authorize]
public sealed class DashboardController : ControllerBase {
  private readonly AccessService _access;

  public DashboardController(AccessService access) {
    _access = access;
  }

  [HttpGet("summary")]
  public async Task<ActionResult<DashboardSummaryResponse>> GetSummary(CancellationToken ct) {
    var totalAssociations = await _access.AccessibleAssociations(User).CountAsync(ct);
    var totalPoktan = await _access.AccessiblePoktan(User).CountAsync(ct);
    var totalPetani = await _access.AccessiblePetani(User).CountAsync(ct);
    var accessibleLahan = _access.AccessibleLahan(User);
    var totalLahan = await accessibleLahan.CountAsync(ct);
    var totalLuasLahan = await accessibleLahan.SumAsync(x => x.LuasLegalitas, ct) ?? 0m;

    return Ok(new DashboardSummaryResponse(
        totalAssociations,
        totalPoktan,
        totalPetani,
        totalLahan,
        totalLuasLahan));
  }
}
