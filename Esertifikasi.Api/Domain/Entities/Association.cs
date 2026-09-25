namespace Esertifikasi.Api.Domain.Entities;

public sealed class Association : ISoftDeletable {
  public Guid Id { get; set; }
  public string Nama { get; set; } = string.Empty;
  public OrganizationLevel LevelOrganisasi { get; set; }
  public OrganizationType? JenisOrganisasi { get; set; }
  public string KetuaOrganisasi { get; set; } = string.Empty;
  public string Bendahara { get; set; } = string.Empty;
  public string SekretarisOrganisasi { get; set; } = string.Empty;
  public string Bidang { get; set; } = string.Empty;
  public string? NoTelp { get; set; }
  public long? ProvinceId { get; set; }
  public Province? Province { get; set; }
  public long? RegencyId { get; set; }
  public Regency? Regency { get; set; }
  public long? DistrictId { get; set; }
  public District? District { get; set; }
  public long? DesaId { get; set; }
  public Village? Desa { get; set; }
  public bool IsDeleted { get; set; }
  public DateTimeOffset? DeletedAt { get; set; }
  public ICollection<Poktan> Poktan { get; set; } = new List<Poktan>();
  public ICollection<AssociationAdminAssignment> AdminAssignments { get; set; } = new List<AssociationAdminAssignment>();
  public ICollection<CertificationCycle> CertificationCycles { get; set; } = new List<CertificationCycle>();
  public ICollection<DocumentRecord> Documents { get; set; } = new List<DocumentRecord>();
}

public enum OrganizationLevel { Provinsi, Kabupaten, Kecamatan, Desa }
public enum OrganizationType { Koperasi, Poktan, Gapoktan, Bumdes, KelompokTani }
