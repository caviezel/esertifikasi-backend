using System.IO.Compression;
using System.Security;
using System.Text;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models;
using Esertifikasi.Api.Security;
using Esertifikasi.Api.Services;
using Esertifikasi.Api.Services.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api.Controllers;

[ApiController, Route("api/monitoring"), Authorize]
public sealed partial class MonitoringController : ControllerBase {
  private readonly AppDbContext _db;
  private readonly MonitoringService _monitoring;
  private readonly FileValidationService _fileValidation;
  private readonly IFileStorage _storage;
  private readonly LandBoundaryMonitoringExcelService _landBoundaryImport;
  public MonitoringController(AppDbContext db, MonitoringService monitoring, FileValidationService fileValidation, IFileStorage storage, LandBoundaryMonitoringExcelService landBoundaryImport) {
    _db = db; _monitoring = monitoring; _fileValidation = fileValidation; _storage = storage; _landBoundaryImport = landBoundaryImport;
  }

  [HttpGet("definitions")]
  public async Task<IActionResult> Definitions(CancellationToken ct) {
    var configured = await _db.MonitoringDefinitions.AsNoTracking().ToDictionaryAsync(x => x.Type, ct);
    return Ok(Enum.GetValues<MonitoringType>().Select(x => configured.GetValueOrDefault(x) ?? MonitoringService.Definition(x)));
  }

  [HttpPut("definitions/{type}")]
  public async Task<IActionResult> ConfigureDefinition(MonitoringType type, MonitoringDefinition request, CancellationToken ct) {
    if (!User.IsInRole(AppRoles.SuperAdmin)) return Forbid();
    if (!Enum.IsDefined(type) || !Enum.IsDefined(request.Frequency) || !Enum.IsDefined(request.Group) || string.IsNullOrWhiteSpace(request.Name)) return ValidationProblem("Definisi tidak valid.");
    if (request.RequiredOccurrencesPerYear < 1) return ValidationProblem("RequiredOccurrencesPerYear minimal 1.");
    var row = await _db.MonitoringDefinitions.SingleOrDefaultAsync(x => x.Type == type, ct);
    if (row is null) { row = new MonitoringDefinition { Type = type }; _db.MonitoringDefinitions.Add(row); }
    row.Name = request.Name.Trim(); row.Group = request.Group; row.Frequency = request.Frequency; row.RequiredOccurrencesPerYear = request.RequiredOccurrencesPerYear; row.IsActive = request.IsActive;
    await _db.SaveChangesAsync(ct); return NoContent();
  }

  [HttpGet("reference-items")]
  public async Task<IActionResult> ReferenceItems([FromQuery] MonitoringReferenceKind? kind, CancellationToken ct) {
    var query = _db.MonitoringReferenceItems.Where(x => x.IsActive);
    if (kind is not null) query = query.Where(x => x.Kind == kind);
    return Ok(await query.OrderBy(x => x.Kind).ThenBy(x => x.Name).ToListAsync(ct));
  }

  [HttpPost("reference-items")]
  public async Task<IActionResult> CreateReferenceItem(MonitoringReferenceItem request, CancellationToken ct) {
    if (!User.IsInRole(AppRoles.SuperAdmin)) return Forbid();
    request.Id = Guid.NewGuid(); request.Code = request.Code.Trim(); request.Name = request.Name.Trim();
    _db.MonitoringReferenceItems.Add(request); await _db.SaveChangesAsync(ct);
    return Created($"/api/monitoring/reference-items/{request.Id}", request);
  }

  [HttpGet("submissions")]
  public async Task<IActionResult> List([FromQuery] MonitoringSubmissionQuery request, CancellationToken ct) {
    if (!await CanAccessAssociationAsync(request.AssociationId, ct)) return Forbid();
    var userId = AccessService.UserId(User);
    var query = _db.MonitoringSubmissions.Where(x => x.AssociationId == request.AssociationId && (User.IsInRole(AppRoles.SuperAdmin) || x.Poktan.Association.AdminAssignments.Any(a => a.UserId == userId) || x.Poktan.AdminAssignments.Any(a => a.UserId == userId) || _db.IcsAuditorAssignments.Any(a => a.UserId == userId && a.PoktanId == x.PoktanId)));
    if (request.PoktanId is not null) query = query.Where(x => x.PoktanId == request.PoktanId);
    if (request.Type is not null) query = query.Where(x => x.Type == request.Type);
    if (request.Status is not null) query = query.Where(x => x.Status == request.Status);
    if (request.Year is not null) query = query.Where(x => x.PeriodStart.Year == request.Year);
    if (request.PetaniId is not null || request.LahanId is not null) {
      var ids = SubjectSubmissionIds(request.PetaniId, request.LahanId);
      query = query.Where(x => ids.Contains(x.Id));
    }
    if (!string.IsNullOrWhiteSpace(request.Search)) query = query.Where(x => x.Summary != null && x.Summary.Contains(request.Search));
    var projected = query.OrderByDescending(x => x.PeriodStart).ThenBy(x => x.Type).Select(x => new MonitoringSubmissionListItem(
        x.Id, x.AssociationId, x.PoktanId, x.Poktan.Nama, x.Type, x.Group, x.PeriodStart, x.PeriodEnd, x.Status,
        x.Summary, x.FollowUps.Count(f => f.Status == FollowUpStatus.Open || f.Status == FollowUpStatus.InProgress),
        x.Attachments.Count, x.CreatedByUserId, x.CreatedAt, x.FinalizedAt));
    return Ok(await projected.ToPagedResultAsync(request, ct));
  }

  [HttpGet("submissions/{id:guid}")]
  public async Task<IActionResult> Get(Guid id, CancellationToken ct) {
    var header = await _db.MonitoringSubmissions.AsNoTracking().Include(x => x.FollowUps).Include(x => x.Attachments).SingleOrDefaultAsync(x => x.Id == id, ct);
    if (header is null) return NotFound(); if (!await _monitoring.CanViewAsync(User, header, ct)) return Forbid();
    return Ok(new { Submission = header, Detail = await VisibleDetailAsync(header, ct) });
  }

  [HttpPost("land-boundaries")] public Task<IActionResult> LandBoundaries(CreateLandBoundaryMonitoringRequest r, CancellationToken ct) =>
      Create(r, MonitoringType.LandBoundaryMarker, async h => { await _monitoring.PrepareRowsAsync(r.Inspections, h.PoktanId, h.Id, ct); ValidateDates(r.Inspections, r); return new LandBoundaryMonitoring { MonitoringSubmissionId = h.Id, Inspections = r.Inspections }; }, ct);
  [HttpPost("turnera")] public Task<IActionResult> Turnera(CreateTurneraMonitoringRequest r, CancellationToken ct) =>
      Create(r, MonitoringType.Turnera, async h => { await _monitoring.PrepareRowsAsync(r.Inspections, h.PoktanId, h.Id, ct); return new TurneraMonitoring { MonitoringSubmissionId = h.Id, Inspections = r.Inspections }; }, ct);
  [HttpPost("chemical-buffers")] public Task<IActionResult> ChemicalBuffers(CreateChemicalBufferMonitoringRequest r, CancellationToken ct) =>
      Create(r, MonitoringType.ChemicalBufferBoundary, async h => { await _monitoring.PrepareRowsAsync(r.Inspections, h.PoktanId, h.Id, ct); return new ChemicalBufferMonitoring { MonitoringSubmissionId = h.Id, Inspections = r.Inspections }; }, ct);
  [HttpPost("woody-plants")] public Task<IActionResult> WoodyPlants(CreateWoodyPlantMonitoringRequest r, CancellationToken ct) =>
      Create(r, MonitoringType.WoodyPlantAndErosionControl, async h => { foreach (var x in r.Inspections) foreach (var y in x.Observations) y.Id = Guid.NewGuid(); await _monitoring.PrepareRowsAsync(r.Inspections, h.PoktanId, h.Id, ct); return new WoodyPlantMonitoring { MonitoringSubmissionId = h.Id, Inspections = r.Inspections }; }, ct);
  [HttpPost("first-aid-kits")] public Task<IActionResult> FirstAidKits(CreateFirstAidKitMonitoringRequest r, CancellationToken ct) =>
      Create(r, MonitoringType.FirstAidKit, h => { foreach (var x in r.Inspections) { x.Id = Guid.NewGuid(); x.MonitoringSubmissionId = h.Id; foreach (var y in x.Items) { y.Id = Guid.NewGuid(); y.FirstAidKitInspectionId = x.Id; } } return Task.FromResult(new FirstAidKitMonitoring { MonitoringSubmissionId = h.Id, Inspections = r.Inspections }); }, ct);
  [HttpPost("ppe")] public Task<IActionResult> Ppe(CreatePpeMonitoringRequest r, CancellationToken ct) =>
      Create(r, MonitoringType.PersonalProtectiveEquipment, async h => { foreach (var x in r.Inspections) foreach (var y in x.Items) y.Id = Guid.NewGuid(); await _monitoring.PrepareRowsAsync(r.Inspections, h.PoktanId, h.Id, ct); return new PpeMonitoring { MonitoringSubmissionId = h.Id, Inspections = r.Inspections }; }, ct);
  [HttpPost("high-conservation-values")] public Task<IActionResult> Hcv(CreateHighConservationValueMonitoringRequest r, CancellationToken ct) =>
      Create(r, MonitoringType.HighConservationValue, async h => { await _monitoring.PrepareRowsAsync(r.Locations, h.PoktanId, h.Id, ct); await _monitoring.PrepareRowsAsync(r.SpeciesObservations, h.PoktanId, h.Id, ct); return new HighConservationValueMonitoring { MonitoringSubmissionId = h.Id, Locations = r.Locations, SpeciesObservations = r.SpeciesObservations }; }, ct);
  [HttpPost("fires")] public Task<IActionResult> Fires(CreateFireMonitoringRequest r, CancellationToken ct) =>
      Create(r, MonitoringType.FireIncident, async h => { ValidateZero(r.NoIncidents, r.Incidents.Count, "kebakaran"); await _monitoring.PrepareRowsAsync(r.Incidents, h.PoktanId, h.Id, ct); return new FireMonitoring { MonitoringSubmissionId = h.Id, NoIncidents = r.NoIncidents, ZeroIncidentDeclaration = r.ZeroIncidentDeclaration, Incidents = r.Incidents }; }, ct);
  [HttpPost("workplace-accidents")] public Task<IActionResult> Accidents(CreateWorkplaceAccidentMonitoringRequest r, CancellationToken ct) =>
      Create(r, MonitoringType.WorkplaceAccident, async h => { ValidateZero(r.NoIncidents, r.Incidents.Count, "kecelakaan"); await _monitoring.PrepareRowsAsync(r.Incidents, h.PoktanId, h.Id, ct); return new WorkplaceAccidentMonitoring { MonitoringSubmissionId = h.Id, NoIncidents = r.NoIncidents, ZeroIncidentDeclaration = r.ZeroIncidentDeclaration, Incidents = r.Incidents }; }, ct);
  [HttpPost("weeds")] public Task<IActionResult> Weeds(CreateWeedMonitoringRequest r, CancellationToken ct) =>
      Create(r, MonitoringType.Weed, async h => { await _monitoring.PrepareRowsAsync(r.Inspections, h.PoktanId, h.Id, ct); return new WeedMonitoring { MonitoringSubmissionId = h.Id, Inspections = r.Inspections }; }, ct);
  [HttpPost("plant-diseases")] public Task<IActionResult> Diseases(CreatePlantDiseaseMonitoringRequest r, CancellationToken ct) =>
      Create(r, MonitoringType.PlantDisease, async h => { await _monitoring.PrepareRowsAsync(r.Inspections, h.PoktanId, h.Id, ct); return new PlantDiseaseMonitoring { MonitoringSubmissionId = h.Id, Inspections = r.Inspections }; }, ct);
  [HttpPost("pests")] public Task<IActionResult> Pests(CreatePestMonitoringRequest r, CancellationToken ct) =>
      Create(r, MonitoringType.Pest, async h => { await _monitoring.PrepareRowsAsync(r.Inspections, h.PoktanId, h.Id, ct); return new PestMonitoring { MonitoringSubmissionId = h.Id, Inspections = r.Inspections }; }, ct);
  [HttpPost("member-complaints")] public Task<IActionResult> Complaints(CreateMemberComplaintMonitoringRequest r, CancellationToken ct) =>
      Create(r, MonitoringType.MemberComplaint, async h => { ValidateZero(r.NoComplaints, r.Complaints.Count, "pengaduan"); await PrepareComplaintsAsync(r.Complaints, h, ct); return new MemberComplaintMonitoring { MonitoringSubmissionId = h.Id, NoComplaints = r.NoComplaints, ZeroComplaintDeclaration = r.ZeroComplaintDeclaration, Complaints = r.Complaints }; }, ct);

  [HttpPut("land-boundaries/{id:guid}")] public Task<IActionResult> UpdateLand(Guid id, CreateLandBoundaryMonitoringRequest r, CancellationToken ct) => Replace(id, r, MonitoringType.LandBoundaryMarker, async h => { await _monitoring.PrepareRowsAsync(r.Inspections, h.PoktanId, h.Id, ct); ValidateDates(r.Inspections, r); return new LandBoundaryMonitoring { MonitoringSubmissionId = id, Inspections = r.Inspections }; }, ct);
  [HttpPut("turnera/{id:guid}")] public Task<IActionResult> UpdateTurnera(Guid id, CreateTurneraMonitoringRequest r, CancellationToken ct) => Replace(id, r, MonitoringType.Turnera, async h => { await _monitoring.PrepareRowsAsync(r.Inspections, h.PoktanId, h.Id, ct); return new TurneraMonitoring { MonitoringSubmissionId = id, Inspections = r.Inspections }; }, ct);
  [HttpPut("chemical-buffers/{id:guid}")] public Task<IActionResult> UpdateChemical(Guid id, CreateChemicalBufferMonitoringRequest r, CancellationToken ct) => Replace(id, r, MonitoringType.ChemicalBufferBoundary, async h => { await _monitoring.PrepareRowsAsync(r.Inspections, h.PoktanId, h.Id, ct); return new ChemicalBufferMonitoring { MonitoringSubmissionId = id, Inspections = r.Inspections }; }, ct);
  [HttpPut("woody-plants/{id:guid}")] public Task<IActionResult> UpdateWoody(Guid id, CreateWoodyPlantMonitoringRequest r, CancellationToken ct) => Replace(id, r, MonitoringType.WoodyPlantAndErosionControl, async h => { foreach (var x in r.Inspections) foreach (var y in x.Observations) y.Id = Guid.NewGuid(); await _monitoring.PrepareRowsAsync(r.Inspections, h.PoktanId, h.Id, ct); return new WoodyPlantMonitoring { MonitoringSubmissionId = id, Inspections = r.Inspections }; }, ct);
  [HttpPut("first-aid-kits/{id:guid}")] public Task<IActionResult> UpdateFirstAid(Guid id, CreateFirstAidKitMonitoringRequest r, CancellationToken ct) => Replace(id, r, MonitoringType.FirstAidKit, h => { foreach (var x in r.Inspections) { x.Id = Guid.NewGuid(); x.MonitoringSubmissionId = id; foreach (var y in x.Items) { y.Id = Guid.NewGuid(); y.FirstAidKitInspectionId = x.Id; } } return Task.FromResult(new FirstAidKitMonitoring { MonitoringSubmissionId = id, Inspections = r.Inspections }); }, ct);
  [HttpPut("ppe/{id:guid}")] public Task<IActionResult> UpdatePpe(Guid id, CreatePpeMonitoringRequest r, CancellationToken ct) => Replace(id, r, MonitoringType.PersonalProtectiveEquipment, async h => { foreach (var x in r.Inspections) foreach (var y in x.Items) y.Id = Guid.NewGuid(); await _monitoring.PrepareRowsAsync(r.Inspections, h.PoktanId, h.Id, ct); return new PpeMonitoring { MonitoringSubmissionId = id, Inspections = r.Inspections }; }, ct);
  [HttpPut("high-conservation-values/{id:guid}")] public Task<IActionResult> UpdateHcv(Guid id, CreateHighConservationValueMonitoringRequest r, CancellationToken ct) => Replace(id, r, MonitoringType.HighConservationValue, async h => { await _monitoring.PrepareRowsAsync(r.Locations, h.PoktanId, h.Id, ct); await _monitoring.PrepareRowsAsync(r.SpeciesObservations, h.PoktanId, h.Id, ct); return new HighConservationValueMonitoring { MonitoringSubmissionId = id, Locations = r.Locations, SpeciesObservations = r.SpeciesObservations }; }, ct);
  [HttpPut("fires/{id:guid}")] public Task<IActionResult> UpdateFire(Guid id, CreateFireMonitoringRequest r, CancellationToken ct) => Replace(id, r, MonitoringType.FireIncident, async h => { ValidateZero(r.NoIncidents, r.Incidents.Count, "kebakaran"); await _monitoring.PrepareRowsAsync(r.Incidents, h.PoktanId, h.Id, ct); return new FireMonitoring { MonitoringSubmissionId = id, NoIncidents = r.NoIncidents, ZeroIncidentDeclaration = r.ZeroIncidentDeclaration, Incidents = r.Incidents }; }, ct);
  [HttpPut("workplace-accidents/{id:guid}")] public Task<IActionResult> UpdateAccident(Guid id, CreateWorkplaceAccidentMonitoringRequest r, CancellationToken ct) => Replace(id, r, MonitoringType.WorkplaceAccident, async h => { ValidateZero(r.NoIncidents, r.Incidents.Count, "kecelakaan"); await _monitoring.PrepareRowsAsync(r.Incidents, h.PoktanId, h.Id, ct); return new WorkplaceAccidentMonitoring { MonitoringSubmissionId = id, NoIncidents = r.NoIncidents, ZeroIncidentDeclaration = r.ZeroIncidentDeclaration, Incidents = r.Incidents }; }, ct);
  [HttpPut("weeds/{id:guid}")] public Task<IActionResult> UpdateWeed(Guid id, CreateWeedMonitoringRequest r, CancellationToken ct) => Replace(id, r, MonitoringType.Weed, async h => { await _monitoring.PrepareRowsAsync(r.Inspections, h.PoktanId, h.Id, ct); return new WeedMonitoring { MonitoringSubmissionId = id, Inspections = r.Inspections }; }, ct);
  [HttpPut("plant-diseases/{id:guid}")] public Task<IActionResult> UpdateDisease(Guid id, CreatePlantDiseaseMonitoringRequest r, CancellationToken ct) => Replace(id, r, MonitoringType.PlantDisease, async h => { await _monitoring.PrepareRowsAsync(r.Inspections, h.PoktanId, h.Id, ct); return new PlantDiseaseMonitoring { MonitoringSubmissionId = id, Inspections = r.Inspections }; }, ct);
  [HttpPut("pests/{id:guid}")] public Task<IActionResult> UpdatePest(Guid id, CreatePestMonitoringRequest r, CancellationToken ct) => Replace(id, r, MonitoringType.Pest, async h => { await _monitoring.PrepareRowsAsync(r.Inspections, h.PoktanId, h.Id, ct); return new PestMonitoring { MonitoringSubmissionId = id, Inspections = r.Inspections }; }, ct);
  [HttpPut("member-complaints/{id:guid}")] public Task<IActionResult> UpdateComplaint(Guid id, CreateMemberComplaintMonitoringRequest r, CancellationToken ct) => Replace(id, r, MonitoringType.MemberComplaint, async h => { if (!await _monitoring.CanReopenAsync(User, h.AssociationId, ct)) throw new UnauthorizedAccessException(); ValidateZero(r.NoComplaints, r.Complaints.Count, "pengaduan"); await PrepareComplaintsAsync(r.Complaints, h, ct); return new MemberComplaintMonitoring { MonitoringSubmissionId = id, NoComplaints = r.NoComplaints, ZeroComplaintDeclaration = r.ZeroComplaintDeclaration, Complaints = r.Complaints }; }, ct);

  [HttpPost("submissions/{id:guid}/finalize")]
  public async Task<IActionResult> Finalize(Guid id, CancellationToken ct) {
    var row = await _db.MonitoringSubmissions.SingleOrDefaultAsync(x => x.Id == id, ct); if (row is null) return NotFound();
    if (!await _monitoring.CanFinalizeAsync(User, row.PoktanId, ct)) return Forbid(); if (row.Status == MonitoringSubmissionStatus.Finalized) return Conflict(new { message = "Submission sudah final." });
    await ValidateCompleteAsync(row, ct); row.Version = Guid.NewGuid(); row.Status = MonitoringSubmissionStatus.Finalized; row.FinalizedByUserId = AccessService.UserId(User); row.FinalizedAt = DateTimeOffset.UtcNow; await _db.SaveChangesAsync(ct); return NoContent();
  }

  [HttpPost("submissions/{id:guid}/reopen")]
  public async Task<IActionResult> Reopen(Guid id, ReopenMonitoringRequest request, CancellationToken ct) {
    var row = await _db.MonitoringSubmissions.SingleOrDefaultAsync(x => x.Id == id, ct); if (row is null) return NotFound();
    if (!await _monitoring.CanReopenAsync(User, row.AssociationId, ct)) return Forbid(); if (row.Status != MonitoringSubmissionStatus.Finalized) return Conflict(new { message = "Hanya submission final yang dapat dibuka kembali." });
    row.Version = Guid.NewGuid(); row.Status = MonitoringSubmissionStatus.Reopened; row.ReopenedByUserId = AccessService.UserId(User); row.ReopenedAt = DateTimeOffset.UtcNow; row.ReopenReason = request.Reason.Trim(); await _db.SaveChangesAsync(ct); return NoContent();
  }

  [HttpDelete("submissions/{id:guid}")]
  public async Task<IActionResult> Delete(Guid id, CancellationToken ct) {
    var row = await _db.MonitoringSubmissions.SingleOrDefaultAsync(x => x.Id == id, ct); if (row is null) return NotFound();
    if (!await _monitoring.CanManageAsync(User, row.PoktanId, ct)) return Forbid(); if (row.Status == MonitoringSubmissionStatus.Finalized) return Conflict(new { message = "Submission final tidak dapat dihapus." });
    row.Version = Guid.NewGuid(); row.IsDeleted = true; row.DeletedAt = DateTimeOffset.UtcNow; await _db.SaveChangesAsync(ct); return NoContent();
  }

  [HttpGet("compliance")]
  public async Task<IActionResult> Compliance([FromQuery] Guid associationId, [FromQuery] Guid? poktanId, [FromQuery] int year, CancellationToken ct) {
    if (!await CanAccessAssociationAsync(associationId, ct)) return Forbid();
    var query = _db.MonitoringSubmissions.Where(x => x.AssociationId == associationId && x.PeriodStart.Year == year && x.Status == MonitoringSubmissionStatus.Finalized);
    if (poktanId is not null) query = query.Where(x => x.PoktanId == poktanId);
    var counts = await query.GroupBy(x => x.Type).Select(x => new { x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    var configured = await _db.MonitoringDefinitions.AsNoTracking().ToDictionaryAsync(x => x.Type, ct);
    return Ok(Enum.GetValues<MonitoringType>().Where(type => !configured.TryGetValue(type, out var effective) || effective.IsActive).Select(type => { var d = configured.GetValueOrDefault(type) ?? MonitoringService.Definition(type); var complete = counts.GetValueOrDefault(type); return new MonitoringComplianceItem(type, d.Name, d.Frequency, d.RequiredOccurrencesPerYear, complete, Math.Max(0, d.RequiredOccurrencesPerYear - complete), complete >= d.RequiredOccurrencesPerYear ? "Complete" : "Incomplete"); }));
  }

  [HttpPost("submissions/{id:guid}/follow-ups")]
  public async Task<IActionResult> AddFollowUp(Guid id, UpsertMonitoringFollowUpRequest request, CancellationToken ct) {
    var row = await _db.MonitoringSubmissions.SingleOrDefaultAsync(x => x.Id == id, ct); if (row is null) return NotFound(); if (!await _monitoring.CanManageAsync(User, row.PoktanId, ct)) return Forbid();
    var followUp = new MonitoringFollowUp { Id = Guid.NewGuid(), MonitoringSubmissionId = id, Description = request.Description.Trim(), Status = request.Status, AssignedToUserId = request.AssignedToUserId, DueDate = request.DueDate, CompletedAt = request.Status == FollowUpStatus.Completed ? DateTimeOffset.UtcNow : null };
    _db.MonitoringFollowUps.Add(followUp); await _db.SaveChangesAsync(ct); return Created($"/api/monitoring/submissions/{id}/follow-ups/{followUp.Id}", followUp);
  }

  [HttpPut("submissions/{id:guid}/follow-ups/{followUpId:guid}")]
  public async Task<IActionResult> UpdateFollowUp(Guid id, Guid followUpId, UpsertMonitoringFollowUpRequest request, CancellationToken ct) {
    var row = await _db.MonitoringSubmissions.SingleOrDefaultAsync(x => x.Id == id, ct); if (row is null) return NotFound(); if (!await _monitoring.CanManageAsync(User, row.PoktanId, ct)) return Forbid();
    var item = await _db.MonitoringFollowUps.SingleOrDefaultAsync(x => x.Id == followUpId && x.MonitoringSubmissionId == id, ct); if (item is null) return NotFound(); item.Description = request.Description.Trim(); item.Status = request.Status; item.AssignedToUserId = request.AssignedToUserId; item.DueDate = request.DueDate; item.CompletedAt = request.Status == FollowUpStatus.Completed ? item.CompletedAt ?? DateTimeOffset.UtcNow : null; await _db.SaveChangesAsync(ct); return NoContent();
  }

  [HttpPost("submissions/{id:guid}/attachments")]
  public async Task<IActionResult> Upload(Guid id, IFormFile file, CancellationToken ct) {
    var row = await _db.MonitoringSubmissions.SingleOrDefaultAsync(x => x.Id == id, ct); if (row is null) return NotFound(); if (!await _monitoring.CanManageAsync(User, row.PoktanId, ct)) return Forbid(); if (row.Status == MonitoringSubmissionStatus.Finalized) return Conflict(new { message = "Buka kembali submission sebelum menambah lampiran." });
    var type = new DocumentType { AllowedExtensions = ".pdf,.jpg,.jpeg,.png", MaximumFileSize = 10 * 1024 * 1024 };
    await using var validated = await _fileValidation.ValidateAsync(file, type, ct); var key = await _storage.SaveAsync(validated.Content, validated.Extension, ct);
    row.Version = Guid.NewGuid();
    var attachment = new MonitoringAttachment { Id = Guid.NewGuid(), MonitoringSubmissionId = id, StorageKey = key, OriginalFileName = validated.OriginalFileName, ContentType = validated.ContentType, FileExtension = validated.Extension, FileSize = validated.Size, Sha256Hash = validated.Sha256Hash, UploadedByUserId = AccessService.UserId(User)!.Value };
    _db.MonitoringAttachments.Add(attachment); await _db.SaveChangesAsync(ct); return Created($"/api/monitoring/submissions/{id}/attachments/{attachment.Id}", attachment);
  }

  [HttpGet("submissions/{id:guid}/attachments/{attachmentId:guid}")]
  public async Task<IActionResult> Download(Guid id, Guid attachmentId, CancellationToken ct) {
    var row = await _db.MonitoringSubmissions.SingleOrDefaultAsync(x => x.Id == id, ct); if (row is null) return NotFound(); if (!await _monitoring.CanViewAsync(User, row, ct)) return Forbid();
    var item = await _db.MonitoringAttachments.SingleOrDefaultAsync(x => x.Id == attachmentId && x.MonitoringSubmissionId == id, ct); if (item is null) return NotFound(); var stored = await _storage.OpenReadAsync(item.StorageKey, ct); return stored is null ? NotFound() : File(stored.Content, item.ContentType, item.OriginalFileName);
  }

  [HttpDelete("submissions/{id:guid}/attachments/{attachmentId:guid}")]
  public async Task<IActionResult> DeleteAttachment(Guid id, Guid attachmentId, CancellationToken ct) {
    var row = await _db.MonitoringSubmissions.SingleOrDefaultAsync(x => x.Id == id, ct); if (row is null) return NotFound(); if (!await _monitoring.CanManageAsync(User, row.PoktanId, ct)) return Forbid(); if (row.Status == MonitoringSubmissionStatus.Finalized) return Conflict(new { message = "Buka kembali submission sebelum menghapus lampiran." });
    var item = await _db.MonitoringAttachments.SingleOrDefaultAsync(x => x.Id == attachmentId && x.MonitoringSubmissionId == id, ct); if (item is null) return NotFound();
    row.Version = Guid.NewGuid(); _db.Remove(item); await _db.SaveChangesAsync(ct); await _storage.DeleteAsync(item.StorageKey, ct); return NoContent();
  }

  [HttpGet("submissions/{id:guid}/export")]
  public async Task<IActionResult> Export(Guid id, CancellationToken ct) {
    var row = await _db.MonitoringSubmissions.Include(x => x.Poktan).SingleOrDefaultAsync(x => x.Id == id, ct); if (row is null) return NotFound(); if (!await _monitoring.CanViewAsync(User, row, ct)) return Forbid();
    var detail = await VisibleDetailAsync(row, ct);
    var values = new List<string[]> { new[] { $"MICS {(int)row.Type + 1}", MonitoringService.Definition(row.Type).Name }, new[] { "Poktan", row.Poktan.Nama }, new[] { "Periode", $"{row.PeriodStart:yyyy-MM-dd} - {row.PeriodEnd:yyyy-MM-dd}" }, new[] { "Status", row.Status.ToString() }, new[] { "Ringkasan", row.Summary ?? "" }, Array.Empty<string>() };
    values.AddRange(ExportRows(detail));
    return File(CreateXlsx(values), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"MICS-{(int)row.Type + 1}-{row.PeriodStart.Year}.xlsx");
  }

  [HttpGet("land-boundaries/import/template")]
  public IActionResult LandBoundaryImportTemplate() =>
      File(_landBoundaryImport.CreateTemplate(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "template-mics-1-patok-batas-lahan.xlsx");

  [HttpPost("land-boundaries/import/preview"), RequestSizeLimit(LandBoundaryMonitoringExcelService.MaximumFileSize + 1024 * 1024)]
  public async Task<IActionResult> PreviewLandBoundaryImport([FromForm] Guid poktanId, [FromForm] IFormFile file, CancellationToken ct) {
    if (!await _monitoring.CanManageAsync(User, poktanId, ct)) return Forbid();
    var parsed = await _landBoundaryImport.ParseAsync(file, ct);
    var (preview, _) = await ValidateLandBoundaryRowsAsync(poktanId, parsed, ct);
    return Ok(preview);
  }

  [HttpPost("land-boundaries/import")]
  public async Task<IActionResult> ImportLandBoundaries(ImportLandBoundaryRequest request, CancellationToken ct) {
    if (!await _monitoring.CanManageAsync(User, request.PoktanId, ct)) return Forbid();
    var rows = request.Rows.Select((x, i) => (i + 2, (BulkLandBoundaryRow?)x, new List<string>())).ToList();
    var (preview, inspections) = await ValidateLandBoundaryRowsAsync(request.PoktanId, rows, ct);
    if (!preview.IsValid) return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> {
      ["rows"] = preview.Rows.Where(x => x.Errors.Count > 0).SelectMany(x => x.Errors.Select(e => $"Baris {x.RowNumber}: {e}")).ToArray()
    }));
    var existing = await _db.MonitoringSubmissions.FirstOrDefaultAsync(x => x.PoktanId == request.PoktanId && x.Type == MonitoringType.LandBoundaryMarker
        && x.PeriodStart <= request.PeriodEnd && x.PeriodEnd >= request.PeriodStart, ct);
    if (existing is null) {
      var create = new CreateLandBoundaryMonitoringRequest { PoktanId = request.PoktanId, PeriodStart = request.PeriodStart, PeriodEnd = request.PeriodEnd, Inspections = inspections };
      return await Create(create, MonitoringType.LandBoundaryMarker, async h => { await _monitoring.PrepareRowsAsync(create.Inspections, h.PoktanId, h.Id, ct); return new LandBoundaryMonitoring { MonitoringSubmissionId = h.Id, Inspections = create.Inspections }; }, ct);
    }
    var current = (LandBoundaryMonitoring)(await DetailAsync(existing, ct))!;
    var update = new CreateLandBoundaryMonitoringRequest { PoktanId = existing.PoktanId, PeriodStart = existing.PeriodStart, PeriodEnd = existing.PeriodEnd,
      Summary = existing.Summary, PreparedByName = existing.PreparedByName, AcknowledgedByName = existing.AcknowledgedByName,
      Inspections = current.Inspections.Concat(inspections).ToList() };
    return await Replace(existing.Id, update, MonitoringType.LandBoundaryMarker, async h => { await _monitoring.PrepareRowsAsync(update.Inspections, h.PoktanId, h.Id, ct); return new LandBoundaryMonitoring { MonitoringSubmissionId = existing.Id, Inspections = update.Inspections }; }, ct);
  }

  private async Task<(LandBoundaryImportPreview Preview, List<LandBoundaryInspection> Inspections)> ValidateLandBoundaryRowsAsync(
      Guid poktanId, IReadOnlyList<(int RowNumber, BulkLandBoundaryRow? Data, List<string> Errors)> rows, CancellationToken ct) {
    var farmers = await _db.Petani.AsNoTracking().Where(x => x.PoktanId == poktanId && x.Nik != null)
        .Select(x => new {
          x.Id,
          Nik = x.Nik!,
          Lahan = x.Lahan.Select(l => new { l.Id, NoLegalitas = (string?)l.NoLegalitas }).ToList()
        }).ToListAsync(ct);
    var byNik = farmers.GroupBy(x => x.Nik!).ToDictionary(x => x.Key, x => x.First());
    var output = new List<LandBoundaryImportPreviewRow>(); var inspections = new List<LandBoundaryInspection>();
    foreach (var (rowNumber, data, parseErrors) in rows) {
      var errors = new List<string>(parseErrors);
      if (data is not null && errors.Count == 0) {
        if (!byNik.TryGetValue(data.Nik.Trim(), out var farmer)) errors.Add($"Petani dengan NIK {data.Nik} tidak ditemukan pada Poktan ini.");
        else {
          Guid? lahanId = null;
          if (!string.IsNullOrWhiteSpace(data.LandLegalNumber)) {
            var match = farmer.Lahan.FirstOrDefault(l => string.Equals(l.NoLegalitas, data.LandLegalNumber.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is null) errors.Add($"Lahan dengan No Legalitas {data.LandLegalNumber} tidak dimiliki Petani tersebut."); else lahanId = match.Id;
          } else if (farmer.Lahan.Count == 1) lahanId = farmer.Lahan[0].Id;
          else errors.Add(farmer.Lahan.Count == 0 ? "Petani belum memiliki Lahan." : "NoLegalitas wajib diisi karena Petani memiliki lebih dari satu Lahan.");
          if (errors.Count == 0) inspections.Add(new LandBoundaryInspection { Id = Guid.NewGuid(), PetaniId = farmer.Id, LahanId = lahanId, ObservedOn = data.ObservedOn,
            InstalledOn = data.InstalledOn, MarkerCount = data.MarkerCount, Condition = data.Condition, Notes = data.Notes, FollowUp = data.FollowUp });
        }
      }
      output.Add(new LandBoundaryImportPreviewRow(rowNumber, data, errors));
    }
    return (new LandBoundaryImportPreview(output.All(x => x.Errors.Count == 0), output.Count, output.Count(x => x.Errors.Count == 0), output), inspections);
  }

  private async Task<IActionResult> Create<T>(CreateMonitoringSubmissionRequest request, MonitoringType type, Func<MonitoringSubmission, Task<T>> detailFactory, CancellationToken ct) where T : class {
    var header = await _monitoring.CreateHeaderAsync(request, type, User, ct); var detail = await detailFactory(header); await ValidateDetailAsync(header, detail, ct); _db.MonitoringSubmissions.Add(header); _db.Add(detail); await _db.SaveChangesAsync(ct); return Created($"/api/monitoring/submissions/{header.Id}", new { header.Id, header.Type, header.Status });
  }

  private async Task<IActionResult> Replace<T>(Guid id, CreateMonitoringSubmissionRequest request, MonitoringType type, Func<MonitoringSubmission, Task<T>> detailFactory, CancellationToken ct) where T : class {
    var header = await _db.MonitoringSubmissions.SingleOrDefaultAsync(x => x.Id == id && x.Type == type, ct); if (header is null) return NotFound(); if (!await _monitoring.CanManageAsync(User, header.PoktanId, ct)) return Forbid(); if (header.Status == MonitoringSubmissionStatus.Finalized) return Conflict(new { message = "Submission final harus dibuka kembali sebelum diubah." }); if (request.PoktanId != header.PoktanId) return ValidationProblem("Poktan tidak dapat diubah.");
    await _monitoring.ValidateEffectivePeriodAsync(type, request.PeriodStart, request.PeriodEnd, ct); if (await _db.MonitoringSubmissions.AnyAsync(x => x.Id != id && x.PoktanId == header.PoktanId && x.Type == type && x.PeriodStart <= request.PeriodEnd && x.PeriodEnd >= request.PeriodStart, ct)) return Conflict(new { message = "Periode bertumpang tindih." });
    await using var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(ct) : null;
    header.Version = Guid.NewGuid(); await RemoveDetailAsync(header, ct); header.PeriodStart = request.PeriodStart; header.PeriodEnd = request.PeriodEnd; header.Summary = request.Summary; header.PreparedByName = request.PreparedByName; header.AcknowledgedByName = request.AcknowledgedByName; var detail = await detailFactory(header); await ValidateDetailAsync(header, detail, ct); _db.Add(detail); await _db.SaveChangesAsync(ct); if (transaction is not null) await transaction.CommitAsync(ct); return NoContent();
  }

  private async Task<object?> DetailAsync(MonitoringSubmission h, CancellationToken ct) => h.Type switch {
    MonitoringType.LandBoundaryMarker => await _db.Set<LandBoundaryMonitoring>().AsNoTracking().Include(x => x.Inspections).SingleAsync(x => x.MonitoringSubmissionId == h.Id, ct),
    MonitoringType.Turnera => await _db.Set<TurneraMonitoring>().AsNoTracking().Include(x => x.Inspections).SingleAsync(x => x.MonitoringSubmissionId == h.Id, ct),
    MonitoringType.ChemicalBufferBoundary => await _db.Set<ChemicalBufferMonitoring>().AsNoTracking().Include(x => x.Inspections).SingleAsync(x => x.MonitoringSubmissionId == h.Id, ct),
    MonitoringType.WoodyPlantAndErosionControl => await _db.Set<WoodyPlantMonitoring>().AsNoTracking().Include(x => x.Inspections).ThenInclude(x => x.Observations).SingleAsync(x => x.MonitoringSubmissionId == h.Id, ct),
    MonitoringType.FirstAidKit => await _db.Set<FirstAidKitMonitoring>().AsNoTracking().Include(x => x.Inspections).ThenInclude(x => x.Items).SingleAsync(x => x.MonitoringSubmissionId == h.Id, ct),
    MonitoringType.PersonalProtectiveEquipment => await _db.Set<PpeMonitoring>().AsNoTracking().Include(x => x.Inspections).ThenInclude(x => x.Items).SingleAsync(x => x.MonitoringSubmissionId == h.Id, ct),
    MonitoringType.HighConservationValue => await _db.Set<HighConservationValueMonitoring>().AsNoTracking().Include(x => x.Locations).Include(x => x.SpeciesObservations).SingleAsync(x => x.MonitoringSubmissionId == h.Id, ct),
    MonitoringType.FireIncident => await _db.Set<FireMonitoring>().AsNoTracking().Include(x => x.Incidents).SingleAsync(x => x.MonitoringSubmissionId == h.Id, ct),
    MonitoringType.WorkplaceAccident => await _db.Set<WorkplaceAccidentMonitoring>().AsNoTracking().Include(x => x.Incidents).SingleAsync(x => x.MonitoringSubmissionId == h.Id, ct),
    MonitoringType.Weed => await _db.Set<WeedMonitoring>().AsNoTracking().Include(x => x.Inspections).SingleAsync(x => x.MonitoringSubmissionId == h.Id, ct),
    MonitoringType.PlantDisease => await _db.Set<PlantDiseaseMonitoring>().AsNoTracking().Include(x => x.Inspections).SingleAsync(x => x.MonitoringSubmissionId == h.Id, ct),
    MonitoringType.Pest => await _db.Set<PestMonitoring>().AsNoTracking().Include(x => x.Inspections).SingleAsync(x => x.MonitoringSubmissionId == h.Id, ct),
    _ => await _db.Set<MemberComplaintMonitoring>().AsNoTracking().Include(x => x.Complaints).SingleAsync(x => x.MonitoringSubmissionId == h.Id, ct)
  };

  private async Task RemoveDetailAsync(MonitoringSubmission h, CancellationToken ct) { var detail = await DetailAsync(h, ct); if (detail is not null) _db.Remove(detail); await _db.SaveChangesAsync(ct); }
  private async Task ValidateCompleteAsync(MonitoringSubmission h, CancellationToken ct) {
    var detail = await DetailAsync(h, ct) ?? throw new MonitoringValidationException("Detail monitoring tidak ditemukan.");
    if (detail is MemberComplaintMonitoring confidential && confidential.Complaints.Any(c => c.IsAnonymous || c.IsConfidential) && !await _monitoring.CanReopenAsync(User, h.AssociationId, ct)) throw new UnauthorizedAccessException();
    await ValidateDetailAsync(h, detail, ct);
    var count = detail switch { LandBoundaryMonitoring x => x.Inspections.Count, TurneraMonitoring x => x.Inspections.Count, ChemicalBufferMonitoring x => x.Inspections.Count, WoodyPlantMonitoring x => x.Inspections.Count, FirstAidKitMonitoring x => x.Inspections.Count(i => i.Items.Count > 0), PpeMonitoring x => x.Inspections.Count, HighConservationValueMonitoring x => x.Locations.Count + x.SpeciesObservations.Count, FireMonitoring x => x.NoIncidents ? 1 : x.Incidents.Count, WorkplaceAccidentMonitoring x => x.NoIncidents ? 1 : x.Incidents.Count, WeedMonitoring x => x.Inspections.Count, PlantDiseaseMonitoring x => x.Inspections.Count, PestMonitoring x => x.Inspections.Count, MemberComplaintMonitoring x => x.NoComplaints ? 1 : x.Complaints.Count, _ => 0 };
    if (count == 0) throw new MonitoringValidationException("Submission belum memiliki data minimum untuk difinalisasi.");
  }
  private async Task<bool> CanAccessAssociationAsync(Guid associationId, CancellationToken ct) { if (await new OperationsAccess(_db, new AccessService(_db)).ViewAssociation(User, associationId, ct)) return true; var userId = AccessService.UserId(User); return userId is not null && await _db.Petani.AnyAsync(x => x.ApplicationUserId == userId && x.Poktan.AssociationId == associationId, ct); }
  private IQueryable<Guid> SubjectSubmissionIds(Guid? petaniId, Guid? lahanId) {
    IQueryable<Guid> From<TRow>() where TRow : FarmerLandMonitoringRow => _db.Set<TRow>()
        .Where(x => (petaniId == null || x.PetaniId == petaniId) && (lahanId == null || x.LahanId == lahanId))
        .Select(x => EF.Property<Guid>(x, "MonitoringSubmissionId"));
    var ids = From<LandBoundaryInspection>().Concat(From<TurneraInspection>()).Concat(From<ChemicalBufferInspection>())
        .Concat(From<WoodyPlantInspection>()).Concat(From<PpeInspection>()).Concat(From<HcvLocationAssessment>())
        .Concat(From<ProtectedSpeciesObservation>()).Concat(From<FireIncident>()).Concat(From<WorkplaceAccidentIncident>())
        .Concat(From<WeedInspection>()).Concat(From<PlantDiseaseInspection>()).Concat(From<PestInspection>());
    if (lahanId is null) ids = ids.Concat(_db.Set<MemberComplaint>().Where(x => petaniId == null || x.PetaniId == petaniId).Select(x => x.MonitoringSubmissionId));
    return ids;
  }
  private static IEnumerable<string[]> ExportRows(object? detail) {
    static string D(DateOnly value) => value.ToString("yyyy-MM-dd");
    static string[] Subject(FarmerLandMonitoringRow x) => new[] { x.FarmerNameSnapshot ?? "", x.NikSnapshot ?? "", x.LandLegalNumberSnapshot ?? "", D(x.ObservedOn) };
    return detail switch {
      LandBoundaryMonitoring x => Table(new[] { "Nama Petani", "NIK", "No Legalitas", "Tanggal Monitoring", "Tanggal Pemasangan", "Jumlah Patok", "Kondisi", "Keterangan", "Tindak Lanjut" }, x.Inspections.Select(i => Subject(i).Concat(new[] { i.InstalledOn?.ToString("yyyy-MM-dd") ?? "", i.MarkerCount.ToString(), i.Condition.ToString(), i.Notes ?? "", i.FollowUp ?? "" }).ToArray())),
      TurneraMonitoring x => Table(new[] { "Nama Petani", "NIK", "No Legalitas", "Tanggal Monitoring", "Kondisi", "Keterangan", "Tindak Lanjut" }, x.Inspections.Select(i => Subject(i).Concat(new[] { i.Condition.ToString(), i.Description ?? i.Notes ?? "", i.FollowUp ?? "" }).ToArray())),
      ChemicalBufferMonitoring x => Table(new[] { "Nama Petani", "NIK", "No Legalitas", "Tanggal", "Patok Sempadan", "Tanpa Kimia 5m", "Tanaman Berkayu 5m", "Tidak Menanam Lahan Miring", "Keterangan" }, x.Inspections.Select(i => Subject(i).Concat(new[] { i.HasRiverBoundaryMarker.ToString(), i.NoChemicalActivityWithinFiveMeters.ToString(), i.HasWoodyPlantsWithinFiveMeters.ToString(), i.NoPlantingOnSteepSlope.ToString(), i.Notes ?? "" }).ToArray())),
      WoodyPlantMonitoring x => Table(new[] { "Nama Petani", "NIK", "No Legalitas", "Tanggal", "Nama Pohon", "Jumlah", "Tinggi (cm)", "Gejala Kerusakan", "Keterangan" }, x.Inspections.SelectMany(i => i.Observations.DefaultIfEmpty().Select(o => Subject(i).Concat(new[] { o?.TreeName ?? "", o?.Quantity.ToString() ?? "", o?.HeightCentimeters?.ToString() ?? "", o?.DamageSymptoms ?? "", o?.Remarks ?? i.Notes ?? "" }).ToArray()))),
      FirstAidKitMonitoring x => Table(new[] { "Lokasi", "Tanggal", "Item", "Kondisi", "Keterangan", "Tindak Lanjut" }, x.Inspections.SelectMany(i => i.Items.Select(item => new[] { i.Location, D(i.ObservedOn), item.ItemName, item.Condition.ToString(), item.Notes ?? "", item.FollowUp ?? "" }))),
      PpeMonitoring x => Table(new[] { "Nama Petani", "NIK", "No Legalitas", "Tanggal", "Jenis Kerja", "Item APD", "Tersedia", "Digunakan", "Kondisi", "Keterangan" }, x.Inspections.SelectMany(i => i.Items.Select(item => Subject(i).Concat(new[] { i.Activity.ToString(), item.ItemName, item.IsAvailable.ToString(), item.IsUsed.ToString(), item.Condition?.ToString() ?? "", item.Notes ?? "" }).ToArray()))),
      HighConservationValueMonitoring x => Table(new[] { "Jenis", "Nama Petani", "NIK", "No Legalitas", "Tanggal", "Semester/Spesies", "Lokasi/Keterangan" }, x.Locations.Select(i => new[] { "Lokasi", i.FarmerNameSnapshot ?? "", i.NikSnapshot ?? "", i.LandLegalNumberSnapshot ?? "", D(i.ObservedOn), i.Semester.ToString(), i.Location }).Concat(x.SpeciesObservations.Select(i => new[] { "Spesies", i.FarmerNameSnapshot ?? "", i.NikSnapshot ?? "", i.LandLegalNumberSnapshot ?? "", D(i.ObservedOn), $"{i.SpeciesKind}: {i.SpeciesName}", i.Description ?? "" }))),
      FireMonitoring x => Table(new[] { "Tanggal", "Nama Petani", "NIK", "No Legalitas", "Kategori", "Kronologis", "Tindak Lanjut" }, x.NoIncidents ? new[] { new[] { "", "", "", "", "Nihil", x.ZeroIncidentDeclaration ?? "Tidak ada kebakaran", "" } } : x.Incidents.Select(i => new[] { D(i.ObservedOn), i.FarmerNameSnapshot ?? "", i.NikSnapshot ?? "", i.LandLegalNumberSnapshot ?? "", i.Severity.ToString(), i.Chronology, i.FollowUp ?? "" })),
      WorkplaceAccidentMonitoring x => Table(new[] { "Tanggal", "Nama Petani", "NIK", "No Legalitas", "Kategori", "Jumlah Kasus", "Kronologis", "Tindak Lanjut" }, x.NoIncidents ? new[] { new[] { "", "", "", "", "Nihil", "0", x.ZeroIncidentDeclaration ?? "Tidak ada kecelakaan", "" } } : x.Incidents.Select(i => new[] { D(i.ObservedOn), i.FarmerNameSnapshot ?? "", i.NikSnapshot ?? "", i.LandLegalNumberSnapshot ?? "", i.Category.ToString(), i.CaseCount.ToString(), i.Chronology, i.FollowUp ?? "" })),
      WeedMonitoring x => ThreatRows("Jenis Gulma", x.Inspections.Select(i => (Row: (FarmerLandMonitoringRow)i, Type: i.WeedType, Result: i.Result, Treatment: i.Treatment, Severity: ""))),
      PlantDiseaseMonitoring x => ThreatRows("Jenis Penyakit", x.Inspections.Select(i => (Row: (FarmerLandMonitoringRow)i, Type: i.DiseaseType, Result: i.Result, Treatment: i.Treatment, Severity: ""))),
      PestMonitoring x => ThreatRows("Jenis Hama", x.Inspections.Select(i => (Row: (FarmerLandMonitoringRow)i, Type: i.PestType, Result: i.ObservedDensity is null ? null : $"{i.ObservedDensity} {i.DensityUnit}", Treatment: i.Treatment, Severity: i.Severity.ToString()))),
      MemberComplaintMonitoring x => Table(new[] { "Tanggal", "Nama Petani", "NIK", "Jenis Pengaduan", "Deskripsi", "Tindak Lanjut", "Status", "Selesai", "Rahasia" }, x.NoComplaints ? new[] { new[] { "", "", "", "Nihil", x.ZeroComplaintDeclaration ?? "Tidak ada pengaduan", "", "", "", "" } } : x.Complaints.Select(i => new[] { D(i.ReceivedOn), i.IsAnonymous ? "Anonim" : i.FarmerNameSnapshot ?? "", i.IsAnonymous ? "" : i.NikSnapshot ?? "", i.ComplaintType, i.Description, i.FollowUp ?? "", i.Status.ToString(), i.ResolvedOn?.ToString("yyyy-MM-dd") ?? "", i.IsConfidential.ToString() })),
      _ => Array.Empty<string[]>()
    };
  }
  private static IEnumerable<string[]> ThreatRows(string typeHeader, IEnumerable<(FarmerLandMonitoringRow Row, string Type, string? Result, string? Treatment, string Severity)> rows) =>
      Table(new[] { "No Lahan", "Nama", "NIK", "Luas Tanah", "Tanggal", typeHeader, "Kategori", "Hasil", "Penanganan", "Keterangan" }, rows.Select(i => new[] { i.Row.LandLegalNumberSnapshot ?? "", i.Row.FarmerNameSnapshot ?? "", i.Row.NikSnapshot ?? "", i.Row.LandAreaSnapshot?.ToString() ?? "", i.Row.ObservedOn.ToString("yyyy-MM-dd"), i.Type, i.Severity, i.Result ?? "", i.Treatment ?? "", i.Row.Notes ?? "" }));
  private static IEnumerable<string[]> Table(string[] header, IEnumerable<string[]> rows) => new[] { header }.Concat(rows);
  private static void ValidateZero(bool zero, int count, string noun) { if (zero && count > 0) throw new MonitoringValidationException($"Deklarasi tidak ada {noun} tidak boleh memiliki baris kejadian."); }

  private static byte[] CreateXlsx(IEnumerable<string[]> rows) {
    using var memory = new MemoryStream(); using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true)) {
      Write(zip, "[Content_Types].xml", "<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
      Write(zip, "_rels/.rels", "<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
      Write(zip, "xl/workbook.xml", "<?xml version=\"1.0\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Monitoring\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
      Write(zip, "xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
      var xml = new StringBuilder("<?xml version=\"1.0\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>"); int r = 1; foreach (var row in rows) { xml.Append($"<row r=\"{r}\">"); for (var c = 0; c < row.Length; c++) xml.Append($"<c r=\"{(char)('A' + c)}{r}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(row[c])}</t></is></c>"); xml.Append("</row>"); r++; } xml.Append("</sheetData></worksheet>"); Write(zip, "xl/worksheets/sheet1.xml", xml.ToString());
    } return memory.ToArray();
  }
  private static void Write(ZipArchive zip, string path, string content) { var entry = zip.CreateEntry(path); using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)); writer.Write(content); }
}
