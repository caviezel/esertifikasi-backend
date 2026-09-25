using System.ComponentModel.DataAnnotations;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Security;
using Esertifikasi.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api.Controllers;

[ApiController, Route("api/registrations")]
public sealed class RegistrationsController : ControllerBase {
  private readonly AppDbContext _db;
  private readonly UserManager<ApplicationUser> _users;
  private readonly ITaniBaikVerifier _verifier;
  private readonly AccessService _access;
  private readonly CertificationWorkflowService _workflow;

  public RegistrationsController(AppDbContext db, UserManager<ApplicationUser> users, ITaniBaikVerifier verifier, AccessService access, CertificationWorkflowService workflow) {
    _db = db;
    _users = users;
    _verifier = verifier;
    _access = access;
    _workflow = workflow;
  }

  [AllowAnonymous, HttpPost("tani-baik")]
  public async Task<IActionResult> Register(RegisterTaniBaikRequest request, CancellationToken ct) {
    if (!await _verifier.VerifyAsync(request.ProviderSubjectId, ct)) {
      return BadRequest(new { message = "Keanggotaan Tani Baik tidak dapat diverifikasi." });
    }

    if (!await _db.Poktan.AnyAsync(x => x.Id == request.PoktanId && x.AssociationId == request.AssociationId, ct)) {
      return BadRequest(new { message = "Poktan tidak termasuk dalam Association yang dipilih." });
    }

    if (await _db.MemberRegistrations.AnyAsync(x => x.Provider == "TaniBaik" && x.ProviderSubjectId == request.ProviderSubjectId, ct)) {
      return Conflict(new { message = "Keanggotaan Tani Baik ini sudah terdaftar." });
    }

    var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = request.Email, Email = request.Email, EmailConfirmed = true, Status = AccountStatus.PendingApproval };
    var created = await _users.CreateAsync(user, request.Password);
    if (!created.Succeeded) {
      return ValidationProblem(new ValidationProblemDetails(created.Errors.GroupBy(x => x.Code).ToDictionary(x => x.Key, x => x.Select(e => e.Description).ToArray())));
    }

    var registration = new MemberRegistration { Id = Guid.NewGuid(), UserId = user.Id, AssociationId = request.AssociationId, PoktanId = request.PoktanId, ExistingPetaniId = request.ExistingPetaniId, ProviderSubjectId = request.ProviderSubjectId, Nama = request.Nama, Nik = request.Nik };
    _db.MemberRegistrations.Add(registration);
    await _db.SaveChangesAsync(ct);

    return Accepted(new { registration.Id, registration.Status });
  }

  [Authorize, HttpGet("pending")]
  public async Task<IActionResult> Pending(CancellationToken ct) {
    var rows = await _db.MemberRegistrations.Where(x => x.Status == RegistrationStatus.Pending)
        .Select(x => new { x.Id, x.AssociationId, Association = x.Association.Nama, x.PoktanId, Poktan = x.Poktan.Nama, x.Nama, x.Nik, x.SubmittedAt }).ToListAsync(ct);
    var allowed = new List<object>();
    foreach (var row in rows) {
      if (await _access.CanManageAssociationAsync(User, row.AssociationId, ct)) {
        allowed.Add(row);
      }
    }

    return Ok(allowed);
  }

  [Authorize, HttpPost("{id:guid}/approve")]
  public async Task<IActionResult> Approve(Guid id, CancellationToken ct) {
    var registration = await _db.MemberRegistrations.Include(x => x.User).SingleOrDefaultAsync(x => x.Id == id, ct);
    if (registration is null) {
      return NotFound();
    }

    if (!await _access.CanManageAssociationAsync(User, registration.AssociationId, ct)) {
      return Forbid();
    }

    if (registration.Status != RegistrationStatus.Pending) {
      return Conflict(new { message = "Pendaftaran ini sudah ditinjau." });
    }

    Petani? petani = null;
    if (registration.ExistingPetaniId is not null) {
      petani = await _db.Petani.SingleOrDefaultAsync(x => x.Id == registration.ExistingPetaniId && x.PoktanId == registration.PoktanId, ct);
    }

    if (registration.ExistingPetaniId is not null && petani is null) {
      return BadRequest(new { message = "Petani yang dipilih tidak berada dalam Poktan yang diminta." });
    }

    petani ??= new Petani { Id = Guid.NewGuid(), PoktanId = registration.PoktanId, Nama = registration.Nama, Nik = registration.Nik };
    if (petani.ApplicationUserId is not null) {
      return Conflict(new { message = "Petani sudah memiliki akun." });
    }

    if (_db.Entry(petani).State == EntityState.Detached) {
      _db.Petani.Add(petani);
    }

    petani.ApplicationUserId = registration.UserId;
    registration.Status = RegistrationStatus.Approved;
    registration.ReviewedAt = DateTimeOffset.UtcNow;
    registration.ReviewedByUserId = AccessService.UserId(User);
    registration.User.Status = AccountStatus.Active;
    var role = await _users.AddToRoleAsync(registration.User, AppRoles.MemberTaniBaik);
    if (!role.Succeeded) {
      return Problem(string.Join("; ", role.Errors.Select(x => x.Description)));
    }

    await _db.SaveChangesAsync(ct);

    var currentCycle = await _db.CertificationCycles.SingleOrDefaultAsync(x =>
        x.AssociationId == registration.AssociationId && x.IsCurrent && x.Status == CertificationCycleStatus.Active
        && x.CurrentPhase < CertificationPhase.InternalAudit, ct);
    if (currentCycle is not null && !await _db.CertificationParticipants.AnyAsync(x =>
        x.CertificationCycleId == currentCycle.Id && x.PetaniId == petani.Id, ct)
        && (!await _db.Disclosures.AnyAsync(x => x.CertificationCycleId == currentCycle.Id && x.Status == DisclosureStatus.Completed, ct)
            || await _db.Disclosures.AnyAsync(x => x.CertificationCycleId == currentCycle.Id && x.VersionNumber > 1
                && x.Status == DisclosureStatus.ApprovedForSecondDisclosure, ct))) {
      await _workflow.EnrollParticipantAsync(currentCycle, petani.Id, ParticipantEntryPath.New, Array.Empty<Guid>(), ct);
      await _db.SaveChangesAsync(ct);
    }

    return Ok(new { registration.Id, PetaniId = petani.Id, registration.Status });
  }

  [Authorize, HttpPost("{id:guid}/reject")]
  public async Task<IActionResult> Reject(Guid id, RejectRegistrationRequest request, CancellationToken ct) {
    var registration = await _db.MemberRegistrations.Include(x => x.User).SingleOrDefaultAsync(x => x.Id == id, ct);
    if (registration is null) {
      return NotFound();
    }

    if (!await _access.CanManageAssociationAsync(User, registration.AssociationId, ct)) {
      return Forbid();
    }

    if (registration.Status != RegistrationStatus.Pending) {
      return Conflict();
    }

    registration.Status = RegistrationStatus.Rejected;
    registration.RejectionReason = request.Reason;
    registration.ReviewedAt = DateTimeOffset.UtcNow;
    registration.ReviewedByUserId = AccessService.UserId(User);
    registration.User.Status = AccountStatus.Rejected;
    await _db.SaveChangesAsync(ct);

    return NoContent();
  }
}

public sealed record RegisterTaniBaikRequest(
    string Email,
    string Password,
    string ProviderSubjectId,
    Guid AssociationId,
    Guid PoktanId,
    Guid? ExistingPetaniId,
    string Nama,
    [property: Required, RegularExpression(@"^\d{16}$")] string Nik);

public sealed record RejectRegistrationRequest(string Reason);
