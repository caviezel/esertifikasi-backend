namespace Esertifikasi.Api.Domain.Entities;

public sealed class Petani : ISoftDeletable {
  public Guid Id { get; set; }
  public Guid PoktanId { get; set; }
  public Poktan Poktan { get; set; } = null!;
  public Guid? ApplicationUserId { get; set; }
  public ApplicationUser? ApplicationUser { get; set; }
  public string Nama { get; set; } = string.Empty;
  public string? Nik { get; set; }
  public string? TempatLahir { get; set; }
  public DateOnly? TanggalLahir { get; set; }
  public Gender? JenisKelamin { get; set; }
  public long? DesaId { get; set; }
  public Village? Desa { get; set; }
  public string? RtRw { get; set; }
  public string? Alamat { get; set; }
  public MaritalStatus? StatusPerkawinan { get; set; }
  public string? Pekerjaan { get; set; }
  public Citizenship? Kewarganegaraan { get; set; }
  public string? Suku { get; set; }
  public string? Email { get; set; }
  public string? NoTelepon { get; set; }
  public string? NoKk { get; set; }
  public string? NamaKepalaKeluarga { get; set; }
  public int? JumlahAnggotaKeluarga { get; set; }
  public int? JumlahAnak { get; set; }
  public string? PekerjaanSampingan { get; set; }
  public EducationLevel? PendidikanTerakhir { get; set; }
  public bool IsDeleted { get; set; }
  public DateTimeOffset? DeletedAt { get; set; }
  public ICollection<Lahan> Lahan { get; set; } = new List<Lahan>();
  public ICollection<DocumentRecord> Documents { get; set; } = new List<DocumentRecord>();
  public ICollection<CertificationParticipant> CertificationParticipations { get; set; } = new List<CertificationParticipant>();
  public ICollection<TrainingAttendance> TrainingAttendance { get; set; } = new List<TrainingAttendance>();
  public ICollection<MonitoringRecord> MonitoringRecords { get; set; } = new List<MonitoringRecord>();
}

public enum Gender { LakiLaki, Perempuan }
public enum Citizenship { Wni, Wna }
public enum MaritalStatus { BelumKawin, Kawin, KawinBelumTercatat, CeraiHidup, CeraiMati }
public enum EducationLevel { TidakTamatSd, Sd, Smp, Sma, D3, S1, S2, S3, Lainnya }
