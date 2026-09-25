namespace Esertifikasi.Api.Models;

public sealed record RegionItem(long Id, string Code, string Name);
public sealed record VillageResponse(
    long Id, string Code, string Name, long DistrictId, string DistrictCode, string DistrictName,
    long RegencyId, string RegencyCode, string RegencyName, long ProvinceId, string ProvinceCode, string ProvinceName, bool IsActive,
    DateTimeOffset SourceUpdatedAt, DateTimeOffset SyncedAt);
