using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models.Documents;
using Esertifikasi.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api.Controllers;

[ApiController, Route("api/certification-cycles/{cycleId:guid}/association-documents"), Authorize]
public sealed class AssociationDocumentSubmissionsController : ControllerBase {
  private readonly AppDbContext _db;
  private readonly AccessService _access;

  public AssociationDocumentSubmissionsController(AppDbContext db, AccessService access) {
    _db = db;
    _access = access;
  }

  [HttpGet]
  public async Task<IActionResult> List(Guid cycleId, CancellationToken ct) {
    if (!await _access.CanAccessCertificationCycleAsync(User, cycleId, ct)) return Forbid();
    if (!await _db.CertificationCycles.AnyAsync(x => x.Id == cycleId, ct)) return NotFound();
    var requirements = await _db.CycleDocumentRequirements
        .Where(x => x.CertificationCycleId == cycleId && x.OwnerType == DocumentOwnerType.Association)
        .OrderBy(x => x.DocumentType.Nama)
        .Select(x => new { x.DocumentTypeId, x.DocumentType.Code, x.DocumentType.Nama, x.IsRequired, x.RequiredCount })
        .ToListAsync(ct);
    var submissions = await _db.AssociationDocumentSubmissions.Where(x => x.CertificationCycleId == cycleId)
        .Select(x => new AssociationDocumentSubmissionResponse(x.Id, x.DocumentTypeId, x.DocumentType.Code,
            x.DocumentType.Nama, x.DocumentId, x.DocumentVersionId, x.DocumentVersion.VersionNumber,
            x.DocumentVersion.OriginalFileName, x.Status, x.AttachedAt, x.SubmittedAt, x.ReviewedAt, x.RejectionReason))
        .ToListAsync(ct);
    var items = requirements.Select(requirement => new AssociationDocumentRequirementResponse(
        requirement.DocumentTypeId, requirement.Code, requirement.Nama, requirement.IsRequired,
        requirement.RequiredCount, submissions.SingleOrDefault(x => x.DocumentTypeId == requirement.DocumentTypeId))).ToList();
    var required = requirements.Where(x => x.IsRequired).Sum(x => x.RequiredCount);
    var submitted = items.Count(x => x.Submission?.Status is AssociationDocumentSubmissionStatus.Submitted or AssociationDocumentSubmissionStatus.Verified);
    var verified = items.Count(x => x.Submission?.Status == AssociationDocumentSubmissionStatus.Verified);
    return Ok(new AssociationCycleDocumentStatusResponse(required, submitted, verified,
        required == submitted, required == verified, items));
  }

  [HttpPost]
  public async Task<IActionResult> Attach(Guid cycleId, AttachAssociationDocumentRequest request, CancellationToken ct) {
    if (!await _access.CanManageCertificationCycleAsync(User, cycleId, ct)) return Forbid();
    var cycle = await _db.CertificationCycles.SingleOrDefaultAsync(x => x.Id == cycleId, ct);
    if (cycle is null) return NotFound();
    if (!cycle.IsCurrent) return HistoricalCycleConflict();
    var version = await _db.DocumentVersions
        .Where(x => x.Id == request.DocumentVersionId && x.Document.AssociationId == cycle.AssociationId)
        .Select(x => new { Version = x, x.DocumentId, x.Document.DocumentTypeId }).SingleOrDefaultAsync(ct);
    if (version is null) return ValidationProblem(new ValidationProblemDetails {
      Errors = { [nameof(request.DocumentVersionId)] = new[] { "Versi dokumen tidak ditemukan pada Association siklus ini." } }
    });
    var requirementExists = await _db.CycleDocumentRequirements.AnyAsync(x => x.CertificationCycleId == cycleId
        && x.OwnerType == DocumentOwnerType.Association && x.DocumentTypeId == version.DocumentTypeId, ct);
    if (!requirementExists) return ValidationProblem(new ValidationProblemDetails {
      Errors = { [nameof(request.DocumentVersionId)] = new[] { "Jenis dokumen ini bukan persyaratan Association untuk siklus tersebut." } }
    });
    var userId = AccessService.UserId(User)!.Value;
    var now = DateTimeOffset.UtcNow;
    var submission = await _db.AssociationDocumentSubmissions.SingleOrDefaultAsync(
        x => x.CertificationCycleId == cycleId && x.DocumentTypeId == version.DocumentTypeId, ct);
    if (submission is null) {
      submission = new AssociationDocumentSubmission {
        Id = Guid.NewGuid(), CertificationCycleId = cycleId, DocumentTypeId = version.DocumentTypeId,
        DocumentId = version.DocumentId, DocumentVersionId = request.DocumentVersionId,
        AttachedByUserId = userId, AttachedAt = now
      };
      _db.AssociationDocumentSubmissions.Add(submission);
    }
    else {
      if (submission.Status is AssociationDocumentSubmissionStatus.Submitted or AssociationDocumentSubmissionStatus.Verified)
        return Conflict(new { message = "Dokumen yang sudah diajukan atau diverifikasi tidak dapat diganti." });
      submission.DocumentId = version.DocumentId;
      submission.DocumentVersionId = request.DocumentVersionId;
      submission.Status = AssociationDocumentSubmissionStatus.Draft;
      submission.AttachedByUserId = userId;
      submission.AttachedAt = now;
      submission.SubmittedByUserId = null;
      submission.SubmittedAt = null;
      submission.ReviewedByUserId = null;
      submission.ReviewedAt = null;
      submission.RejectionReason = null;
    }
    await _db.SaveChangesAsync(ct);
    return Created($"/api/certification-cycles/{cycleId}/association-documents", new { submission.Id });
  }

  [HttpPost("submit")]
  public async Task<IActionResult> Submit(Guid cycleId, CancellationToken ct) {
    if (!await _access.CanManageCertificationCycleAsync(User, cycleId, ct)) return Forbid();
    var cycle = await _db.CertificationCycles.Where(x => x.Id == cycleId)
        .Select(x => new { x.IsCurrent }).SingleOrDefaultAsync(ct);
    if (cycle is null) return NotFound();
    if (!cycle.IsCurrent) return HistoricalCycleConflict();
    var requiredTypeIds = await _db.CycleDocumentRequirements.Where(x => x.CertificationCycleId == cycleId
        && x.OwnerType == DocumentOwnerType.Association && x.IsRequired).Select(x => x.DocumentTypeId).ToListAsync(ct);
    var submissions = await _db.AssociationDocumentSubmissions.Where(x => x.CertificationCycleId == cycleId).ToListAsync(ct);
    var missingIds = requiredTypeIds.Except(submissions.Where(x => x.Status != AssociationDocumentSubmissionStatus.Rejected)
        .Select(x => x.DocumentTypeId)).ToArray();
    if (missingIds.Length != 0) return Conflict(new { message = "Dokumen wajib Association belum lengkap.", missingDocumentTypeIds = missingIds });
    var now = DateTimeOffset.UtcNow;
    var userId = AccessService.UserId(User)!.Value;
    foreach (var submission in submissions.Where(x => x.Status == AssociationDocumentSubmissionStatus.Draft)) {
      submission.Status = AssociationDocumentSubmissionStatus.Submitted;
      submission.SubmittedByUserId = userId;
      submission.SubmittedAt = now;
    }
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  [HttpPost("{submissionId:guid}/verify"), Authorize(Roles = AppRoles.SuperAdmin)]
  public async Task<IActionResult> Verify(Guid cycleId, Guid submissionId, CancellationToken ct) {
    var submission = await _db.AssociationDocumentSubmissions.SingleOrDefaultAsync(
        x => x.Id == submissionId && x.CertificationCycleId == cycleId, ct);
    if (submission is null) return NotFound();
    if (!await _db.CertificationCycles.AnyAsync(x => x.Id == cycleId && x.IsCurrent, ct))
      return HistoricalCycleConflict();
    if (submission.Status != AssociationDocumentSubmissionStatus.Submitted)
      return Conflict(new { message = "Hanya dokumen berstatus Submitted yang dapat diverifikasi." });
    submission.Status = AssociationDocumentSubmissionStatus.Verified;
    submission.ReviewedByUserId = AccessService.UserId(User);
    submission.ReviewedAt = DateTimeOffset.UtcNow;
    submission.RejectionReason = null;
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  [HttpPost("{submissionId:guid}/reject"), Authorize(Roles = AppRoles.SuperAdmin)]
  public async Task<IActionResult> Reject(Guid cycleId, Guid submissionId, RejectDocumentRequest request, CancellationToken ct) {
    var submission = await _db.AssociationDocumentSubmissions.SingleOrDefaultAsync(
        x => x.Id == submissionId && x.CertificationCycleId == cycleId, ct);
    if (submission is null) return NotFound();
    if (!await _db.CertificationCycles.AnyAsync(x => x.Id == cycleId && x.IsCurrent, ct))
      return HistoricalCycleConflict();
    if (submission.Status != AssociationDocumentSubmissionStatus.Submitted)
      return Conflict(new { message = "Hanya dokumen berstatus Submitted yang dapat ditolak." });
    submission.Status = AssociationDocumentSubmissionStatus.Rejected;
    submission.ReviewedByUserId = AccessService.UserId(User);
    submission.ReviewedAt = DateTimeOffset.UtcNow;
    submission.RejectionReason = request.Reason.Trim();
    await _db.SaveChangesAsync(ct);
    return NoContent();
  }

  private ConflictObjectResult HistoricalCycleConflict() => Conflict(new {
    code = "CERTIFICATION_CYCLE_READ_ONLY", message = "Siklus historis hanya dapat dilihat."
  });
}
