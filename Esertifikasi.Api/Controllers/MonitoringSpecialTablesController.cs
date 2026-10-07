using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models;
using Esertifikasi.Api.Security;
using Esertifikasi.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Esertifikasi.Api.Services.OperationsAccess;
namespace Esertifikasi.Api.Controllers;
public sealed partial class MonitoringController {
  [HttpGet("first-aid-table")]
  public async Task<IActionResult> FirstAidTable(Guid associationId, int year, Guid? locationId, [FromQuery] PagedQuery paging, CancellationToken ct) {
    Require(year is >= 1900 and <= 9999, "Tahun tidak valid."); var access = new AccessService(_db); var operations = new OperationsAccess(_db, access); if (!await operations.ViewAssociation(User, associationId, ct)) return Forbid();
    var q = _db.Set<FirstAidLocation>().Where(x => x.AssociationId == associationId && x.IsActive && (!locationId.HasValue || x.Id == locationId)); if (!string.IsNullOrWhiteSpace(paging.Search)) q = q.Where(x => x.Name.Contains(paging.Search));
    var page = await q.OrderBy(x => x.Name).ToPagedResultAsync(paging, ct); var ids = page.Items.Select(x => x.Id).ToArray();
    var headers = await _db.MonitoringSubmissions.Where(x => x.AssociationId == associationId && x.Type == MonitoringType.FirstAidKit && x.PeriodStart.Year == year).ToListAsync(ct); var rows = new List<(MonitoringSubmission Header, FirstAidKitInspection Row)>();
    foreach (var header in headers) if (await _monitoring.CanManageAsync(User, header.PoktanId, ct)) { var detail = (FirstAidKitMonitoring)(await DetailAsync(header, ct))!; rows.AddRange(detail.Inspections.Where(x => x.LocationId.HasValue && ids.Contains(x.LocationId.Value)).Select(x => (header, x))); }
    return Ok(new { page.Page, page.PageSize, page.TotalCount, page.TotalPages, Items = page.Items.Select(location => new { Location = location, Observations = rows.Where(x => x.Row.LocationId == location.Id).OrderByDescending(x => x.Row.ObservedOn).Select(x => new { ObservationId = ObservationId(x.Row), SubmissionId = x.Header.Id, x.Header.Status, Data = x.Row }) }) });
  }
  [HttpGet("ppe-table")]
  public async Task<IActionResult> PpeByFarmer(Guid associationId, int year, Guid? poktanId, [FromQuery] PagedQuery paging, CancellationToken ct) {
    Require(year is >= 1900 and <= 9999, "Tahun tidak valid."); var operations = new OperationsAccess(_db, new AccessService(_db));
    var q = operations.Farmers(User).Where(x => x.Poktan.AssociationId == associationId && (!poktanId.HasValue || x.PoktanId == poktanId));
    if (!string.IsNullOrWhiteSpace(paging.Search)) q = q.Where(x => x.Nama.Contains(paging.Search) || x.Nik != null && x.Nik.Contains(paging.Search));
    var page = await q.OrderBy(x => x.Nama).Select(x => new { PetaniId = x.Id, x.Nama, x.Nik, x.PoktanId }).ToPagedResultAsync(paging, ct);
    var poktans = page.Items.Select(x => x.PoktanId).Distinct().ToArray(); var farmerIds = page.Items.Select(x => x.PetaniId).ToArray();
    var headers = await _db.MonitoringSubmissions.Where(x => x.AssociationId == associationId && x.Type == MonitoringType.PersonalProtectiveEquipment && x.PeriodStart.Year == year && poktans.Contains(x.PoktanId)).ToListAsync(ct);
    var rows = new List<(MonitoringSubmission Header, PpeInspection Row)>();
    foreach (var h in headers) { var d = (PpeMonitoring)(await VisibleDetailAsync(h, ct))!; rows.AddRange(d.Inspections.Where(x => x.PetaniId.HasValue && farmerIds.Contains(x.PetaniId.Value)).Select(x => (h, x))); }
    return Ok(new { page.Page, page.PageSize, page.TotalCount, page.TotalPages, Items = page.Items.Select(farmer => new { Farmer = farmer, Observations = rows.Where(x => x.Row.PetaniId == farmer.PetaniId).GroupBy(x => ObservationId(x.Row)).Select(g => new { ObservationId = g.Key, SubmissionId = g.First().Header.Id, Status = g.First().Header.Status, ObservedOn = g.First().Row.ObservedOn, Rows = g.Select(x => x.Row) }).OrderByDescending(x => x.ObservedOn) }) });
  }
  [HttpGet("complaints")]
  public async Task<IActionResult> ComplaintsTable(Guid associationId, int year, Guid? poktanId, Guid? petaniId, ComplaintStatus? status, [FromQuery] PagedQuery paging, CancellationToken ct) {
    Require(year is >= 1900 and <= 9999, "Tahun tidak valid."); var headers = await _db.MonitoringSubmissions.Where(x => x.AssociationId == associationId && x.Type == MonitoringType.MemberComplaint && x.PeriodStart.Year == year && (!poktanId.HasValue || x.PoktanId == poktanId)).ToListAsync(ct); var output = new List<object>();
    foreach (var header in headers) {
      var detail = (MemberComplaintMonitoring)(await VisibleDetailAsync(header, ct))!;
      foreach (var c in detail.Complaints.Where(x => (!petaniId.HasValue || x.PetaniId == petaniId) && (!status.HasValue || x.Status == status) && (string.IsNullOrWhiteSpace(paging.Search) || x.ComplaintType.Contains(paging.Search, StringComparison.OrdinalIgnoreCase) || x.Description.Contains(paging.Search, StringComparison.OrdinalIgnoreCase)))) output.Add(new { ObservationId = ObservationId(c), SubmissionId = header.Id, ReportStatus = header.Status, Data = c });
    }
    return Ok(new PagedResult<object> { Page = paging.Page, PageSize = paging.PageSize, TotalCount = output.Count, Items = output.Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize).ToList() });
  }
  [HttpGet("complaints-table")]
  public async Task<IActionResult> ComplaintsByFarmer(Guid associationId, int year, Guid? poktanId, [FromQuery] PagedQuery paging, CancellationToken ct) {
    Require(year is >= 1900 and <= 9999, "Tahun tidak valid."); var operations = new OperationsAccess(_db, new AccessService(_db)); var q = operations.Farmers(User).Where(x => x.Poktan.AssociationId == associationId && (!poktanId.HasValue || x.PoktanId == poktanId)); if (!string.IsNullOrWhiteSpace(paging.Search)) q = q.Where(x => x.Nama.Contains(paging.Search) || x.Nik != null && x.Nik.Contains(paging.Search));
    var page = await q.OrderBy(x => x.Nama).Select(x => new { PetaniId = x.Id, x.Nama, x.Nik, x.PoktanId }).ToPagedResultAsync(paging, ct); var poktans = page.Items.Select(x => x.PoktanId).Distinct().ToArray(); var headers = await _db.MonitoringSubmissions.Where(x => x.AssociationId == associationId && x.Type == MonitoringType.MemberComplaint && x.PeriodStart.Year == year && poktans.Contains(x.PoktanId)).ToListAsync(ct); var rows = new List<(MonitoringSubmission Header, MemberComplaint Row)>();
    foreach (var h in headers) { var d = (MemberComplaintMonitoring)(await VisibleDetailAsync(h, ct))!; rows.AddRange(d.Complaints.Select(c => (h, c))); }
    return Ok(new { page.Page, page.PageSize, page.TotalCount, page.TotalPages, Items = page.Items.Select(farmer => new { Farmer = farmer, RecordCount = rows.Count(x => x.Row.PetaniId == farmer.PetaniId), Complaints = rows.Where(x => x.Row.PetaniId == farmer.PetaniId).OrderByDescending(x => x.Row.ReceivedOn).Select(x => new { ObservationId = ObservationId(x.Row), SubmissionId = x.Header.Id, ReportStatus = x.Header.Status, Data = x.Row }) }) });
  }
}
