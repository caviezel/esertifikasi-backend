using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models;
using Esertifikasi.Api.Security;
using Esertifikasi.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Esertifikasi.Api.Services.OperationsAccess;
namespace Esertifikasi.Api.Controllers;
[ApiController, Authorize, Route("api/associations/{associationId:guid}/training")]
public sealed class AssociationTrainingController(AppDbContext db, AccessService access, OperationsAccess scope) : ControllerBase {
  private IQueryable<AssociationTraining> Visible(Guid associationId) {
    var id = AccessService.UserId(User);
    return db.Set<AssociationTraining>().Where(x => x.AssociationId == associationId && (User.IsInRole(AppRoles.SuperAdmin) || x.Association.AdminAssignments.Any(a => a.UserId == id) || x.PoktanId != null && (x.Poktan!.AdminAssignments.Any(a => a.UserId == id) || db.IcsAuditorAssignments.Any(a => a.PoktanId == x.PoktanId && a.UserId == id)) || x.Participants.Any(p => p.Petani.ApplicationUserId == id || p.Petani.Poktan.AdminAssignments.Any(a => a.UserId == id) || db.IcsAuditorAssignments.Any(a => a.UserId == id && a.PoktanId == p.Petani.PoktanId))));
  }
  private async Task Manage(Guid associationId, Guid? poktanId, CancellationToken ct) {
    if (!await db.Associations.AnyAsync(x => x.Id == associationId, ct)) throw new KeyNotFoundException();
    if (poktanId.HasValue) Require(await db.Poktan.AnyAsync(x => x.Id == poktanId && x.AssociationId == associationId, ct), "Poktan di luar asosiasi.");
    if (!(await access.CanManageAssociationAsync(User, associationId, ct) || poktanId.HasValue && await scope.ManagePoktan(User, poktanId.Value, ct))) throw new UnauthorizedAccessException();
  }
  private async Task<AssociationTraining> Load(Guid associationId, Guid id, CancellationToken ct) {
    var row = await db.Set<AssociationTraining>().Include(x => x.Days).Include(x => x.Topics).ThenInclude(x => x.Topic).Include(x => x.Participants).ThenInclude(x => x.Petani).SingleOrDefaultAsync(x => x.Id == id && x.AssociationId == associationId, ct) ?? throw new KeyNotFoundException();
    // Child edits must also compete with concurrent completion/reopening of the session.
    if (Request.Method != HttpMethods.Get) row.Version = Guid.NewGuid();
    return row;
  }

  private static string Status(AssociationTraining t, DateOnly today) => t.CompletedAt.HasValue ? "Completed" : today < t.StartDate ? "Upcoming" : today <= t.EndDate ? "Ongoing" : "AwaitingCompletion";
  private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Asia/Makassar").DateTime);
  private async Task Validate(Guid associationId, AssociationTrainingRequest r, CancellationToken ct) {
    Require(!string.IsNullOrWhiteSpace(r.Title) && r.StartDate != default && r.EndDate >= r.StartDate, "Judul/tanggal tidak valid.");
    Require(r.Days.Length > 0 && r.Days.Distinct().Count() == r.Days.Length && r.Days.All(d => d >= r.StartDate && d <= r.EndDate), "Pilih hari unik dalam rentang tanggal.");
    var ids = r.PetaniIds.Distinct().ToArray(); Require(ids.Length > 0, "Pilih minimal satu petani.");
    Require(await db.Petani.CountAsync(x => ids.Contains(x.Id) && x.Poktan.AssociationId == associationId && (!r.PoktanId.HasValue || x.PoktanId == r.PoktanId), ct) == ids.Length, "Petani di luar cakupan training.");
    var packageIds = r.PackageIds.Distinct().ToArray();
    Require(await db.Set<TrainingPackage>().CountAsync(x => packageIds.Contains(x.Id), ct) == packageIds.Length, "Paket tidak ditemukan.");
    r.TopicIds = r.TopicIds.Concat(await db.Set<TrainingPackageTopic>().Where(x => packageIds.Contains(x.PackageId)).Select(x => x.TopicId).ToListAsync(ct)).Distinct().ToArray();
    Require(r.TopicIds.Length > 0 && await db.Set<TrainingTopic>().CountAsync(x => r.TopicIds.Contains(x.Id) && (x.AssociationId == null || x.AssociationId == associationId), ct) == r.TopicIds.Length, "Topik tidak valid.");
  }
  [HttpPost]
  public async Task<IActionResult> Create(Guid associationId, AssociationTrainingRequest r, CancellationToken ct) {
    await Manage(associationId, r.PoktanId, ct); await Validate(associationId, r, ct);
    var t = new AssociationTraining { Id = Guid.NewGuid(), AssociationId = associationId, PoktanId = r.PoktanId, Title = r.Title.Trim(), StartDate = r.StartDate, EndDate = r.EndDate, CreatedByUserId = AccessService.UserId(User)!.Value,
      Days = r.Days.Select(d => new AssociationTrainingDay { Date = d }).ToList(), Participants = r.PetaniIds.Distinct().Select(id => new AssociationTrainingParticipant { PetaniId = id }).ToList(), Topics = r.TopicIds.Select(id => new AssociationTrainingTopic { TopicId = id }).ToList() };
    db.Add(t); await db.SaveChangesAsync(ct); return Created($"/api/associations/{associationId}/training/{t.Id}", new { t.Id });
  }
  [HttpPut("{id:guid}")]
  public async Task<IActionResult> Update(Guid associationId, Guid id, AssociationTrainingRequest r, CancellationToken ct) {
    var t = await Load(associationId, id, ct); await Manage(associationId, t.PoktanId, ct); await Manage(associationId, r.PoktanId, ct);
    if (t.CompletedAt.HasValue) return Conflict(new { message = "Buka training kembali sebelum mengubah." });
    await Validate(associationId, r, ct); var attendance = await db.Set<DailyTrainingAttendance>().Where(x => x.TrainingId == id).ToListAsync(ct);
    Require(attendance.All(a => r.PetaniIds.Contains(a.PetaniId) && r.Days.Contains(a.Date)), "Peserta/hari dengan kehadiran tercatat tidak dapat dihapus.");
    t.Title = r.Title.Trim(); t.StartDate = r.StartDate; t.EndDate = r.EndDate; t.PoktanId = r.PoktanId;
    foreach (var p in t.Participants.Where(x => !r.PetaniIds.Contains(x.PetaniId)).ToArray()) db.Remove(p);
    foreach (var p in r.PetaniIds.Distinct().Except(t.Participants.Select(x => x.PetaniId))) t.Participants.Add(new AssociationTrainingParticipant { PetaniId = p });
    foreach (var d in t.Days.Where(x => !r.Days.Contains(x.Date)).ToArray()) db.Remove(d);
    foreach (var d in r.Days.Except(t.Days.Select(x => x.Date))) t.Days.Add(new AssociationTrainingDay { Date = d });
    foreach (var topic in t.Topics.Where(x => !r.TopicIds.Contains(x.TopicId)).ToArray()) db.Remove(topic);
    foreach (var topic in r.TopicIds.Except(t.Topics.Select(x => x.TopicId))) t.Topics.Add(new AssociationTrainingTopic { TopicId = topic });
    await db.SaveChangesAsync(ct); return NoContent();
  }
  [HttpGet]
  public async Task<IActionResult> List(Guid associationId, [FromQuery] TrainingQuery r, CancellationToken ct) {
    var rows = await Filter(associationId, r).Include(x => x.Days).Include(x => x.Participants).ThenInclude(x => x.Petani).Include(x => x.Topics).ThenInclude(x => x.Topic).OrderByDescending(x => x.StartDate).ThenBy(x => x.Id).ToListAsync(ct);
    if (!string.IsNullOrWhiteSpace(r.Status)) rows = rows.Where(x => Status(x, Today).Equals(r.Status, StringComparison.OrdinalIgnoreCase)).ToList();
    var own = scope.Farmers(User).Select(x => x.Id); var marks = await db.Set<DailyTrainingAttendance>().Where(x => rows.Select(t => t.Id).Contains(x.TrainingId) && own.Contains(x.PetaniId)).ToListAsync(ct);
    var visibleFarmerIds = await own.ToListAsync(ct);
    object Project(AssociationTraining t) { var participants = t.Participants.Where(p => visibleFarmerIds.Contains(p.PetaniId)).ToArray(); return new { t.Id, t.Title, t.StartDate, t.EndDate, t.CompletedAt, t.PoktanId, Status = Status(t, Today), Topics = t.Topics.Select(x => new { x.TopicId, x.Topic.Name }), Days = t.Days.Select(x => x.Date).Order(), ParticipantCount = participants.Length, CompletedParticipantCount = participants.Count(p => t.Days.All(d => marks.Any(a => a.TrainingId == t.Id && a.PetaniId == p.PetaniId && a.Date == d.Date && a.Present))) }; }
    return Ok(new PagedResult<object> { Page = r.Page, PageSize = r.PageSize, TotalCount = rows.Count, Items = rows.Skip((r.Page - 1) * r.PageSize).Take(r.PageSize).Select(Project).ToList() });
  }
  private IQueryable<AssociationTraining> Filter(Guid associationId, TrainingQuery r) { var q = Visible(associationId); if (r.From.HasValue) q = q.Where(x => x.EndDate >= r.From); if (r.To.HasValue) q = q.Where(x => x.StartDate <= r.To); if (!string.IsNullOrWhiteSpace(r.Search)) q = q.Where(x => x.Title.Contains(r.Search) || x.Topics.Any(t => t.Topic.Name.Contains(r.Search))); return q; }
  [HttpGet("summary")]
  public async Task<IActionResult> Summary(Guid associationId, [FromQuery] TrainingQuery r, CancellationToken ct) {
    var q = Filter(associationId, r); var visible = scope.Farmers(User).Select(x => x.Id); var ids = q.Select(x => x.Id);
    return Ok(new { TotalTraining = await q.CountAsync(ct), TotalParticipants = await db.Set<AssociationTrainingParticipant>().Where(x => ids.Contains(x.TrainingId) && visible.Contains(x.PetaniId)).Select(x => x.PetaniId).Distinct().CountAsync(ct), Completed = await q.CountAsync(x => x.CompletedAt != null, ct), Upcoming = await q.CountAsync(x => x.CompletedAt == null && x.StartDate > Today, ct), Ongoing = await q.CountAsync(x => x.CompletedAt == null && x.StartDate <= Today && x.EndDate >= Today, ct), AwaitingCompletion = await q.CountAsync(x => x.CompletedAt == null && x.EndDate < Today, ct) });
  }
  [HttpGet("{id:guid}")]
  public async Task<IActionResult> Get(Guid associationId, Guid id, CancellationToken ct) {
    if (!await Visible(associationId).AnyAsync(x => x.Id == id, ct)) return NotFound(); var t = await Load(associationId, id, ct);
    var farmers = await scope.Farmers(User).Select(x => x.Id).ToListAsync(ct); var attendance = await db.Set<DailyTrainingAttendance>().Where(x => x.TrainingId == id && farmers.Contains(x.PetaniId)).Select(x => new { x.PetaniId, x.Date, x.Present, x.Notes }).ToListAsync(ct);
    return Ok(new { t.Id, t.Title, t.StartDate, t.EndDate, t.CompletedAt, t.PoktanId, t.ReopenReason, t.LegacyDescription, Status = Status(t, Today), Days = t.Days.Select(x => x.Date).Order(), Topics = t.Topics.Select(x => new { x.TopicId, x.Topic.Name }), Participants = t.Participants.Where(x => farmers.Contains(x.PetaniId)).Select(x => new { x.PetaniId, x.Petani.Nama, Completed = t.Days.All(d => attendance.Any(a => a.PetaniId == x.PetaniId && a.Date == d.Date && a.Present)) }), Attendance = attendance });
  }
  [HttpGet("candidates")]
  public async Task<IActionResult> Candidates(Guid associationId, [FromQuery] Guid? poktanId, [FromQuery] PagedQuery r, CancellationToken ct) { var q = scope.Farmers(User).Where(x => x.Poktan.AssociationId == associationId && (!poktanId.HasValue || x.PoktanId == poktanId)); if (!string.IsNullOrWhiteSpace(r.Search)) q = q.Where(x => x.Nama.Contains(r.Search) || x.Nik != null && x.Nik.Contains(r.Search)); return Ok(await q.OrderBy(x => x.Nama).Select(x => new { PetaniId = x.Id, x.Nama, x.Nik, x.PoktanId }).ToPagedResultAsync(r, ct)); }
  [HttpPut("{id:guid}/attendance/{petaniId:guid}/{date}")]
  public async Task<IActionResult> Attendance(Guid associationId, Guid id, Guid petaniId, DateOnly date, DailyAttendanceRequest r, CancellationToken ct) {
    var t = await Load(associationId, id, ct); if (t.CompletedAt.HasValue) return Conflict();
    var farmer = await db.Petani.SingleOrDefaultAsync(x => x.Id == petaniId, ct) ?? throw new KeyNotFoundException();
    if (!await scope.ManagePoktan(User, farmer.PoktanId, ct)) return Forbid();
    Require(t.Participants.Any(x => x.PetaniId == petaniId) && t.Days.Any(x => x.Date == date), "Peserta/hari tidak terdaftar.");
    var row = await db.Set<DailyTrainingAttendance>().FindAsync([id, petaniId, date], ct);
    if (row is null) { row = new DailyTrainingAttendance { TrainingId = id, PetaniId = petaniId, Date = date }; db.Add(row); }
    row.Present = r.Present!.Value; row.Notes = r.Notes; await db.SaveChangesAsync(ct); return NoContent();
  }
  [HttpPost("{id:guid}/complete")]
  public async Task<IActionResult> Complete(Guid associationId, Guid id, CancellationToken ct) {
    var t = await Load(associationId, id, ct); if (!(await access.CanManageAssociationAsync(User, associationId, ct) || t.PoktanId.HasValue && await access.CanManagePoktanAsync(User, t.PoktanId.Value, ct))) return Forbid(); if (t.CompletedAt.HasValue) return Conflict();
    var marks = await db.Set<DailyTrainingAttendance>().Where(x => x.TrainingId == id).ToListAsync(ct); Require(t.Participants.Count > 0 && t.Days.Count > 0 && t.Participants.All(p => t.Days.All(d => marks.Any(a => a.PetaniId == p.PetaniId && a.Date == d.Date))), "Semua kehadiran harian harus diisi.");
    Require(Today >= t.Days.Max(x => x.Date), "Hari training terakhir belum berlangsung."); t.CompletedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); return NoContent();
  }
  [HttpPost("{id:guid}/reopen")]
  public async Task<IActionResult> Reopen(Guid associationId, Guid id, ReopenTrainingRequest r, CancellationToken ct) { var t = await Load(associationId, id, ct); if (!await access.CanManageAssociationAsync(User, associationId, ct)) return Forbid(); Require(!string.IsNullOrWhiteSpace(r.Reason), "Alasan wajib."); if (!t.CompletedAt.HasValue) return Conflict(); t.CompletedAt = null; t.ReopenReason = r.Reason.Trim(); await db.SaveChangesAsync(ct); return NoContent(); }
  [HttpDelete("{id:guid}")]
  public async Task<IActionResult> Delete(Guid associationId, Guid id, CancellationToken ct) { var t = await Load(associationId, id, ct); await Manage(associationId, t.PoktanId, ct); if (t.CompletedAt.HasValue) return Conflict(); Require(!await db.Set<DailyTrainingAttendance>().AnyAsync(x => x.TrainingId == id, ct), "Training dengan kehadiran tidak dapat dihapus."); t.IsDeleted = true; await db.SaveChangesAsync(ct); return NoContent(); }
}
