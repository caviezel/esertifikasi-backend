using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models;
using Esertifikasi.Api.Security;
using Esertifikasi.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Esertifikasi.Api.Services.OperationsAccess;
namespace Esertifikasi.Api.Controllers;
[ApiController, Authorize, Route("api/field-logs")]
public sealed partial class FieldLogsController(AppDbContext db, AccessService access, OperationsAccess scope) : ControllerBase {
  private async Task<Lahan> Land(Guid id, bool manage, CancellationToken ct) {
    var land = await db.Lahan.Include(x => x.Petani).ThenInclude(x => x.Poktan).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException();
    if (manage ? !await scope.ManagePoktan(User, land.Petani.PoktanId, ct) : !await scope.Farmers(User).AnyAsync(x => x.Id == land.PetaniId, ct)) throw new UnauthorizedAccessException();
    return land;
  }
  [HttpGet("rotations")]
  public async Task<IActionResult> Rotations(Guid lahanId, CancellationToken ct) { await Land(lahanId, false, ct); return Ok(await db.Set<HarvestRotation>().Where(x => x.LahanId == lahanId).OrderByDescending(x => x.StartDate).Select(x => new { x.Id, x.LahanId, x.Name, x.StartDate, x.EndDate }).ToListAsync(ct)); }
  [HttpPost("rotations")]
  public async Task<IActionResult> Rotation(RotationRequest r, CancellationToken ct) {
    await Land(r.LahanId, true, ct); Require(r.StartDate != default && r.EndDate >= r.StartDate, "Tanggal rotasi tidak valid.");
    await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
    Require(!await db.Set<HarvestRotation>().AnyAsync(x => x.LahanId == r.LahanId && x.StartDate <= r.EndDate && x.EndDate >= r.StartDate, ct), "Tanggal rotasi bertumpang tindih.");
    var row = new HarvestRotation { Id = Guid.NewGuid(), LahanId = r.LahanId, Name = r.Name.Trim(), StartDate = r.StartDate, EndDate = r.EndDate };
    db.Add(row); await db.SaveChangesAsync(ct); if (transaction is not null) await transaction.CommitAsync(ct); return Created($"/api/field-logs/rotations?lahanId={row.LahanId}", new { row.Id });
  }
  [HttpPut("rotations/{id:guid}")]
  public async Task<IActionResult> UpdateRotation(Guid id, RotationRequest r, CancellationToken ct) {
    var row = await db.Set<HarvestRotation>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException();
    await Land(row.LahanId, true, ct); Require(r.LahanId == row.LahanId && r.StartDate != default && r.EndDate >= r.StartDate && !string.IsNullOrWhiteSpace(r.Name), "Rotasi tidak valid.");
    await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
    Require(!await db.Set<HarvestRotation>().AnyAsync(x => x.Id != id && x.LahanId == row.LahanId && x.StartDate <= r.EndDate && x.EndDate >= r.StartDate, ct), "Tanggal rotasi bertumpang tindih.");
    Require(!await db.Set<FieldLog>().AnyAsync(x => x.RotationId == id && (x.ActivityDate < r.StartDate || x.ActivityDate > r.EndDate), ct), "Tanggal rotasi harus mencakup seluruh catatan panen terkait.");
    row.Name = r.Name.Trim(); row.StartDate = r.StartDate; row.EndDate = r.EndDate; await db.SaveChangesAsync(ct); if (transaction is not null) await transaction.CommitAsync(ct); return NoContent();
  }
  internal async Task<FieldLog> Prepare(FieldLogRequest r, FieldLog? existing, CancellationToken ct) {
    var land = await Land(r.LahanId, true, ct); Require(r.ActivityDate != default, "Tanggal aktivitas wajib.");
    if (existing is not null) Require(existing.LahanId == r.LahanId && existing.Activity == r.Activity && existing.ActivityDate.Year == r.ActivityDate.Year, "Lahan, aktivitas, dan tahun tidak dapat diubah.");
    Require(Enum.IsDefined(r.Activity), "Aktivitas tidak valid.");
    if (r.Activity == FieldActivity.Harvest) Require(await db.Set<HarvestRotation>().AnyAsync(x => x.Id == r.RotationId && x.LahanId == r.LahanId && x.StartDate <= r.ActivityDate && x.EndDate >= r.ActivityDate, ct), "Pilih rotasi lahan yang mencakup tanggal panen.");
    else Require(r.RotationId is null, "Rotasi hanya untuk panen.");
    if (r.Activity == FieldActivity.Spraying) Require(land.LuasLegalitas is not null && r.TreatedAreaHa <= land.LuasLegalitas, "Area semprot melebihi luas legalitas atau luas belum tersedia.");
    var row = existing ?? new FieldLog { Id = Guid.NewGuid(), CreatedByUserId = AccessService.UserId(User)!.Value, FarmerNameSnapshot = land.Petani.Nama, NikSnapshot = land.Petani.Nik ?? "", LandLegalNumberSnapshot = land.NoLegalitas, LandAreaHaSnapshot = land.LuasLegalitas };
    // Copy only request fields; identity snapshots and workflow metadata remain server-owned.
    foreach (var p in typeof(FieldLogRequest).GetProperties()) typeof(FieldLog).GetProperty(p.Name)!.SetValue(row, p.GetValue(r));
    return row;
  }
  internal async Task AssignNumber(FieldLog row, CancellationToken ct) {
    var counter = await db.Set<ActivityCounter>().FindAsync([row.LahanId, row.Activity, row.ActivityDate.Year], ct);
    if (counter is null) { counter = new ActivityCounter { LahanId = row.LahanId, Activity = row.Activity, Year = row.ActivityDate.Year }; db.Add(counter); }
    counter.LastNumber++; counter.Version = Guid.NewGuid(); row.SequenceNumber = counter.LastNumber;
  }
  [HttpPost]
  public async Task<IActionResult> Create(FieldLogRequest r, CancellationToken ct) { var row = await Prepare(r, null, ct); await AssignNumber(row, ct); db.Add(row); await db.SaveChangesAsync(ct); return Created($"/api/field-logs/{row.Id}", row); }
  [HttpGet]
  public async Task<IActionResult> List([FromQuery] FieldLogQuery r, CancellationToken ct) {
    var farmers = scope.Farmers(User).Select(x => x.Id);
    var q = db.Set<FieldLog>().Where(x => x.Lahan.Petani.Poktan.AssociationId == r.AssociationId && farmers.Contains(x.Lahan.PetaniId));
    if (r.PoktanId.HasValue) q = q.Where(x => x.Lahan.Petani.PoktanId == r.PoktanId);
    if (r.LahanId.HasValue) q = q.Where(x => x.LahanId == r.LahanId);
    if (r.Activity.HasValue) q = q.Where(x => x.Activity == r.Activity);
    if (r.Year.HasValue) q = q.Where(x => x.ActivityDate.Year == r.Year);
    if (r.RotationId.HasValue) q = q.Where(x => x.RotationId == r.RotationId);
    if (r.Status.HasValue) q = q.Where(x => x.Status == r.Status);
    if (!string.IsNullOrWhiteSpace(r.Search)) q = q.Where(x => x.FarmerNameSnapshot.Contains(r.Search) || x.LandLegalNumberSnapshot != null && x.LandLegalNumberSnapshot.Contains(r.Search));
    return Ok(await q.OrderByDescending(x => x.ActivityDate).ThenBy(x => x.Id).ToPagedResultAsync(r, ct));
  }
  [HttpGet("{id:guid}")]
  public async Task<IActionResult> Get(Guid id, CancellationToken ct) { var row = await db.Set<FieldLog>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException(); await Land(row.LahanId, false, ct); return Ok(row); }
  [HttpPut("{id:guid}")]
  public async Task<IActionResult> Update(Guid id, FieldLogRequest r, CancellationToken ct) { var row = await db.Set<FieldLog>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException(); if (row.Status == MonitoringSubmissionStatus.Finalized) return Conflict(new { message = "Buka kembali sebelum mengubah." }); await Prepare(r, row, ct); row.Version = Guid.NewGuid(); await db.SaveChangesAsync(ct); return NoContent(); }
  [HttpPost("{id:guid}/finalize")]
  public async Task<IActionResult> Finalize(Guid id, CancellationToken ct) { var row = await db.Set<FieldLog>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException(); var land = await Land(row.LahanId, false, ct); if (!await access.CanManagePoktanAsync(User, land.Petani.PoktanId, ct)) return Forbid(); if (row.Status == MonitoringSubmissionStatus.Finalized) return Conflict(); row.Version = Guid.NewGuid(); row.Status = MonitoringSubmissionStatus.Finalized; await db.SaveChangesAsync(ct); return NoContent(); }
  [HttpPost("{id:guid}/reopen")]
  public async Task<IActionResult> Reopen(Guid id, ReopenMonitoringRequest r, CancellationToken ct) { var row = await db.Set<FieldLog>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException(); var land = await Land(row.LahanId, false, ct); if (!await access.CanManageAssociationAsync(User, land.Petani.Poktan.AssociationId, ct)) return Forbid(); Require(!string.IsNullOrWhiteSpace(r.Reason), "Alasan wajib."); if (row.Status != MonitoringSubmissionStatus.Finalized) return Conflict(); row.Version = Guid.NewGuid(); row.Status = MonitoringSubmissionStatus.Reopened; row.ReopenReason = r.Reason.Trim(); await db.SaveChangesAsync(ct); return NoContent(); }
  [HttpDelete("{id:guid}")]
  public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { var row = await db.Set<FieldLog>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException(); await Land(row.LahanId, true, ct); if (row.Status == MonitoringSubmissionStatus.Finalized) return Conflict(); row.Version = Guid.NewGuid(); row.IsDeleted = true; row.DeletedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); return NoContent(); }
  [HttpGet("totals")]
  public async Task<IActionResult> Totals(Guid lahanId, FieldActivity activity, int? year, CancellationToken ct) {
    await Land(lahanId, false, ct); var rows = await db.Set<FieldLog>().Where(x => x.LahanId == lahanId && x.Activity == activity && x.Status == MonitoringSubmissionStatus.Finalized && (!year.HasValue || x.ActivityDate.Year == year)).ToListAsync(ct);
    object Sum(IEnumerable<FieldLog> a) => new { WeightKg = a.Sum(x => x.WeightKg), BunchCount = a.Sum(x => x.BunchCount), TreeCount = a.Sum(x => x.TreeCount), GrossSales = a.Sum(x => x.GrossSales), Deductions = a.Sum(x => x.Deductions), WorkerCost = a.Sum(x => x.WorkerCost), Transport = a.Sum(x => x.Transport), MaterialQuantity = a.Sum(x => x.MaterialQuantity), MaterialCost = a.Sum(x => x.MaterialCost), TotalCost = a.Sum(x => x.TotalCost), NetIncome = a.Sum(x => x.NetIncome) };
    return Ok(new { Overall = Sum(rows), Rotations = rows.Where(x => x.RotationId != null).GroupBy(x => x.RotationId).Select(g => new { RotationId = g.Key, Totals = Sum(g) }) });
  }
}
