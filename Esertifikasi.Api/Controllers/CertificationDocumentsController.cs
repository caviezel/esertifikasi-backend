using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models;
using Esertifikasi.Api.Models.Documents;
using Esertifikasi.Api.Security;
using Esertifikasi.Api.Services;
using Esertifikasi.Api.Services.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api.Controllers;

[ApiController, Route("api/certification-cycles/{cycleId:guid}/documents"), Authorize]
public sealed class CertificationDocumentsController : ControllerBase {
  private readonly AppDbContext _db;
  private readonly AccessService _access;
  private readonly DocumentUploadService _uploads;

  public CertificationDocumentsController(AppDbContext db, AccessService access, DocumentUploadService uploads) {
    _db = db;
    _access = access;
    _uploads = uploads;
  }

  [HttpGet]
  public async Task<IActionResult> List(Guid cycleId, CancellationToken ct) {
    if (!await _access.CanAccessCertificationCycleAsync(User, cycleId, ct)) return Forbid();
    var query = _db.Documents.Where(x => x.CertificationCycleId == cycleId);
    if (User.IsInRole(AppRoles.MemberTaniBaik)) query = query.Where(x => _db.Certificates.Any(c => c.DocumentId == x.Id));
    return Ok(await query.ToListItems().ToListAsync(ct));
  }

  [HttpPost, RequestSizeLimit(10 * 1024 * 1024 + 1024 * 1024)]
  public async Task<IActionResult> Upload(Guid cycleId, [FromForm] UploadDocumentRequest request, CancellationToken ct) {
    if (!await _access.CanManageCertificationCycleAsync(User, cycleId, ct)) return Forbid();
    var cycle = await _db.CertificationCycles.SingleOrDefaultAsync(x => x.Id == cycleId, ct);
    if (cycle is null) return NotFound();
    if (cycle.Status == CertificationCycleStatus.Active && cycle.CurrentPhase < CertificationPhase.CertificateIssuance)
      throw new CertificationWorkflowException(
          "Dokumen sertifikat baru dapat diunggah setelah Audit Eksternal selesai dan siklus dilanjutkan ke tahap Penerbitan Sertifikat.");
    if (cycle.CurrentPhase == CertificationPhase.Completed)
      throw new CertificationWorkflowException("Siklus sudah selesai sehingga dokumen sertifikat tidak dapat diubah.");
    CertificationWorkflowService.EnsurePhase(cycle, CertificationPhase.CertificateIssuance);
    var userId = AccessService.UserId(User);
    if (userId is null) return Unauthorized();
    var document = await _uploads.CreateAsync(DocumentOwnerType.CertificationCycle, cycleId, request, userId.Value, ct);
    return Created($"/api/documents/{document.Id}", new { document.Id });
  }

  [HttpGet("~/api/certification/participants/{participantId:guid}/documents")]
  public async Task<IActionResult> ParticipantDocuments(Guid participantId, CancellationToken ct) {
    var cycleId = await _db.CertificationParticipants.Where(x => x.Id == participantId).Select(x => (Guid?)x.CertificationCycleId).SingleOrDefaultAsync(ct);
    if (cycleId is null) return NotFound();
    if (!await _access.CanAccessCertificationCycleAsync(User, cycleId.Value, ct)) return Forbid();
    if (User.IsInRole(AppRoles.MemberTaniBaik) && !await _db.CertificationParticipants.AnyAsync(x => x.Id == participantId && x.Petani.ApplicationUserId == AccessService.UserId(User), ct)) return Forbid();
    return Ok(await _db.Documents.Where(x => x.CertificationParticipantId == participantId).ToListItems().ToListAsync(ct));
  }

  [HttpPost("~/api/certification/participants/{participantId:guid}/documents"), RequestSizeLimit(10 * 1024 * 1024 + 1024 * 1024)]
  public async Task<IActionResult> UploadParticipantDocument(Guid participantId, [FromForm] UploadDocumentRequest request, CancellationToken ct) {
    if (!await _access.CanManageParticipantPreparationAsync(User, participantId, ct)) return Forbid();
    var participant = await _db.CertificationParticipants.Include(x => x.CertificationCycle).SingleOrDefaultAsync(x => x.Id == participantId, ct);
    if (participant is null) return NotFound();
    EnsureActiveCycle(participant.CertificationCycle);
    if (!await IsParticipantInLatestDisclosureAsync(participant.CertificationCycleId, participantId, ct))
      return Conflict(new { message = "Dokumen hanya dapat dikelola untuk peserta yang diikutkan." });
    var document = await _uploads.CreateAsync(DocumentOwnerType.CertificationParticipant, participantId, request, AccessService.UserId(User)!.Value, ct);
    return Created($"/api/documents/{document.Id}", new { document.Id });
  }

  [HttpPost("~/api/certification/participants/{participantId:guid}/documents/attach")]
  public async Task<IActionResult> AttachParticipantDocument(Guid participantId, AttachMasterDocumentVersionRequest request, CancellationToken ct) {
    if (!await _access.CanManageParticipantPreparationAsync(User, participantId, ct)) return Forbid();
    var participant = await _db.CertificationParticipants.Include(x => x.CertificationCycle).SingleOrDefaultAsync(x => x.Id == participantId, ct);
    if (participant is null) return NotFound();
    EnsureActiveCycle(participant.CertificationCycle);
    if (!await IsParticipantInLatestDisclosureAsync(participant.CertificationCycleId, participantId, ct))
      return Conflict(new { message = "Dokumen hanya dapat dikelola untuk peserta yang diikutkan." });
    var document = await _uploads.AttachMasterVersionAsync(DocumentOwnerType.CertificationParticipant, participantId,
        request.DocumentVersionId, AccessService.UserId(User)!.Value, ct);
    return Created($"/api/documents/{document.Id}", new { document.Id });
  }

  [HttpGet("~/api/certification/participant-lahan/{participantLahanId:guid}/documents")]
  public async Task<IActionResult> ParticipantLahanDocuments(Guid participantLahanId, CancellationToken ct) {
    var participantId = await _db.CertificationParticipantLahan.Where(x => x.Id == participantLahanId).Select(x => (Guid?)x.CertificationParticipantId).SingleOrDefaultAsync(ct);
    if (participantId is null) return NotFound();
    var cycleId = await _db.CertificationParticipants.Where(x => x.Id == participantId).Select(x => x.CertificationCycleId).SingleAsync(ct);
    if (!await _access.CanAccessCertificationCycleAsync(User, cycleId, ct)) return Forbid();
    if (User.IsInRole(AppRoles.MemberTaniBaik) && !await _db.CertificationParticipants.AnyAsync(x => x.Id == participantId && x.Petani.ApplicationUserId == AccessService.UserId(User), ct)) return Forbid();
    return Ok(await _db.Documents.Where(x => x.CertificationParticipantLahanId == participantLahanId).ToListItems().ToListAsync(ct));
  }

  [HttpGet("~/api/certification-cycles/{cycleId:guid}/document-readiness")]
  public async Task<IActionResult> DocumentReadiness(Guid cycleId, CancellationToken ct) {
    if (!await _access.CanAccessCertificationCycleAsync(User, cycleId, ct)) return Forbid();
    var associationId = await _db.CertificationCycles.Where(x => x.Id == cycleId)
        .Select(x => (Guid?)x.AssociationId).SingleOrDefaultAsync(ct);
    if (associationId is null) return NotFound();
    var requirements = await _db.CycleDocumentRequirements.Where(x => x.CertificationCycleId == cycleId && x.IsRequired)
        .Select(x => new { x.DocumentTypeId, x.OwnerType, x.RequiredCount, Code = x.DocumentType.Code, Name = x.DocumentType.Nama }).ToListAsync(ct);
    var participants = await _db.CertificationParticipants.Where(x => x.CertificationCycleId == cycleId
        && _db.Set<DisclosureParticipant>().Any(dp => dp.CertificationParticipantId == x.Id
            && dp.Disclosure.Status == DisclosureStatus.Completed
            && dp.Disclosure.VersionNumber == _db.Disclosures.Where(d => d.CertificationCycleId == cycleId
                && d.Status == DisclosureStatus.Completed).Max(d => (int?)d.VersionNumber)))
        .Select(x => new { x.Id, x.PetaniId, Name = x.Petani.Nama }).ToListAsync(ct);
    var participantIds = participants.Select(x => x.Id).ToArray();
    var lahan = await _db.CertificationParticipantLahan.Where(x => participantIds.Contains(x.CertificationParticipantId)
        && _db.Set<DisclosureLahan>().Any(dl => dl.CertificationParticipantLahanId == x.Id
            && dl.Disclosure.Status == DisclosureStatus.Completed
            && dl.Disclosure.VersionNumber == _db.Disclosures.Where(d => d.CertificationCycleId == cycleId
                && d.Status == DisclosureStatus.Completed).Max(d => (int?)d.VersionNumber)))
        .Select(x => new { x.Id, x.CertificationParticipantId, x.LahanId, LegalNumber = x.Lahan.NoLegalitas }).ToListAsync(ct);
    var documents = await _db.Documents.Where(x => x.Status == DocumentStatus.Verified
        && (x.CertificationParticipantId != null && participantIds.Contains(x.CertificationParticipantId.Value)
            || x.PetaniId != null && participants.Select(p => p.PetaniId).Contains(x.PetaniId.Value)
            || x.CertificationParticipantLahanId != null && lahan.Select(l => l.Id).Contains(x.CertificationParticipantLahanId.Value)
            || x.LahanId != null && lahan.Select(l => l.LahanId).Contains(x.LahanId.Value)))
        .Select(x => new { x.DocumentTypeId, x.PetaniId, x.LahanId,
          x.CertificationParticipantId, x.CertificationParticipantLahanId }).ToListAsync(ct);
    var associationVerified = await _db.Documents.Where(x => x.AssociationId == associationId
        && x.Status == DocumentStatus.Verified).GroupBy(x => x.DocumentTypeId)
        .Select(x => new { TypeId = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.TypeId, x => x.Count, ct);

    var associationRequirements = requirements.Where(x => x.OwnerType == DocumentOwnerType.Association).ToList();
    var participantRequirements = requirements.Where(x => x.OwnerType == DocumentOwnerType.CertificationParticipant).ToList();
    var lahanRequirements = requirements.Where(x => x.OwnerType == DocumentOwnerType.CertificationParticipantLahan).ToList();
    return Ok(new {
      association = new {
        required = associationRequirements.Sum(x => x.RequiredCount),
        verified = associationRequirements.Sum(x => Math.Min(x.RequiredCount, associationVerified.GetValueOrDefault(x.DocumentTypeId)))
      },
      participants = participants.Select(owner => new {
        owner.Id, owner.PetaniId, owner.Name,
        required = participantRequirements.Sum(x => x.RequiredCount),
        verified = participantRequirements.Sum(req => Math.Min(req.RequiredCount,
            documents.Count(x => (x.CertificationParticipantId == owner.Id || x.PetaniId == owner.PetaniId)
                && x.DocumentTypeId == req.DocumentTypeId)))
      }),
      lahan = lahan.Select(owner => new {
        owner.Id, owner.CertificationParticipantId, owner.LahanId, owner.LegalNumber,
        required = lahanRequirements.Sum(x => x.RequiredCount),
        verified = lahanRequirements.Sum(req => Math.Min(req.RequiredCount,
            documents.Count(x => (x.CertificationParticipantLahanId == owner.Id || x.LahanId == owner.LahanId)
                && x.DocumentTypeId == req.DocumentTypeId)))
      })
    });
  }

  [HttpGet("~/api/certification-cycles/{cycleId:guid}/document-verification")]
  public async Task<IActionResult> DocumentVerification(Guid cycleId, CancellationToken ct) {
    if (!await _access.CanAccessCertificationCycleAsync(User, cycleId, ct)) return Forbid();
    var associationId = await _db.CertificationCycles.Where(x => x.Id == cycleId)
        .Select(x => (Guid?)x.AssociationId).SingleOrDefaultAsync(ct);
    if (associationId is null) return NotFound();

    var requirements = await _db.CycleDocumentRequirements.Where(x => x.CertificationCycleId == cycleId)
        .Select(x => new { x.Id, x.DocumentTypeId, Code = x.DocumentType.Code, Name = x.DocumentType.Nama,
          x.OwnerType, x.IsRequired, x.RequiredCount }).ToListAsync(ct);
    var participants = await _db.CertificationParticipants
        .Where(x => x.CertificationCycleId == cycleId
            && _db.Set<DisclosureParticipant>().Any(dp => dp.CertificationParticipantId == x.Id
                && dp.Disclosure.Status == DisclosureStatus.Completed
                && dp.Disclosure.VersionNumber == _db.Disclosures.Where(d => d.CertificationCycleId == cycleId
                    && d.Status == DisclosureStatus.Completed).Max(d => (int?)d.VersionNumber)))
        .Select(x => new { x.Id, x.PetaniId, PetaniName = x.Petani.Nama }).ToListAsync(ct);
    var participantIds = participants.Select(x => x.Id).ToArray();
    var lahan = await _db.CertificationParticipantLahan
        .Where(x => participantIds.Contains(x.CertificationParticipantId)
            && _db.Set<DisclosureLahan>().Any(dl => dl.CertificationParticipantLahanId == x.Id
                && dl.Disclosure.Status == DisclosureStatus.Completed
                && dl.Disclosure.VersionNumber == _db.Disclosures.Where(d => d.CertificationCycleId == cycleId
                    && d.Status == DisclosureStatus.Completed).Max(d => (int?)d.VersionNumber)))
        .Select(x => new { x.Id, ParticipantId = x.CertificationParticipantId, x.LahanId,
          PetaniName = x.CertificationParticipant.Petani.Nama, LegalNumber = x.Lahan.NoLegalitas }).ToListAsync(ct);
    var lahanIds = lahan.Select(x => x.Id).ToArray();
    var petaniIds = participants.Select(x => x.PetaniId).ToArray();
    var masterLahanIds = lahan.Select(x => x.LahanId).ToArray();
    var documents = await _db.Documents.Where(x => x.CertificationCycleId == cycleId
            || x.AssociationId == associationId
            || x.CertificationParticipantId != null && participantIds.Contains(x.CertificationParticipantId.Value)
            || x.PetaniId != null && petaniIds.Contains(x.PetaniId.Value)
            || x.CertificationParticipantLahanId != null && lahanIds.Contains(x.CertificationParticipantLahanId.Value)
            || x.LahanId != null && masterLahanIds.Contains(x.LahanId.Value))
        .Select(x => new { x.Id, x.DocumentTypeId, x.AssociationId, x.CertificationCycleId, x.CertificationParticipantId,
          x.CertificationParticipantLahanId, x.PetaniId, x.LahanId, x.Status, x.UploadedAt, x.ReviewedAt, x.RejectionReason,
          Version = x.Versions.OrderByDescending(v => v.VersionNumber)
              .Select(v => new { v.Id, v.OriginalFileName }).First() }).ToListAsync(ct);
    List<DocumentVerificationRequirement> Build(DocumentOwnerType ownerType, Guid? ownerId) =>
      requirements.Where(x => x.OwnerType == ownerType).Select(requirement => {
        var matches = documents.Where(x => x.DocumentTypeId == requirement.DocumentTypeId
            && (ownerType == DocumentOwnerType.Association && x.AssociationId == associationId
                || ownerType == DocumentOwnerType.CertificationCycle && x.CertificationCycleId == ownerId
                || ownerType == DocumentOwnerType.CertificationParticipant
                    && (x.CertificationParticipantId == ownerId
                        || x.PetaniId == participants.Single(p => p.Id == ownerId).PetaniId)
                || ownerType == DocumentOwnerType.CertificationParticipantLahan
                    && (x.CertificationParticipantLahanId == ownerId
                        || x.LahanId == lahan.Single(l => l.Id == ownerId).LahanId)))
            .ToList();
        var latest = matches.OrderByDescending(x => x.UploadedAt).FirstOrDefault();
        var latestStatus = latest is null ? DocumentVerificationStatus.Missing
            : Enum.Parse<DocumentVerificationStatus>(latest.Status.ToString());
        return new DocumentVerificationRequirement(requirement.Id, requirement.DocumentTypeId,
            requirement.Code, requirement.Name, requirement.IsRequired, requirement.RequiredCount,
            matches.Count(x => x.Status != DocumentStatus.Draft), matches.Count(x => x.Status == DocumentStatus.Verified),
            latestStatus, latest is null ? null : new DocumentVerificationSubmission(latest.Id, latest.Id,
                latest.Version.Id, latest.Version.OriginalFileName, latest.UploadedAt,
                latest.ReviewedAt, latest.RejectionReason));
      }).ToList();

    var association = new DocumentVerificationOwner(null, null, null, null, null, null,
        Build(DocumentOwnerType.Association, cycleId));
    var participantOwners = participants.Select(x => new DocumentVerificationOwner(
        x.Id, null, x.PetaniId, null, x.PetaniName, null,
        Build(DocumentOwnerType.CertificationParticipant, x.Id))).ToList();
    var lahanOwners = lahan.Select(x => new DocumentVerificationOwner(
        x.ParticipantId, x.Id, null, x.LahanId, x.PetaniName, x.LegalNumber,
        Build(DocumentOwnerType.CertificationParticipantLahan, x.Id))).ToList();
    var cycleOwner = new DocumentVerificationOwner(null, null, null, null, null, null,
        Build(DocumentOwnerType.CertificationCycle, cycleId));
    var all = association.Requirements.Concat(participantOwners.SelectMany(x => x.Requirements))
        .Concat(lahanOwners.SelectMany(x => x.Requirements)).Concat(cycleOwner.Requirements).ToList();
    var required = all.Where(x => x.IsRequired).Sum(x => x.RequiredCount);
    var submitted = all.Sum(x => Math.Min(x.RequiredCount, x.SubmittedCount));
    var verified = all.Sum(x => Math.Min(x.RequiredCount, x.VerifiedCount));
    var rejected = all.Count(x => x.Status == DocumentVerificationStatus.Rejected);
    return Ok(new DocumentVerificationResponse(
        new DocumentVerificationSummary(required, submitted, verified, rejected, Math.Max(0, required - submitted)),
        association, participantOwners, lahanOwners, cycleOwner));
  }

  [HttpPost("~/api/certification/participant-lahan/{participantLahanId:guid}/documents"), RequestSizeLimit(10 * 1024 * 1024 + 1024 * 1024)]
  public async Task<IActionResult> UploadParticipantLahanDocument(Guid participantLahanId, [FromForm] UploadDocumentRequest request, CancellationToken ct) {
    var participantId = await _db.CertificationParticipantLahan.Where(x => x.Id == participantLahanId).Select(x => (Guid?)x.CertificationParticipantId).SingleOrDefaultAsync(ct);
    if (participantId is null) return NotFound();
    if (!await _access.CanManageParticipantPreparationAsync(User, participantId.Value, ct)) return Forbid();
    var scope = await _db.CertificationParticipantLahan.Where(x => x.Id == participantLahanId)
        .Select(x => new { x.Status, CycleStatus = x.CertificationParticipant.CertificationCycle.Status,
          x.CertificationParticipant.CertificationCycle.IsCurrent,
          x.CertificationParticipant.CertificationCycleId,
          Phase = x.CertificationParticipant.CertificationCycle.CurrentPhase, ParticipantStatus = x.CertificationParticipant.Status }).SingleAsync(ct);
    if (!scope.IsCurrent) return HistoricalCycleConflict();
    EnsureActiveCycle(scope.IsCurrent, scope.CycleStatus);
    if (!await IsLahanInLatestDisclosureAsync(scope.CertificationCycleId, participantLahanId, ct))
      return Conflict(new { message = "Dokumen hanya dapat dikelola untuk peserta dan Lahan yang diikutkan." });
    var document = await _uploads.CreateAsync(DocumentOwnerType.CertificationParticipantLahan, participantLahanId, request, AccessService.UserId(User)!.Value, ct);
    return Created($"/api/documents/{document.Id}", new { document.Id });
  }

  [HttpPost("~/api/certification/participant-lahan/{participantLahanId:guid}/documents/attach")]
  public async Task<IActionResult> AttachParticipantLahanDocument(Guid participantLahanId, AttachMasterDocumentVersionRequest request, CancellationToken ct) {
    var participantId = await _db.CertificationParticipantLahan.Where(x => x.Id == participantLahanId)
        .Select(x => (Guid?)x.CertificationParticipantId).SingleOrDefaultAsync(ct);
    if (participantId is null) return NotFound();
    if (!await _access.CanManageParticipantPreparationAsync(User, participantId.Value, ct)) return Forbid();
    var scope = await _db.CertificationParticipantLahan.Where(x => x.Id == participantLahanId)
        .Select(x => new { x.Status, CycleStatus = x.CertificationParticipant.CertificationCycle.Status,
          x.CertificationParticipant.CertificationCycle.IsCurrent,
          x.CertificationParticipant.CertificationCycleId,
          Phase = x.CertificationParticipant.CertificationCycle.CurrentPhase, ParticipantStatus = x.CertificationParticipant.Status }).SingleAsync(ct);
    if (!scope.IsCurrent) return HistoricalCycleConflict();
    EnsureActiveCycle(scope.IsCurrent, scope.CycleStatus);
    if (!await IsLahanInLatestDisclosureAsync(scope.CertificationCycleId, participantLahanId, ct))
      return Conflict(new { message = "Dokumen hanya dapat dikelola untuk peserta dan Lahan yang diikutkan." });
    var document = await _uploads.AttachMasterVersionAsync(DocumentOwnerType.CertificationParticipantLahan, participantLahanId,
        request.DocumentVersionId, AccessService.UserId(User)!.Value, ct);
    return Created($"/api/documents/{document.Id}", new { document.Id });
  }

  private Task<bool> IsParticipantInLatestDisclosureAsync(Guid cycleId, Guid participantId, CancellationToken ct) =>
      _db.Set<DisclosureParticipant>().AnyAsync(x => x.CertificationParticipantId == participantId
          && x.Disclosure.Status == DisclosureStatus.Completed
          && x.Disclosure.VersionNumber == _db.Disclosures.Where(d => d.CertificationCycleId == cycleId
              && d.Status == DisclosureStatus.Completed).Max(d => (int?)d.VersionNumber), ct);

  private Task<bool> IsLahanInLatestDisclosureAsync(Guid cycleId, Guid participantLahanId, CancellationToken ct) =>
      _db.Set<DisclosureLahan>().AnyAsync(x => x.CertificationParticipantLahanId == participantLahanId
          && x.Disclosure.Status == DisclosureStatus.Completed
          && x.Disclosure.VersionNumber == _db.Disclosures.Where(d => d.CertificationCycleId == cycleId
              && d.Status == DisclosureStatus.Completed).Max(d => (int?)d.VersionNumber), ct);

  private ConflictObjectResult HistoricalCycleConflict() => Conflict(new {
    code = "CERTIFICATION_CYCLE_READ_ONLY", message = "Siklus historis hanya dapat dilihat."
  });

  private static void EnsureActiveCycle(CertificationCycle cycle) => EnsureActiveCycle(cycle.IsCurrent, cycle.Status);

  private static void EnsureActiveCycle(bool isCurrent, CertificationCycleStatus status) {
    if (!isCurrent) throw new CertificationWorkflowException("Siklus historis hanya dapat dilihat.", "CERTIFICATION_CYCLE_READ_ONLY");
    if (status != CertificationCycleStatus.Active) throw new CertificationWorkflowException("Siklus tidak aktif.");
  }
}
