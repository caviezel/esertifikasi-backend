using System.Security.Claims;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Security;
using Esertifikasi.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api.Services.Documents;

public sealed class DocumentAccessService {
  private readonly AppDbContext _db;
  private readonly AccessService _access;

  public DocumentAccessService(AppDbContext db, AccessService access) {
    _db = db;
    _access = access;
  }

  public async Task<bool> CanViewAsync(ClaimsPrincipal user, Guid documentId, CancellationToken ct) {
    var owner = await GetOwnerAsync(documentId, ct);
    if (owner is null) return false;
    if (owner.PetaniId is not null) return await _access.CanViewPetaniAsync(user, owner.PetaniId.Value, ct);
    if (owner.AssociationId is not null) return await _access.CanAccessAssociationAsync(user, owner.AssociationId.Value, ct);
    if (owner.CertificationCycleId is not null) {
      if (!await _access.CanAccessCertificationCycleAsync(user, owner.CertificationCycleId.Value, ct)) return false;
      return !user.IsInRole(AppRoles.MemberTaniBaik)
          || await _db.Certificates.AnyAsync(x => x.CertificationCycleId == owner.CertificationCycleId && x.DocumentId == documentId, ct);
    }
    if (owner.CertificationParticipantId is not null) {
      var participant = await _db.CertificationParticipants.Where(x => x.Id == owner.CertificationParticipantId)
          .Select(x => new { x.CertificationCycleId, x.Petani.ApplicationUserId }).SingleOrDefaultAsync(ct);
      return participant is not null && await _access.CanAccessCertificationCycleAsync(user, participant.CertificationCycleId, ct)
          && (!user.IsInRole(AppRoles.MemberTaniBaik) || participant.ApplicationUserId == AccessService.UserId(user));
    }
    if (owner.CertificationParticipantLahanId is not null) {
      var participant = await _db.CertificationParticipantLahan.Where(x => x.Id == owner.CertificationParticipantLahanId)
          .Select(x => new { x.CertificationParticipant.CertificationCycleId, x.CertificationParticipant.Petani.ApplicationUserId }).SingleOrDefaultAsync(ct);
      return participant is not null && await _access.CanAccessCertificationCycleAsync(user, participant.CertificationCycleId, ct)
          && (!user.IsInRole(AppRoles.MemberTaniBaik) || participant.ApplicationUserId == AccessService.UserId(user));
    }

    var petaniId = await _db.Lahan.Where(x => x.Id == owner.LahanId).Select(x => (Guid?)x.PetaniId).SingleOrDefaultAsync(ct);
    return petaniId is not null && await _access.CanViewPetaniAsync(user, petaniId.Value, ct);
  }

  public async Task<bool> CanManageAsync(ClaimsPrincipal user, Guid documentId, CancellationToken ct) {
    var associationId = await _db.Documents.Where(x => x.Id == documentId).Select(x => x.AssociationId).SingleOrDefaultAsync(ct);
    if (associationId is not null) return await _access.CanManageAssociationAsync(user, associationId.Value, ct);
    var cycleId = await _db.Documents.Where(x => x.Id == documentId).Select(x => x.CertificationCycleId).SingleOrDefaultAsync(ct);
    if (cycleId is not null) return await _access.CanManageCertificationCycleAsync(user, cycleId.Value, ct);
    var participantId = await _db.Documents.Where(x => x.Id == documentId).Select(x => x.CertificationParticipantId).SingleOrDefaultAsync(ct);
    if (participantId is not null) return await _access.CanManageParticipantPreparationAsync(user, participantId.Value, ct);
    var participantLahanId = await _db.Documents.Where(x => x.Id == documentId).Select(x => x.CertificationParticipantLahanId).SingleOrDefaultAsync(ct);
    if (participantLahanId is not null) {
      participantId = await _db.CertificationParticipantLahan.Where(x => x.Id == participantLahanId).Select(x => (Guid?)x.CertificationParticipantId).SingleOrDefaultAsync(ct);
      return participantId is not null && await _access.CanManageParticipantPreparationAsync(user, participantId.Value, ct);
    }
    var poktanId = await GetPoktanIdAsync(documentId, ct);
    return poktanId is not null && await _access.CanManagePoktanAsync(user, poktanId.Value, ct);
  }

  public async Task<bool> CanReviewAsync(ClaimsPrincipal user, Guid documentId, CancellationToken ct) {
    var documentAssociationId = await _db.Documents.Where(x => x.Id == documentId).Select(x => x.AssociationId).SingleOrDefaultAsync(ct);
    if (documentAssociationId is not null) return _access.IsSuperAdmin(user);
    var cycleId = await _db.Documents.Where(x => x.Id == documentId).Select(x => x.CertificationCycleId).SingleOrDefaultAsync(ct);
    if (cycleId is not null) return await _access.CanManageCertificationCycleAsync(user, cycleId.Value, ct);
    var participantCycleId = await _db.Documents.Where(x => x.Id == documentId)
        .Select(x => x.CertificationParticipantId != null ? (Guid?)x.CertificationParticipant!.CertificationCycleId
            : x.CertificationParticipantLahanId != null ? x.CertificationParticipantLahan!.CertificationParticipant.CertificationCycleId : null)
        .SingleOrDefaultAsync(ct);
    if (participantCycleId is not null) return await _access.CanManageCertificationCycleAsync(user, participantCycleId.Value, ct);
    var associationId = await GetAssociationIdAsync(documentId, ct);
    return associationId is not null && await _access.CanManageAssociationAsync(user, associationId.Value, ct);
  }

  public async Task<bool> CanManagePetaniAsync(ClaimsPrincipal user, Guid petaniId, CancellationToken ct) {
    var poktanId = await _db.Petani.Where(x => x.Id == petaniId).Select(x => (Guid?)x.PoktanId).SingleOrDefaultAsync(ct);
    return poktanId is not null && await _access.CanManagePoktanAsync(user, poktanId.Value, ct);
  }

  public async Task<bool> CanManageLahanAsync(ClaimsPrincipal user, Guid lahanId, CancellationToken ct) {
    var poktanId = await _db.Lahan.Where(x => x.Id == lahanId).Select(x => (Guid?)x.Petani.PoktanId).SingleOrDefaultAsync(ct);
    return poktanId is not null && await _access.CanManagePoktanAsync(user, poktanId.Value, ct);
  }

  public async Task EnsureCertificationDocumentMutableAsync(Guid documentId, CancellationToken ct) {
    var scope = await _db.Documents.Where(x => x.Id == documentId).Select(x => new {
      x.CertificationCycleId,
      ParticipantCycleId = x.CertificationParticipantId != null
          ? (Guid?)x.CertificationParticipant!.CertificationCycleId
          : x.CertificationParticipantLahanId != null
              ? x.CertificationParticipantLahan!.CertificationParticipant.CertificationCycleId
              : null
    }).SingleOrDefaultAsync(ct);
    if (scope is null) throw new KeyNotFoundException("Dokumen tidak ditemukan.");
    var cycleId = scope.CertificationCycleId ?? scope.ParticipantCycleId;
    if (cycleId is null) return;
    var cycle = await _db.CertificationCycles.Where(x => x.Id == cycleId)
        .Select(x => new { x.IsCurrent, x.Status, x.CurrentPhase }).SingleAsync(ct);
    if (!cycle.IsCurrent)
      throw new CertificationWorkflowException("Siklus historis hanya dapat dilihat.", "CERTIFICATION_CYCLE_READ_ONLY");
    if (cycle.Status != CertificationCycleStatus.Active)
      throw new CertificationWorkflowException("Siklus tidak aktif.");
  }

  private async Task<DocumentOwner?> GetOwnerAsync(Guid documentId, CancellationToken ct) {
    return await _db.Documents.Where(x => x.Id == documentId)
        .Select(x => new DocumentOwner(x.PetaniId, x.LahanId, x.AssociationId, x.CertificationCycleId, x.CertificationParticipantId, x.CertificationParticipantLahanId))
        .SingleOrDefaultAsync(ct);
  }

  private async Task<Guid?> GetPoktanIdAsync(Guid documentId, CancellationToken ct) {
      return await _db.Documents.Where(x => x.Id == documentId)
        .Where(x => x.CertificationCycleId == null)
        .Select(x => x.PetaniId != null ? (Guid?)x.Petani!.PoktanId : x.Lahan!.Petani.PoktanId)
        .SingleOrDefaultAsync(ct);
  }

  private async Task<Guid?> GetAssociationIdAsync(Guid documentId, CancellationToken ct) {
    return await _db.Documents.Where(x => x.Id == documentId)
        .Select(x => x.AssociationId != null ? x.AssociationId
            : x.CertificationCycleId != null ? (Guid?)x.CertificationCycle!.AssociationId
            : x.PetaniId != null ? x.Petani!.Poktan.AssociationId : x.Lahan!.Petani.Poktan.AssociationId)
        .SingleOrDefaultAsync(ct);
  }

  private sealed record DocumentOwner(Guid? PetaniId, Guid? LahanId, Guid? AssociationId, Guid? CertificationCycleId, Guid? CertificationParticipantId, Guid? CertificationParticipantLahanId);
}
