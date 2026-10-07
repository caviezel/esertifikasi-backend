using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models;
using Esertifikasi.Api.Security;
using Esertifikasi.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Esertifikasi.Api.Services.OperationsAccess;
namespace Esertifikasi.Api.Controllers;
[ApiController, Authorize, Route("api/operations/catalog")]
public sealed class OperationsCatalogController(AppDbContext db, AccessService access, OperationsAccess scope) : ControllerBase {
  [HttpGet("topics")]
  public async Task<IActionResult> Topics(Guid associationId, CancellationToken ct) { if (!await scope.ViewAssociation(User, associationId, ct)) return Forbid(); return Ok(await db.Set<TrainingTopic>().Where(x => x.AssociationId == null || x.AssociationId == associationId).OrderBy(x => x.Name).ToListAsync(ct)); }
  [HttpPost("topics")]
  public async Task<IActionResult> Topic(Guid? associationId, NameRequest r, CancellationToken ct) { if (associationId is null ? !User.IsInRole(AppRoles.SuperAdmin) : !await access.CanManageAssociationAsync(User, associationId.Value, ct)) return Forbid(); Require(!string.IsNullOrWhiteSpace(r.Name), "Nama wajib."); var name = r.Name.Trim(); var row = await db.Set<TrainingTopic>().SingleOrDefaultAsync(x => x.AssociationId == associationId && x.Name == name, ct); if (row is null) { row = new TrainingTopic { Id = Guid.NewGuid(), AssociationId = associationId, Name = name }; db.Add(row); await db.SaveChangesAsync(ct); } return Ok(row); }
  [HttpGet("packages")]
  public async Task<IActionResult> Packages(CancellationToken ct) => Ok(await db.Set<TrainingPackage>().OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, Topics = x.Topics.Select(t => new { t.TopicId, t.Topic.Name }) }).ToListAsync(ct));
  [HttpPost("packages")]
  public async Task<IActionResult> Package(PackageRequest r, CancellationToken ct) { if (!User.IsInRole(AppRoles.SuperAdmin)) return Forbid(); Require(await db.Set<TrainingTopic>().CountAsync(x => r.TopicIds.Contains(x.Id) && x.AssociationId == null, ct) == r.TopicIds.Distinct().Count(), "Paket hanya boleh berisi topik global."); var row = new TrainingPackage { Id = Guid.NewGuid(), Name = r.Name.Trim(), Topics = r.TopicIds.Distinct().Select(id => new TrainingPackageTopic { TopicId = id }).ToList() }; db.Add(row); await db.SaveChangesAsync(ct); return Ok(new { row.Id, row.Name }); }
  [HttpGet("first-aid-locations")]
  public async Task<IActionResult> Locations(Guid associationId, CancellationToken ct) { if (!await scope.ViewAssociation(User, associationId, ct)) return Forbid(); return Ok(await db.Set<FirstAidLocation>().Where(x => x.AssociationId == associationId && x.IsActive).OrderBy(x => x.Name).ToListAsync(ct)); }
  [HttpPost("first-aid-locations")]
  public async Task<IActionResult> Location(Guid associationId, NameRequest r, CancellationToken ct) { if (!await access.CanManageAssociationAsync(User, associationId, ct)) return Forbid(); Require(!string.IsNullOrWhiteSpace(r.Name), "Nama wajib."); var row = new FirstAidLocation { Id = Guid.NewGuid(), AssociationId = associationId, Name = r.Name.Trim() }; db.Add(row); await db.SaveChangesAsync(ct); return Ok(row); }
  [HttpPut("first-aid-locations/{id:guid}")]
  public async Task<IActionResult> UpdateLocation(Guid id, NameRequest r, CancellationToken ct) { var row = await db.Set<FirstAidLocation>().FindAsync([id], ct) ?? throw new KeyNotFoundException(); if (!await access.CanManageAssociationAsync(User, row.AssociationId, ct)) return Forbid(); Require(!string.IsNullOrWhiteSpace(r.Name), "Nama wajib."); row.Name = r.Name.Trim(); await db.SaveChangesAsync(ct); return NoContent(); }
  [HttpDelete("first-aid-locations/{id:guid}")]
  public async Task<IActionResult> DeactivateLocation(Guid id, CancellationToken ct) { var row = await db.Set<FirstAidLocation>().FindAsync([id], ct) ?? throw new KeyNotFoundException(); if (!await access.CanManageAssociationAsync(User, row.AssociationId, ct)) return Forbid(); row.IsActive = false; await db.SaveChangesAsync(ct); return NoContent(); }
  [HttpPut("topics/{id:guid}")]
  public async Task<IActionResult> UpdateTopic(Guid id, NameRequest r, CancellationToken ct) { var row = await db.Set<TrainingTopic>().FindAsync([id], ct) ?? throw new KeyNotFoundException(); if (row.AssociationId is null ? !User.IsInRole(AppRoles.SuperAdmin) : !await access.CanManageAssociationAsync(User, row.AssociationId.Value, ct)) return Forbid(); Require(!string.IsNullOrWhiteSpace(r.Name), "Nama wajib."); row.Name = r.Name.Trim(); await db.SaveChangesAsync(ct); return NoContent(); }
  [HttpPut("packages/{id:guid}")]
  public async Task<IActionResult> UpdatePackage(Guid id, PackageRequest r, CancellationToken ct) { if (!User.IsInRole(AppRoles.SuperAdmin)) return Forbid(); var row = await db.Set<TrainingPackage>().Include(x => x.Topics).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException(); Require(!string.IsNullOrWhiteSpace(r.Name) && await db.Set<TrainingTopic>().CountAsync(x => r.TopicIds.Contains(x.Id) && x.AssociationId == null, ct) == r.TopicIds.Distinct().Count(), "Topik paket tidak valid."); row.Name = r.Name.Trim(); foreach (var topic in row.Topics.Where(x => !r.TopicIds.Contains(x.TopicId)).ToArray()) db.Remove(topic); foreach (var idTopic in r.TopicIds.Distinct().Except(row.Topics.Select(x => x.TopicId))) row.Topics.Add(new TrainingPackageTopic { TopicId = idTopic }); await db.SaveChangesAsync(ct); return NoContent(); }
}
