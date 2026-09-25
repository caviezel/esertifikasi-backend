using Microsoft.AspNetCore.Identity;

namespace Esertifikasi.Api.Domain.Entities;

public sealed class ApplicationUser : IdentityUser<Guid> {
  public AccountStatus Status { get; set; } = AccountStatus.PendingApproval;
  public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
  public DateTimeOffset? LastLoginAt { get; set; }
  public Petani? Petani { get; set; }
  public ICollection<AssociationAdminAssignment> AssociationAssignments { get; set; } = new List<AssociationAdminAssignment>();
  public ICollection<PoktanAdminAssignment> PoktanAssignments { get; set; } = new List<PoktanAdminAssignment>();
  public ICollection<IcsAuditorAssignment> IcsAuditorAssignments { get; set; } = new List<IcsAuditorAssignment>();
}

public enum AccountStatus { PendingApproval, Active, Rejected, Suspended, Disabled }
