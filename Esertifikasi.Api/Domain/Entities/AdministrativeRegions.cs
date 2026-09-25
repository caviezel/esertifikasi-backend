namespace Esertifikasi.Api.Domain.Entities;

public sealed class Province {
  public long Id { get; set; }
  public string Code { get; set; } = string.Empty;
  public string Name { get; set; } = string.Empty;
  public bool IsActive { get; set; } = true;
  public DateTimeOffset SourceUpdatedAt { get; set; }
  public DateTimeOffset SyncedAt { get; set; } = DateTimeOffset.UtcNow;
  public ICollection<Regency> Regencies { get; set; } = new List<Regency>();
}

public sealed class Regency {
  public long Id { get; set; }
  public string Code { get; set; } = string.Empty;
  public long ProvinceId { get; set; }
  public Province Province { get; set; } = null!;
  public string Name { get; set; } = string.Empty;
  public bool IsActive { get; set; } = true;
  public DateTimeOffset SourceUpdatedAt { get; set; }
  public DateTimeOffset SyncedAt { get; set; } = DateTimeOffset.UtcNow;
  public ICollection<District> Districts { get; set; } = new List<District>();
}

public sealed class District {
  public long Id { get; set; }
  public string Code { get; set; } = string.Empty;
  public long RegencyId { get; set; }
  public Regency Regency { get; set; } = null!;
  public string Name { get; set; } = string.Empty;
  public bool IsActive { get; set; } = true;
  public DateTimeOffset SourceUpdatedAt { get; set; }
  public DateTimeOffset SyncedAt { get; set; } = DateTimeOffset.UtcNow;
  public ICollection<Village> Villages { get; set; } = new List<Village>();
}

public sealed class Village {
  public long Id { get; set; }
  public string Code { get; set; } = string.Empty;
  public long DistrictId { get; set; }
  public District District { get; set; } = null!;
  public string Name { get; set; } = string.Empty;
  public bool IsActive { get; set; } = true;
  public DateTimeOffset SourceUpdatedAt { get; set; }
  public DateTimeOffset SyncedAt { get; set; } = DateTimeOffset.UtcNow;
  public ICollection<Petani> Petani { get; set; } = new List<Petani>();
  public ICollection<Lahan> Lahan { get; set; } = new List<Lahan>();
}

public sealed class RegionDatasetImport {
  public Guid Id { get; set; }
  public string Version { get; set; } = string.Empty;
  public string Source { get; set; } = string.Empty;
  public string DatasetSha256 { get; set; } = string.Empty;
  public int ProvinceCount { get; set; }
  public int RegencyCount { get; set; }
  public int DistrictCount { get; set; }
  public int VillageCount { get; set; }
  public DateTimeOffset AppliedAt { get; set; } = DateTimeOffset.UtcNow;
}
