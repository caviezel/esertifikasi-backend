using System.Security.Claims;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Security;
using Microsoft.EntityFrameworkCore;
namespace Esertifikasi.Api.Services;
public sealed class OperationsAccess(AppDbContext db, AccessService access) {
  public IQueryable<Petani> Farmers(ClaimsPrincipal user) {
    var id = AccessService.UserId(user);
    return access.IsSuperAdmin(user) ? db.Petani : db.Petani.Where(x => x.ApplicationUserId == id || x.Poktan.AdminAssignments.Any(a => a.UserId == id) || x.Poktan.Association.AdminAssignments.Any(a => a.UserId == id) || db.IcsAuditorAssignments.Any(a => a.UserId == id && a.PoktanId == x.PoktanId));
  }
  public async Task<bool> ManagePoktan(ClaimsPrincipal user, Guid id, CancellationToken ct) => await access.CanManagePoktanAsync(user, id, ct) || await db.IcsAuditorAssignments.AnyAsync(x => x.UserId == AccessService.UserId(user) && x.PoktanId == id, ct);
  public async Task<bool> ViewAssociation(ClaimsPrincipal user, Guid id, CancellationToken ct) => await access.CanAccessAssociationAsync(user, id, ct) || await Farmers(user).AnyAsync(x => x.Poktan.AssociationId == id, ct);
  public static void Require(bool condition, string message) { if (!condition) throw new MonitoringValidationException(message); }
}
