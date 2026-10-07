using System.Text.Json;

namespace Esertifikasi.Api.Services;

public sealed partial class RegionDatasetImporter {
  // Reuse the direct importer's parser and validation without constructing a DbContext.
  public static async Task<ValidatedRegionDataset> ReadValidatedDatasetAsync(string manifestPath, CancellationToken ct) {
    var path = ResolveManifestPath(manifestPath);
    if (!File.Exists(path)) throw new RegionImportException($"Manifest tidak ditemukan: {path}");
    var manifest = JsonSerializer.Deserialize<RegionDatasetManifest>(await File.ReadAllBytesAsync(path, ct),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new RegionImportException("Manifest tidak dapat dibaca.");
    ValidateManifest(manifest);
    var directory = Path.GetDirectoryName(path)!;
    var provinces = await ReadCsvAsync(directory, manifest.Files.Provinces,
        new[] { "id", "code", "name" }, ParseProvince, ct);
    var regencies = await ReadCsvAsync(directory, manifest.Files.Regencies,
        new[] { "id", "code", "province_id", "name" }, ParseRegency, ct);
    var districts = await ReadCsvAsync(directory, manifest.Files.Districts,
        new[] { "id", "code", "regency_id", "name" }, ParseDistrict, ct);
    var villages = await ReadCsvAsync(directory, manifest.Files.Villages,
        new[] { "id", "code", "district_id", "name" }, ParseVillage, ct);
    ValidateHierarchy(provinces, regencies, districts, villages);
    return new ValidatedRegionDataset(manifest,
        provinces.Values.Select(x => new RegionArtifactRow(x.Id, x.Code, x.Name)).ToArray(),
        regencies.Values.Select(x => new RegionArtifactRow(x.Id, x.Code, x.Name, x.ProvinceId)).ToArray(),
        districts.Values.Select(x => new RegionArtifactRow(x.Id, x.Code, x.Name, x.RegencyId)).ToArray(),
        villages.Values.Select(x => new RegionArtifactRow(x.Id, x.Code, x.Name, x.DistrictId)).ToArray());
  }
}

public sealed record RegionArtifactRow(long Id, string Code, string Name, long? ParentId = null);
public sealed record ValidatedRegionDataset(RegionDatasetManifest Manifest,
    IReadOnlyList<RegionArtifactRow> Provinces, IReadOnlyList<RegionArtifactRow> Regencies,
    IReadOnlyList<RegionArtifactRow> Districts, IReadOnlyList<RegionArtifactRow> Villages);
