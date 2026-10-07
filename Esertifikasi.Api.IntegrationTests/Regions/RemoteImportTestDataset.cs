using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Esertifikasi.Api.IntegrationTests.Regions;

internal sealed class RemoteImportTestDataset : IDisposable {
  public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), $"remote-region-test-{Guid.NewGuid():N}");
  public string ManifestPath => Path.Combine(DirectoryPath, "manifest.json");
  public async Task WriteAsync(string[]? villageNames = null, string? version = null, string provinceName = "Sulawesi Selatan") {
    Directory.CreateDirectory(DirectoryPath);
    villageNames ??= new[] { "Desa Satu", "Desa Dua" };
    string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    var files = new[] {
      (Key: "provinces", Content: $"id,code,name\n73,73,{Csv(provinceName)}\n", Count: 1),
      (Key: "regencies", Content: "id,code,province_id,name\n7301,73.01,73,Selayar\n", Count: 1),
      (Key: "districts", Content: "id,code,regency_id,name\n730101,73.01.01,7301,Bontomatene\n", Count: 1),
      (Key: "villages", Content: "id,code,district_id,name\n" + string.Concat(villageNames.Select((name, i) => $"730101{2001 + i:0000},73.01.01.{2001 + i:0000},730101,{Csv(name)}\n")), Count: villageNames.Length)
    };
    var hashes = new List<string>();
    var metadata = new Dictionary<string, object>();
    foreach (var file in files) {
      var path = file.Key + ".csv";
      await File.WriteAllTextAsync(Path.Combine(DirectoryPath, path), file.Content, new UTF8Encoding(false));
      var hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(DirectoryPath, path)))).ToLowerInvariant();
      hashes.Add(hash);
      metadata[file.Key] = new { path, count = file.Count, sha256 = hash };
    }
    await File.WriteAllTextAsync(ManifestPath, JsonSerializer.Serialize(new {
      schemaVersion = 1, version = version ?? $"remote-test-{Guid.NewGuid():N}", source = "Fixture 'quoted' \\ source 雨",
      generatedAt = new DateTimeOffset(2026, 9, 3, 5, 4, 7, TimeSpan.Zero),
      datasetSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', hashes) + "\n"))).ToLowerInvariant(),
      files = metadata
    }));
  }
  public void Dispose() { if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, true); }
}
