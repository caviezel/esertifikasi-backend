namespace Esertifikasi.Api.Domain.Entities;

public sealed class Poktan : ISoftDeletable {
  public Guid Id { get; set; }
  public Guid AssociationId { get; set; }
  public Association Association { get; set; } = null!;
  public string Nama { get; set; } = string.Empty;
  public int? JumlahPetaniPekebunSwadaya { get; set; }
  public decimal? BatasMaksimumLuasSawit { get; set; }
  public string? NomorKeanggotaanRspo { get; set; }
  public string Negara { get; set; } = "Indonesia";
  public long? ProvinceId { get; set; }
  public Province? Province { get; set; }
  public long? RegencyId { get; set; }
  public Regency? Regency { get; set; }
  public decimal? LuasAreaSawit { get; set; }
  public bool IsDeleted { get; set; }
  public DateTimeOffset? DeletedAt { get; set; }
  public ICollection<Petani> Petani { get; set; } = new List<Petani>();
  public ICollection<PoktanAdminAssignment> AdminAssignments { get; set; } = new List<PoktanAdminAssignment>();
  public ICollection<IcsAuditorAssignment> IcsAuditorAssignments { get; set; } = new List<IcsAuditorAssignment>();
}
