using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Security;
using Esertifikasi.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Esertifikasi.Api.Controllers;
[ApiController, Authorize, Route("api/associations/{associationId:guid}/operations/context")]
public sealed class OperationsContextController(AppDbContext db, AccessService access, OperationsAccess scope, CertificationProgressService progress) : ControllerBase {
  [HttpGet]
  public async Task<IActionResult> Get(Guid associationId, CancellationToken ct) {
    if (!await scope.ViewAssociation(User, associationId, ct)) return Forbid(); var association = await db.Associations.Where(x => x.Id == associationId).Select(x => new { x.Id, x.Nama }).SingleOrDefaultAsync(ct); if (association is null) return NotFound();
    var cycle = await db.CertificationCycles.Where(x => x.AssociationId == associationId && x.IsCurrent).Select(x => new { x.Id, x.CurrentPhase, x.Status }).SingleOrDefaultAsync(ct);
    var userId = AccessService.UserId(User); var manageAssociation = await access.CanManageAssociationAsync(User, associationId, ct);
    var poktans = await db.Poktan.Where(x => x.AssociationId == associationId && (manageAssociation || x.AdminAssignments.Any(a => a.UserId == userId) || db.IcsAuditorAssignments.Any(a => a.UserId == userId && a.PoktanId == x.Id) || x.Petani.Any(p => p.ApplicationUserId == userId))).Select(x => new { x.Id, x.Nama, CanManage = manageAssociation || x.AdminAssignments.Any(a => a.UserId == userId) || db.IcsAuditorAssignments.Any(a => a.UserId == userId && a.PoktanId == x.Id), CanFinalize = manageAssociation || x.AdminAssignments.Any(a => a.UserId == userId) }).ToListAsync(ct);
    return Ok(new { Association = association, CurrentCycle = cycle, Progress = cycle == null ? null : await progress.ComputeAsync(cycle.Id, null, ct), CanManageAssociationTraining = manageAssociation, CanReopen = manageAssociation, Poktans = poktans });
  }
}
