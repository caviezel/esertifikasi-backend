namespace Esertifikasi.Api.Domain.Entities;

public sealed class AssociationAdminAssignment {
  public Guid UserId { get; set; }
  public ApplicationUser User { get; set; } = null!;
  public Guid AssociationId { get; set; }
  public Association Association { get; set; } = null!;
}

public sealed class PoktanAdminAssignment {
  public Guid UserId { get; set; }
  public ApplicationUser User { get; set; } = null!;
  public Guid PoktanId { get; set; }
  public Poktan Poktan { get; set; } = null!;
}

public sealed class IcsAuditorAssignment {
  public Guid UserId { get; set; }
  public ApplicationUser User { get; set; } = null!;
  public Guid PoktanId { get; set; }
  public Poktan Poktan { get; set; } = null!;
}

public sealed class RefreshToken {
  public Guid Id { get; set; }
  public Guid UserId { get; set; }
  public ApplicationUser User { get; set; } = null!;
  public string TokenHash { get; set; } = string.Empty;
  public DateTimeOffset ExpiresAt { get; set; }
  public DateTimeOffset CreatedAt { get; set; }
  public DateTimeOffset? RevokedAt { get; set; }
}

public sealed class MemberRegistration {
  public Guid Id { get; set; }
  public Guid UserId { get; set; }
  public ApplicationUser User { get; set; } = null!;
  public Guid AssociationId { get; set; }
  public Association Association { get; set; } = null!;
  public Guid PoktanId { get; set; }
  public Poktan Poktan { get; set; } = null!;
  public Guid? ExistingPetaniId { get; set; }
  public Petani? ExistingPetani { get; set; }
  public string Provider { get; set; } = "TaniBaik";
  public string ProviderSubjectId { get; set; } = string.Empty;
  public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;
  public string Nama { get; set; } = string.Empty;
  public string? Nik { get; set; }
  public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
  public DateTimeOffset? ReviewedAt { get; set; }
  public Guid? ReviewedByUserId { get; set; }
  public ApplicationUser? ReviewedByUser { get; set; }
  public string? RejectionReason { get; set; }
}

public enum RegistrationStatus { Pending, Approved, Rejected }

public static class AppRoles {
  public const string SuperAdmin = "SuperAdmin";
  public const string AssociationAdmin = "AssociationAdmin";
  public const string PoktanAdmin = "PoktanAdmin";
  public const string IcsAuditor = "IcsAuditor";
  public const string MemberTaniBaik = "MemberTaniBaik";
  public static readonly string[] All = new[] { SuperAdmin, AssociationAdmin, PoktanAdmin, IcsAuditor, MemberTaniBaik };
}
