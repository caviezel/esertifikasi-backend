using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api.Services;

public sealed class AdministrativeRegionService {
  private readonly AppDbContext _db;

  public AdministrativeRegionService(AppDbContext db) => _db = db;

  public Task<bool> IsActiveVillageAsync(long villageId, CancellationToken ct) =>
      _db.Villages.AnyAsync(x => x.Id == villageId && x.IsActive
          && x.District.IsActive && x.District.Regency.IsActive && x.District.Regency.Province.IsActive, ct);
}

public sealed class RegionImportException : Exception {
  public RegionImportException(string message) : base(message) { }
}
