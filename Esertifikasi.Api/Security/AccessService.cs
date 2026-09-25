using System.Security.Claims;
using Esertifikasi.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api.Security;

public sealed class AccessService {
  private readonly AppDbContext _db;

  public AccessService(AppDbContext db) => _db = db;

  public static Guid? UserId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

  public bool IsSuperAdmin(ClaimsPrincipal user) => user.IsInRole(AppRoles.SuperAdmin);

  public IQueryable<Association> AccessibleAssociations(ClaimsPrincipal user) {
    if (IsSuperAdmin(user)) {
      return _db.Associations;
    }

    var userId = UserId(user);
    return userId is null
        ? _db.Associations.Where(x => false)
        : _db.Associations.Where(x =>
            x.AdminAssignments.Any(a => a.UserId == userId)
            || x.Poktan.Any(p => p.AdminAssignments.Any(a => a.UserId == userId)));
  }

  public IQueryable<Poktan> AccessiblePoktan(ClaimsPrincipal user) {
    if (IsSuperAdmin(user)) {
      return _db.Poktan;
    }

    var userId = UserId(user);
    return userId is null
        ? _db.Poktan.Where(x => false)
        : _db.Poktan.Where(x =>
            x.AdminAssignments.Any(a => a.UserId == userId)
            || x.Association.AdminAssignments.Any(a => a.UserId == userId));
  }

  public IQueryable<Petani> AccessiblePetani(ClaimsPrincipal user) {
    if (IsSuperAdmin(user)) {
      return _db.Petani;
    }

    var userId = UserId(user);
    return userId is null
        ? _db.Petani.Where(x => false)
        : _db.Petani.Where(x =>
            x.ApplicationUserId == userId
            || x.Poktan.AdminAssignments.Any(a => a.UserId == userId)
            || x.Poktan.Association.AdminAssignments.Any(a => a.UserId == userId));
  }

  public IQueryable<Lahan> AccessibleLahan(ClaimsPrincipal user) {
    if (IsSuperAdmin(user)) {
      return _db.Lahan;
    }

    var userId = UserId(user);
    return userId is null
        ? _db.Lahan.Where(x => false)
        : _db.Lahan.Where(x =>
            x.Petani.ApplicationUserId == userId
            || x.Petani.Poktan.AdminAssignments.Any(a => a.UserId == userId)
            || x.Petani.Poktan.Association.AdminAssignments.Any(a => a.UserId == userId));
  }

  public IQueryable<CertificationCycle> AccessibleCertificationCycles(ClaimsPrincipal user) {
    if (IsSuperAdmin(user)) return _db.CertificationCycles;
    var userId = UserId(user);
    return userId is null
        ? _db.CertificationCycles.Where(x => false)
        : _db.CertificationCycles.Where(x =>
            x.Association.AdminAssignments.Any(a => a.UserId == userId)
            || x.Association.Poktan.Any(p => p.AdminAssignments.Any(a => a.UserId == userId))
            || x.Participants.Any(p => p.Petani.ApplicationUserId == userId));
  }

  public async Task<bool> CanAccessCertificationCycleAsync(ClaimsPrincipal user, Guid cycleId, CancellationToken ct) {
    return await AccessibleCertificationCycles(user).AnyAsync(x => x.Id == cycleId, ct);
  }

  public async Task<bool> CanManageCertificationCycleAsync(ClaimsPrincipal user, Guid cycleId, CancellationToken ct) {
    if (IsSuperAdmin(user)) return true;
    var associationId = await _db.CertificationCycles.Where(x => x.Id == cycleId)
        .Select(x => (Guid?)x.AssociationId).SingleOrDefaultAsync(ct);
    return associationId is not null && await CanManageAssociationAsync(user, associationId.Value, ct);
  }

  public async Task<bool> CanManageParticipantPreparationAsync(ClaimsPrincipal user, Guid participantId, CancellationToken ct) {
    if (IsSuperAdmin(user)) return true;
    var scope = await _db.CertificationParticipants.Where(x => x.Id == participantId)
        .Select(x => new { x.CertificationCycle.AssociationId, x.PoktanIdSnapshot }).SingleOrDefaultAsync(ct);
    return scope is not null && (await CanManageAssociationAsync(user, scope.AssociationId, ct)
        || await CanManagePoktanAsync(user, scope.PoktanIdSnapshot, ct));
  }

  public async Task<bool> CanManagePetaniAsync(ClaimsPrincipal user, Guid petaniId, CancellationToken ct) {
    if (IsSuperAdmin(user)) return true;
    var scope = await _db.Petani.Where(x => x.Id == petaniId)
        .Select(x => new { x.PoktanId, x.Poktan.AssociationId }).SingleOrDefaultAsync(ct);
    return scope is not null && (await CanManageAssociationAsync(user, scope.AssociationId, ct)
        || await CanManagePoktanAsync(user, scope.PoktanId, ct));
  }

  public async Task<bool> CanAccessAssociationAsync(ClaimsPrincipal user, Guid associationId, CancellationToken ct) {
    if (IsSuperAdmin(user)) {
      return true;
    }

    var userId = UserId(user);
    if (userId is null) {
      return false;
    }

    return await _db.AssociationAdminAssignments.AnyAsync(x => x.UserId == userId && x.AssociationId == associationId, ct)
        || await _db.PoktanAdminAssignments.AnyAsync(x => x.UserId == userId && x.Poktan.AssociationId == associationId, ct);
  }

  public async Task<bool> CanManageAssociationAsync(ClaimsPrincipal user, Guid associationId, CancellationToken ct) {
    if (IsSuperAdmin(user)) {
      return true;
    }

    var userId = UserId(user);

    return userId is not null && await _db.AssociationAdminAssignments.AnyAsync(x => x.UserId == userId && x.AssociationId == associationId, ct);
  }

  public async Task<bool> CanManagePoktanAsync(ClaimsPrincipal user, Guid poktanId, CancellationToken ct) {
    if (IsSuperAdmin(user)) {
      return true;
    }

    var userId = UserId(user);
    if (userId is null) {
      return false;
    }

    return await _db.PoktanAdminAssignments.AnyAsync(x => x.UserId == userId && x.PoktanId == poktanId, ct)
        || await _db.AssociationAdminAssignments.AnyAsync(x => x.UserId == userId && x.Association.Poktan.Any(p => p.Id == poktanId), ct);
  }

  public async Task<bool> CanManagePoktanAuditAsync(ClaimsPrincipal user, Guid poktanId, CancellationToken ct) {
    if (IsSuperAdmin(user)) {
      return true;
    }

    var userId = UserId(user);
    if (userId is null) {
      return false;
    }

    return await _db.IcsAuditorAssignments.AnyAsync(x => x.UserId == userId && x.PoktanId == poktanId, ct)
        || await CanManagePoktanAsync(user, poktanId, ct);
  }

  public async Task<bool> IsIcsAuditorForAnyPoktanInAssociationAsync(ClaimsPrincipal user, Guid associationId, CancellationToken ct) {
    var userId = UserId(user);
    return userId is not null && await _db.IcsAuditorAssignments.AnyAsync(x => x.UserId == userId && x.Poktan.AssociationId == associationId, ct);
  }

  public async Task<bool> CanViewPetaniAsync(ClaimsPrincipal user, Guid petaniId, CancellationToken ct) {
    if (IsSuperAdmin(user)) {
      return true;
    }

    var userId = UserId(user);
    if (userId is null) {
      return false;
    }

    if (await _db.Petani.AnyAsync(x => x.Id == petaniId && x.ApplicationUserId == userId, ct)) {
      return true;
    }

    var poktanId = await _db.Petani.Where(x => x.Id == petaniId).Select(x => (Guid?)x.PoktanId).SingleOrDefaultAsync(ct);

    return poktanId is not null && await CanManagePoktanAsync(user, poktanId.Value, ct);
  }
}
