using System.ComponentModel.DataAnnotations;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Esertifikasi.Api.Services.OperationsAccess;
namespace Esertifikasi.Api.Controllers;
public sealed class ZeroDeclarationRequest { [Required, StringLength(2000)] public string Declaration { get; set; } = ""; }
public sealed partial class MonitoringController {
  [HttpPost("submissions/{id:guid}/zero-declaration")]
  public async Task<IActionResult> DeclareZero(Guid id, ZeroDeclarationRequest r, CancellationToken ct) {
    var h = await Editable(id, ct); Require(!string.IsNullOrWhiteSpace(r.Declaration), "Deklarasi wajib.");
    if (h.Type == MonitoringType.FireIncident) { var d = await _db.Set<FireMonitoring>().Include(x => x.Incidents).SingleAsync(x => x.MonitoringSubmissionId == id, ct); Require(d.Incidents.Count == 0, "Masih ada insiden."); d.NoIncidents = true; d.ZeroIncidentDeclaration = r.Declaration.Trim(); }
    else if (h.Type == MonitoringType.WorkplaceAccident) { var d = await _db.Set<WorkplaceAccidentMonitoring>().Include(x => x.Incidents).SingleAsync(x => x.MonitoringSubmissionId == id, ct); Require(d.Incidents.Count == 0, "Masih ada insiden."); d.NoIncidents = true; d.ZeroIncidentDeclaration = r.Declaration.Trim(); }
    else if (h.Type == MonitoringType.MemberComplaint) { var d = await _db.Set<MemberComplaintMonitoring>().Include(x => x.Complaints).SingleAsync(x => x.MonitoringSubmissionId == id, ct); Require(d.Complaints.Count == 0, "Masih ada pengaduan."); d.NoComplaints = true; d.ZeroComplaintDeclaration = r.Declaration.Trim(); }
    else throw new MonitoringValidationException("Deklarasi nihil hanya untuk MICS 8, 9, 13.");
    await _db.SaveChangesAsync(ct); return NoContent();
  }
}
