using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models;
using Esertifikasi.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api.Controllers;

[ApiController, Route("api/admin"), Authorize(Roles = AppRoles.SuperAdmin)]
public sealed class AdminController : ControllerBase {
  private static readonly string[] ManageableRoles = { AppRoles.SuperAdmin, AppRoles.AssociationAdmin, AppRoles.PoktanAdmin, AppRoles.IcsAuditor };

  private readonly AppDbContext _db;
  private readonly UserManager<ApplicationUser> _users;
  private readonly TokenService _tokens;

  public AdminController(AppDbContext db, UserManager<ApplicationUser> users, TokenService tokens) {
    _db = db;
    _users = users;
    _tokens = tokens;
  }

  [HttpGet("roles")]
  public IActionResult GetRoles() => Ok(ManageableRoles);

  [HttpGet("users")]
  public async Task<ActionResult<PagedResult<AdminUserResponse>>> GetUsers(
      [FromQuery] AdminUserQuery request, CancellationToken ct) {
    var query = _db.Users.Where(u => _db.UserRoles.Any(ur => ur.UserId == u.Id
        && _db.Roles.Any(r => r.Id == ur.RoleId && ManageableRoles.Contains(r.Name))));

    if (!string.IsNullOrWhiteSpace(request.Role)) {
      query = query.Where(u => _db.UserRoles.Any(ur => ur.UserId == u.Id
          && _db.Roles.Any(r => r.Id == ur.RoleId && r.Name == request.Role)));
    }

    if (request.Status is not null) {
      query = query.Where(u => u.Status == request.Status);
    }

    if (!string.IsNullOrWhiteSpace(request.Search)) {
      var search = request.Search.Trim().ToLower();
      query = query.Where(u => u.Email!.ToLower().Contains(search));
    }

    query = (request.SortBy?.ToLowerInvariant(), request.Descending) switch {
      ("status", false) => query.OrderBy(u => u.Status).ThenBy(u => u.Email),
      ("status", true) => query.OrderByDescending(u => u.Status).ThenByDescending(u => u.Email),
      (_, true) => query.OrderByDescending(u => u.Email),
      _ => query.OrderBy(u => u.Email)
    };

    var page = await query
        .Select(u => new AdminUserRow(u.Id, u.Email!, u.Status, u.CreatedAt, u.LastLoginAt))
        .ToPagedResultAsync(request, ct);
    var items = await EnrichAsync(page.Items, ct);
    return Ok(new PagedResult<AdminUserResponse> {
      Items = items, Page = page.Page, PageSize = page.PageSize, TotalCount = page.TotalCount
    });
  }

  [HttpGet("users/{id:guid}")]
  public async Task<ActionResult<AdminUserResponse>> GetUser(Guid id, CancellationToken ct) {
    var row = await _db.Users.Where(u => u.Id == id && _db.UserRoles.Any(ur => ur.UserId == u.Id
            && _db.Roles.Any(r => r.Id == ur.RoleId && ManageableRoles.Contains(r.Name))))
        .Select(u => new AdminUserRow(u.Id, u.Email!, u.Status, u.CreatedAt, u.LastLoginAt))
        .SingleOrDefaultAsync(ct);
    if (row is null) {
      return NotFound();
    }

    return Ok((await EnrichAsync(new[] { row }, ct))[0]);
  }

  [HttpPost("users")]
  public async Task<IActionResult> Create(CreateAdminRequest request, CancellationToken ct) {
    var validation = await ValidateRolesScopeAsync(request.Roles, request.AssociationId, request.PoktanId, ct);
    if (validation is not null) {
      return validation;
    }

    var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = request.Email, Email = request.Email, EmailConfirmed = true, Status = AccountStatus.Active };
    var created = await _users.CreateAsync(user, request.Password);
    if (!created.Succeeded) {
      return ValidationProblem(new ValidationProblemDetails(created.Errors.GroupBy(x => x.Code).ToDictionary(x => x.Key, x => x.Select(e => e.Description).ToArray())));
    }

    await _users.AddToRolesAsync(user, request.Roles);
    await ReconcileScopeAssignmentsAsync(user.Id, request.Roles, request.AssociationId, request.PoktanId, ct);

    await _db.SaveChangesAsync(ct);
    return Created($"/api/admin/users/{user.Id}", new { user.Id, user.Email, request.Roles });
  }

  [HttpPut("users/{id:guid}")]
  public async Task<IActionResult> Update(Guid id, UpdateAdminUserRequest request, CancellationToken ct) {
    var user = await _users.FindByIdAsync(id.ToString());
    var currentRoles = user is null ? Array.Empty<string>() : (await _users.GetRolesAsync(user)).Where(ManageableRoles.Contains).ToArray();
    if (user is null || currentRoles.Length == 0) {
      return NotFound();
    }

    var validation = await ValidateRolesScopeAsync(request.Roles, request.AssociationId, request.PoktanId, ct);
    if (validation is not null) {
      return validation;
    }

    var demotingOrDeactivating = !request.Roles.Contains(AppRoles.SuperAdmin) || request.Status != AccountStatus.Active;
    if (demotingOrDeactivating && await IsLastActiveSuperAdminAsync(user, ct)) {
      return Conflict(new { message = "Tidak dapat mengubah SuperAdmin aktif terakhir." });
    }

    var toRemove = currentRoles.Except(request.Roles).ToArray();
    var toAdd = request.Roles.Except(currentRoles).ToArray();
    if (toRemove.Length > 0) {
      var removed = await _users.RemoveFromRolesAsync(user, toRemove);
      if (!removed.Succeeded) {
        return ValidationProblem(new ValidationProblemDetails(removed.Errors.GroupBy(x => x.Code).ToDictionary(x => x.Key, x => x.Select(e => e.Description).ToArray())));
      }
    }

    if (toAdd.Length > 0) {
      var added = await _users.AddToRolesAsync(user, toAdd);
      if (!added.Succeeded) {
        return ValidationProblem(new ValidationProblemDetails(added.Errors.GroupBy(x => x.Code).ToDictionary(x => x.Key, x => x.Select(e => e.Description).ToArray())));
      }
    }

    await ReconcileScopeAssignmentsAsync(user.Id, request.Roles, request.AssociationId, request.PoktanId, ct);

    var previousStatus = user.Status;
    user.Status = request.Status;
    await _users.UpdateAsync(user);
    await _db.SaveChangesAsync(ct);

    if (previousStatus == AccountStatus.Active && request.Status != AccountStatus.Active) {
      await _tokens.RevokeAllForUserAsync(user.Id, ct);
    }

    return NoContent();
  }

  [HttpDelete("users/{id:guid}")]
  public async Task<IActionResult> Delete(Guid id, CancellationToken ct) {
    var user = await _users.FindByIdAsync(id.ToString());
    if (user is null || !(await _users.GetRolesAsync(user)).Any(ManageableRoles.Contains)) {
      return NotFound();
    }

    if (user.Status == AccountStatus.Disabled) {
      return NoContent();
    }

    if (await IsLastActiveSuperAdminAsync(user, ct)) {
      return Conflict(new { message = "Tidak dapat menonaktifkan SuperAdmin aktif terakhir." });
    }

    user.Status = AccountStatus.Disabled;
    await _users.UpdateAsync(user);
    await _tokens.RevokeAllForUserAsync(user.Id, ct);

    return NoContent();
  }

  private async Task<AdminUserResponse[]> EnrichAsync(IReadOnlyCollection<AdminUserRow> rows, CancellationToken ct) {
    var ids = rows.Select(x => x.Id).ToArray();

    var rolesByUser = await (
        from ur in _db.UserRoles
        join r in _db.Roles on ur.RoleId equals r.Id
        where ids.Contains(ur.UserId) && ManageableRoles.Contains(r.Name)
        select new { ur.UserId, RoleName = r.Name! }
    ).ToListAsync(ct);
    var rolesLookup = rolesByUser.GroupBy(x => x.UserId).ToDictionary(g => g.Key, g => g.Select(x => x.RoleName).ToArray());

    var associationScopes = await _db.AssociationAdminAssignments
        .Where(x => ids.Contains(x.UserId))
        .Select(x => new { x.UserId, x.AssociationId, AssociationNama = x.Association.Nama })
        .ToDictionaryAsync(x => x.UserId, ct);
    var poktanScopes = await _db.PoktanAdminAssignments
        .Where(x => ids.Contains(x.UserId))
        .Select(x => new { x.UserId, x.PoktanId, PoktanNama = x.Poktan.Nama })
        .ToDictionaryAsync(x => x.UserId, ct);
    var icsAuditorScopes = await _db.IcsAuditorAssignments
        .Where(x => ids.Contains(x.UserId))
        .Select(x => new { x.UserId, x.PoktanId, PoktanNama = x.Poktan.Nama })
        .ToDictionaryAsync(x => x.UserId, ct);

    return rows.Select(row => {
      associationScopes.TryGetValue(row.Id, out var association);
      if (!poktanScopes.TryGetValue(row.Id, out var poktan)) {
        icsAuditorScopes.TryGetValue(row.Id, out poktan);
      }

      var roles = rolesLookup.GetValueOrDefault(row.Id, Array.Empty<string>());
      return new AdminUserResponse(row.Id, row.Email, roles, row.Status, row.CreatedAt, row.LastLoginAt,
          association?.AssociationId, association?.AssociationNama, poktan?.PoktanId, poktan?.PoktanNama);
    }).ToArray();
  }

  private async Task<IActionResult?> ValidateRolesScopeAsync(string[] roles, Guid? associationId, Guid? poktanId, CancellationToken ct) {
    if (roles.Length == 0 || roles.Any(r => !ManageableRoles.Contains(r))) {
      return BadRequest(new { message = "Role tidak valid. Hanya SuperAdmin, AssociationAdmin, PoktanAdmin, atau IcsAuditor yang didukung." });
    }

    if (roles.Contains(AppRoles.SuperAdmin) && (roles.Length > 1 || associationId is not null || poktanId is not null)) {
      return BadRequest(new { message = "SuperAdmin tidak dapat digabung dengan role lain atau memiliki cakupan Association/Poktan." });
    }

    if (roles.Contains(AppRoles.AssociationAdmin) && (associationId is null || !await _db.Associations.AnyAsync(x => x.Id == associationId, ct))) {
      return BadRequest(new { message = "AssociationId yang valid wajib diisi." });
    }

    if ((roles.Contains(AppRoles.PoktanAdmin) || roles.Contains(AppRoles.IcsAuditor))
        && (poktanId is null || !await _db.Poktan.AnyAsync(x => x.Id == poktanId, ct))) {
      return BadRequest(new { message = "PoktanId yang valid wajib diisi." });
    }

    return null;
  }

  private async Task<bool> IsLastActiveSuperAdminAsync(ApplicationUser user, CancellationToken ct) {
    if (user.Status != AccountStatus.Active || !await _users.IsInRoleAsync(user, AppRoles.SuperAdmin)) {
      return false;
    }

    var superAdmins = await _users.GetUsersInRoleAsync(AppRoles.SuperAdmin);
    return superAdmins.Count(x => x.Id != user.Id && x.Status == AccountStatus.Active) == 0;
  }

  private async Task ReconcileScopeAssignmentsAsync(Guid userId, string[] roles, Guid? associationId, Guid? poktanId, CancellationToken ct) {
    var existingAssociation = await _db.AssociationAdminAssignments.Where(x => x.UserId == userId).ToListAsync(ct);
    if (existingAssociation.Count > 0) {
      _db.AssociationAdminAssignments.RemoveRange(existingAssociation);
    }

    var existingPoktan = await _db.PoktanAdminAssignments.Where(x => x.UserId == userId).ToListAsync(ct);
    if (existingPoktan.Count > 0) {
      _db.PoktanAdminAssignments.RemoveRange(existingPoktan);
    }

    var existingIcsAuditor = await _db.IcsAuditorAssignments.Where(x => x.UserId == userId).ToListAsync(ct);
    if (existingIcsAuditor.Count > 0) {
      _db.IcsAuditorAssignments.RemoveRange(existingIcsAuditor);
    }

    if (roles.Contains(AppRoles.AssociationAdmin) && associationId is not null) {
      _db.AssociationAdminAssignments.Add(new() { UserId = userId, AssociationId = associationId.Value });
    }

    if (roles.Contains(AppRoles.PoktanAdmin) && poktanId is not null) {
      _db.PoktanAdminAssignments.Add(new() { UserId = userId, PoktanId = poktanId.Value });
    }

    if (roles.Contains(AppRoles.IcsAuditor) && poktanId is not null) {
      _db.IcsAuditorAssignments.Add(new() { UserId = userId, PoktanId = poktanId.Value });
    }
  }
}

public sealed record CreateAdminRequest(string Email, string Password, string[] Roles, Guid? AssociationId, Guid? PoktanId);

public sealed record UpdateAdminUserRequest(string[] Roles, Guid? AssociationId, Guid? PoktanId, AccountStatus Status);

public sealed record AdminUserResponse(
    Guid Id, string Email, string[] Roles, AccountStatus Status,
    DateTimeOffset CreatedAt, DateTimeOffset? LastLoginAt,
    Guid? AssociationId, string? AssociationNama,
    Guid? PoktanId, string? PoktanNama);

internal sealed record AdminUserRow(
    Guid Id, string Email, AccountStatus Status,
    DateTimeOffset CreatedAt, DateTimeOffset? LastLoginAt);

public sealed class AdminUserQuery : PagedQuery {
  public string? Role { get; set; }
  public AccountStatus? Status { get; set; }
}
