using System.Security.Cryptography;
using System.Text.Json;
using Esertifikasi.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api.IntegrationTests.Regions;

public sealed class RegionDatasetImporterTests {
  [Fact]
  public async Task Import_ResolvesRelativeManifestFromParentDirectory() {
    var root = Path.Combine(Path.GetTempPath(), $"region-import-parent-{Guid.NewGuid():N}");
    var projectDirectory = Path.Combine(root, "Esertifikasi.Api");
    var dataDirectory = Path.Combine(root, "data", "regions");
    Directory.CreateDirectory(projectDirectory);
    Directory.CreateDirectory(dataDirectory);
    var originalDirectory = Directory.GetCurrentDirectory();
    try {
      var manifestPath = await WriteDatasetAsync(dataDirectory);
      Directory.SetCurrentDirectory(projectDirectory);
      var options = new DbContextOptionsBuilder<AppDbContext>()
          .UseInMemoryDatabase($"region-import-parent-{Guid.NewGuid():N}").Options;
      await using var db = new AppDbContext(options);
      await db.Database.EnsureCreatedAsync();

      var result = await new RegionDatasetImporter(db)
          .ImportAsync(Path.GetRelativePath(root, manifestPath), CancellationToken.None);

      Assert.False(result.AlreadyApplied);
      Assert.Equal(1, result.Villages);
    }
    finally {
      Directory.SetCurrentDirectory(originalDirectory);
      Directory.Delete(root, recursive: true);
    }
  }

  [Fact]
  public async Task Import_IsValidatedTransactionalAndIdempotent() {
    var directory = Path.Combine(Path.GetTempPath(), $"region-import-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try {
      var manifestPath = await WriteDatasetAsync(directory);
      var options = new DbContextOptionsBuilder<AppDbContext>()
          .UseInMemoryDatabase($"region-import-{Guid.NewGuid():N}").Options;
      await using var db = new AppDbContext(options);
      await db.Database.EnsureCreatedAsync();
      var importer = new RegionDatasetImporter(db);

      var first = await importer.ImportAsync(manifestPath, CancellationToken.None);
      var second = await importer.ImportAsync(manifestPath, CancellationToken.None);

      Assert.False(first.AlreadyApplied);
      Assert.True(second.AlreadyApplied);
      Assert.Equal(4, first.ChangedRecords);
      Assert.Equal("73.01.01.2001", (await db.Villages.SingleAsync()).Code);
      Assert.Single(await db.RegionDatasetImports.ToListAsync());
    }
    finally {
      Directory.Delete(directory, recursive: true);
    }
  }

  private static async Task<string> WriteDatasetAsync(string directory) {
    var files = new Dictionary<string, (string Name, string Content, int Count)> {
      ["provinces"] = ("provinces.csv", "id,code,name\n73,73,Sulawesi Selatan\n", 1),
      ["regencies"] = ("regencies.csv", "id,code,province_id,name\n7301,73.01,73,Kabupaten Kepulauan Selayar\n", 1),
      ["districts"] = ("districts.csv", "id,code,regency_id,name\n730101,73.01.01,7301,Bontomatene\n", 1),
      ["villages"] = ("villages.csv", "id,code,district_id,name\n7301012001,73.01.01.2001,730101,Batu Lekke\n", 1)
    };
    foreach (var file in files.Values) await File.WriteAllTextAsync(Path.Combine(directory, file.Name), file.Content);
    var fileMetadata = files.ToDictionary(x => x.Key, x => new {
      path = x.Value.Name,
      count = x.Value.Count,
      sha256 = Hash(Path.Combine(directory, x.Value.Name))
    });
    var fingerprintInput = string.Join('\n', new[] { "provinces", "regencies", "districts", "villages" }
        .Select(x => fileMetadata[x].sha256)) + "\n";
    var manifest = new {
      schemaVersion = 1,
      version = $"test-{Guid.NewGuid():N}",
      source = "Test dataset",
      generatedAt = DateTimeOffset.UtcNow,
      datasetSha256 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(fingerprintInput))).ToLowerInvariant(),
      files = fileMetadata
    };
    var manifestPath = Path.Combine(directory, "manifest.json");
    await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest));
    return manifestPath;
  }

  private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
