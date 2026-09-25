namespace Esertifikasi.Api.Domain.Entities;

public sealed class Lahan : ISoftDeletable {
  public Guid Id { get; set; }
  public Guid PetaniId { get; set; }
  public Petani Petani { get; set; } = null!;
  public string? NoLegalitas { get; set; }
  public string? NoSppl { get; set; }
  public string? NoStdb { get; set; }
  public string? JenisKepemilikan { get; set; }
  public int? TahunKepemilikan { get; set; }
  public string? StatusKepemilikan { get; set; }
  public string? KeteranganSertifikat { get; set; }
  public string? KemanaMenjualPanen { get; set; }
  public string? NamaPembeli { get; set; }
  public string? NamaPabrik { get; set; }
  public string? Komoditas { get; set; }
  public decimal? LuasLegalitas { get; set; }
  public string? BatasUtara { get; set; }
  public string? BatasTimur { get; set; }
  public string? BatasBarat { get; set; }
  public string? BatasSelatan { get; set; }
  public SoilType? JenisTanah { get; set; }
  public string? TanamanLain { get; set; }
  public DateOnly? BulanTahunTanam { get; set; }
  public decimal? ProduksiRataRata { get; set; }
  public string? TempatBeliPupuk { get; set; }
  public AvailabilityStatus? MitraPengelola { get; set; }
  public AvailabilityStatus? MitraPengelolaLainnya { get; set; }
  public int? JumlahPohonPerHa { get; set; }
  public CroppingPattern? PolaTanam { get; set; }
  public string? AsalBibit { get; set; }
  public string? JenisBibit { get; set; }
  public bool? BibitBersertifikat { get; set; }
  public string? BisnisLain { get; set; }
  public decimal? LuasTertanam { get; set; }
  public decimal? LuasProduktif { get; set; }
  public int? RotasiPanen { get; set; }
  public decimal? BeratTbs { get; set; }
  public long? DesaId { get; set; }
  public Village? Desa { get; set; }
  public string? RtRw { get; set; }
  public string? Alamat { get; set; }
  public string? BoundaryGeoJson { get; set; }
  public string? NoPetaLahan { get; set; }
  public string? MetodeBuka { get; set; }
  public string? DibukaOleh { get; set; }
  public int? TahunDibuka { get; set; }
  public string? TutupanLahan { get; set; }
  public string? TutupanLahanLainnya { get; set; }
  public LandAcquisition? PerolehanTanahGarapan { get; set; }
  public LandUse? PeruntukanTanah { get; set; }
  public string? TanamanAwal { get; set; }
  public bool? StatusHgu { get; set; }
  public bool? StatusGambutFeg { get; set; }
  public bool? StatusGambutKhg { get; set; }
  public decimal? LuasGeometri { get; set; }
  public decimal? LuasTerverifikasi { get; set; }
  public decimal? SelisihLuas { get; set; }
  public bool IsDeleted { get; set; }
  public DateTimeOffset? DeletedAt { get; set; }
  public ICollection<DocumentRecord> Documents { get; set; } = new List<DocumentRecord>();
  public ICollection<BaselineAssessment> BaselineAssessments { get; set; } = new List<BaselineAssessment>();
  public ICollection<CertificationParticipantLahan> CertificationParticipations { get; set; } = new List<CertificationParticipantLahan>();
}

public enum SoilType { Mineral, Berpasir, Gambut, TanahKeringDenganRawa }
public enum CroppingPattern { Monokultur, TumpangSari }
public enum AvailabilityStatus { Ada, TidakAda }
public enum LandAcquisition { Warisan, JualBeli, Transmigran, Wakaf, Perusahaan, Sewa }
public enum LandUse { Perkebunan, Hutan }
