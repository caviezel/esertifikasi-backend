using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api.Services;

public sealed class CertificationReadinessService {
  private readonly AppDbContext _db;

  public CertificationReadinessService(AppDbContext db) => _db = db;

  public async Task<CertificationReadinessResponse?> EvaluateNextAsync(Guid cycleId, CancellationToken ct) {
    var cycle = await _db.CertificationCycles.AsNoTracking().Where(x => x.Id == cycleId)
        .Select(x => new { x.Id, x.Status, x.CurrentPhase }).SingleOrDefaultAsync(ct);
    if (cycle is null) return null;
    if (cycle.Status != CertificationCycleStatus.Active || cycle.CurrentPhase == CertificationPhase.Completed)
      return new(cycle.Id, cycle.CurrentPhase, null, false,
          new[] { new CertificationReadinessBlocker("CYCLE_NOT_ACTIVE", "Siklus tidak aktif atau telah selesai.") });
    var next = (CertificationPhase)((int)cycle.CurrentPhase + 1);
    var blockers = await EvaluateAsync(cycleId, next, ct);
    return new(cycle.Id, cycle.CurrentPhase, next, blockers.Count == 0, blockers);
  }

  public async Task<IReadOnlyList<CertificationReadinessBlocker>> EvaluateAsync(
      Guid cycleId, CertificationPhase target, CancellationToken ct) {
    var blockers = new List<CertificationReadinessBlocker>();
    if (target >= CertificationPhase.Preparation) await CheckDisclosureAsync(cycleId, blockers, ct);
    if (target >= CertificationPhase.InternalAudit) await CheckPreparationAsync(cycleId, blockers, ct);
    if (target >= CertificationPhase.ExternalAudit) await CheckAuditAsync(cycleId, AuditType.Internal, blockers, ct);
    if (target >= CertificationPhase.CertificateIssuance) await CheckAuditAsync(cycleId, AuditType.External, blockers, ct);
    if (target >= CertificationPhase.Completed
        && !await _db.Certificates.AnyAsync(x => x.CertificationCycleId == cycleId, ct))
      blockers.Add(new("CERTIFICATE_NOT_ISSUED", "Sertifikat dan berkas sertifikat belum diterbitkan."));
    return blockers;
  }

  private async Task CheckDisclosureAsync(Guid cycleId, List<CertificationReadinessBlocker> blockers, CancellationToken ct) {
    var latestStatus = await _db.Disclosures.Where(x => x.CertificationCycleId == cycleId)
        .OrderByDescending(x => x.VersionNumber).Select(x => (DisclosureStatus?)x.Status).FirstOrDefaultAsync(ct);
    if (latestStatus != DisclosureStatus.Completed)
      blockers.Add(new("DISCLOSURE_INCOMPLETE", "Disclosure belum diselesaikan."));
  }

  private async Task CheckPreparationAsync(Guid cycleId, List<CertificationReadinessBlocker> blockers, CancellationToken ct) {
    if (!await ActiveParticipants(cycleId).AnyAsync(ct)) {
      blockers.Add(new("NO_ACTIVE_PARTICIPANTS", "Tidak ada Petani aktif dalam lingkup sertifikasi."));
      return;
    }
    // Documents and training are intentionally reviewed when certificate recipients
    // are selected. They run in parallel with audits and do not block Internal Audit.
  }

  private async Task CheckAssociationDocumentsAsync(Guid cycleId, List<CertificationReadinessBlocker> blockers, CancellationToken ct) {
    var required = await Requirements(cycleId, DocumentOwnerType.Association).ToListAsync(ct);
    var associationId = await _db.CertificationCycles.Where(x => x.Id == cycleId).Select(x => x.AssociationId).SingleAsync(ct);
    var counts = await _db.Documents.Where(x => x.AssociationId == associationId && x.Status == DocumentStatus.Verified)
        .GroupBy(x => x.DocumentTypeId).Select(x => new { Id = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
    AddCount(blockers, "ASSOCIATION_DOCUMENTS_INCOMPLETE",
        required.Sum(x => Math.Max(0, x.RequiredCount - counts.GetValueOrDefault(x.DocumentTypeId))),
        "dokumen wajib Association belum diverifikasi.");
  }

  private async Task CheckParticipantDocumentsAsync(Guid cycleId, List<CertificationReadinessBlocker> blockers, CancellationToken ct) {
    var required = await Requirements(cycleId, DocumentOwnerType.CertificationParticipant).ToListAsync(ct);
    var owners = await ActiveParticipants(cycleId).Select(x => new { x.Id, x.PetaniId }).ToListAsync(ct);
    if (required.Count == 0 || owners.Count == 0) return;
    var ownerIds = owners.Select(x => x.Id).ToArray();
    var petaniIds = owners.Select(x => x.PetaniId).ToArray();
    var documents = await _db.Documents.Where(x => x.Status == DocumentStatus.Verified
        && (x.CertificationParticipantId != null && ownerIds.Contains(x.CertificationParticipantId.Value)
            || x.PetaniId != null && petaniIds.Contains(x.PetaniId.Value)))
        .Select(x => new { x.CertificationParticipantId, x.PetaniId, x.DocumentTypeId }).ToListAsync(ct);
    var counts = documents.Select(x => new {
      OwnerId = x.CertificationParticipantId ?? owners.Single(o => o.PetaniId == x.PetaniId).Id,
      x.DocumentTypeId
    }).GroupBy(x => new { x.OwnerId, x.DocumentTypeId })
        .Select(x => new { x.Key.OwnerId, x.Key.DocumentTypeId, Count = x.Count() }).ToList();
    var lookup = counts.ToDictionary(x => (x.OwnerId, x.DocumentTypeId), x => x.Count);
    AddCount(blockers, "PARTICIPANT_DOCUMENTS_INCOMPLETE", owners.Sum(owner => required.Sum(x =>
        Math.Max(0, x.RequiredCount - lookup.GetValueOrDefault((owner.Id, x.DocumentTypeId))))),
        "dokumen wajib peserta belum diverifikasi.");
  }

  private async Task CheckLahanDocumentsAsync(Guid cycleId, List<CertificationReadinessBlocker> blockers, CancellationToken ct) {
    var required = await Requirements(cycleId, DocumentOwnerType.CertificationParticipantLahan).ToListAsync(ct);
    var owners = await _db.CertificationParticipantLahan.Where(x => x.CertificationParticipant.CertificationCycleId == cycleId
        && _db.Set<DisclosureLahan>().Any(dl => dl.CertificationParticipantLahanId == x.Id
            && dl.Disclosure.Status == DisclosureStatus.Completed
            && dl.Disclosure.VersionNumber == _db.Disclosures.Where(d => d.CertificationCycleId == cycleId
                && d.Status == DisclosureStatus.Completed).Max(d => (int?)d.VersionNumber)))
        .Select(x => new { x.Id, x.LahanId }).ToListAsync(ct);
    if (required.Count == 0 || owners.Count == 0) return;
    var ownerIds = owners.Select(x => x.Id).ToArray();
    var lahanIds = owners.Select(x => x.LahanId).ToArray();
    var documents = await _db.Documents.Where(x => x.Status == DocumentStatus.Verified
        && (x.CertificationParticipantLahanId != null && ownerIds.Contains(x.CertificationParticipantLahanId.Value)
            || x.LahanId != null && lahanIds.Contains(x.LahanId.Value)))
        .Select(x => new { x.CertificationParticipantLahanId, x.LahanId, x.DocumentTypeId }).ToListAsync(ct);
    var counts = documents.Select(x => new {
      OwnerId = x.CertificationParticipantLahanId ?? owners.Single(o => o.LahanId == x.LahanId).Id,
      x.DocumentTypeId
    }).GroupBy(x => new { x.OwnerId, x.DocumentTypeId })
        .Select(x => new { x.Key.OwnerId, x.Key.DocumentTypeId, Count = x.Count() }).ToList();
    var lookup = counts.ToDictionary(x => (x.OwnerId, x.DocumentTypeId), x => x.Count);
    AddCount(blockers, "LAHAN_DOCUMENTS_INCOMPLETE", owners.Sum(owner => required.Sum(x =>
        Math.Max(0, x.RequiredCount - lookup.GetValueOrDefault((owner.Id, x.DocumentTypeId))))),
        "dokumen wajib Lahan peserta belum diverifikasi.");
  }


  private async Task CheckAuditAsync(Guid cycleId, AuditType type, List<CertificationReadinessBlocker> blockers, CancellationToken ct) {
    var audit = await _db.CertificationAudits.Where(x => x.CertificationCycleId == cycleId && x.Type == type)
        .Select(x => new { x.Status, x.HasFindings, Open = x.Findings.Count(f => f.Status != FindingStatus.Closed) }).SingleOrDefaultAsync(ct);
    var label = type == AuditType.Internal ? "internal" : "eksternal";
    if (audit is null) blockers.Add(new($"{type.ToString().ToUpperInvariant()}_AUDIT_MISSING", $"Audit {label} belum dibuat."));
    else if (audit.Open > 0) AddCount(blockers, $"{type.ToString().ToUpperInvariant()}_FINDINGS_OPEN", audit.Open, $"temuan audit {label} belum ditutup.");
    else if (type == AuditType.Internal && audit.Status != AuditStatus.Closed) blockers.Add(new("INTERNAL_AUDIT_INCOMPLETE", "Audit internal belum ditutup."));
    else if (type == AuditType.External && audit.Status != AuditStatus.Closed) blockers.Add(new("EXTERNAL_AUDIT_INCOMPLETE", "Audit eksternal belum ditutup."));
    if (type == AuditType.External && audit is not null) {
      var reportUploaded = await _db.Documents.AnyAsync(x => x.CertificationCycleId == cycleId
          && x.DocumentType.Code == "LAPORAN_AUDIT_EKSTERNAL", ct);
      if (!reportUploaded) blockers.Add(new("EXTERNAL_AUDIT_REPORT_MISSING", "Laporan audit eksternal belum diunggah."));
      if (audit.HasFindings == true && !await _db.Documents.AnyAsync(x => x.CertificationCycleId == cycleId
          && x.DocumentType.Code == "LAPORAN_PENUTUPAN_TEMUAN_EKSTERNAL", ct))
        blockers.Add(new("EXTERNAL_AUDIT_CLOSURE_REPORT_MISSING", "Laporan penutupan temuan audit eksternal belum diunggah."));
    }
  }

  private IQueryable<CertificationParticipant> ActiveParticipants(Guid id) => _db.CertificationParticipants.Where(x =>
      x.CertificationCycleId == id && _db.Set<DisclosureParticipant>().Any(dp => dp.CertificationParticipantId == x.Id
          && dp.Disclosure.Status == DisclosureStatus.Completed
          && dp.Disclosure.VersionNumber == _db.Disclosures.Where(d => d.CertificationCycleId == id
              && d.Status == DisclosureStatus.Completed).Max(d => (int?)d.VersionNumber)));
  private IQueryable<CycleDocumentRequirement> Requirements(Guid id, DocumentOwnerType owner) => _db.CycleDocumentRequirements.Where(x =>
      x.CertificationCycleId == id && x.OwnerType == owner && x.IsRequired);
  private static void AddCount(List<CertificationReadinessBlocker> blockers, string code, int count, string message) {
    if (count > 0) blockers.Add(new(code, $"{count} {message}", count));
  }
}
