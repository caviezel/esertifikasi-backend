using System.Collections.Concurrent;
using Esertifikasi.Api.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Esertifikasi.Api.IntegrationTests.Regions;

public sealed class PostgresRegionSqlArtifactTests {
  public sealed class PsqlFactAttribute : FactAttribute {
    public PsqlFactAttribute() {
      if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REGION_IMPORT_TEST_POSTGRES")) ||
          string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REGION_IMPORT_TEST_PSQL")))
        Skip = "Set REGION_IMPORT_TEST_POSTGRES and REGION_IMPORT_TEST_PSQL for isolated real psql tests.";
    }
  }

  [PsqlFact]
  public async Task Artifact_RoundTripsTextUpsertsDeactivatesAndIsIdempotent() {
    await using var test = await PostgresRegionDatasetImporterTests.TestDatabase.CreateAsync();
    using var source = new RemoteImportTestDataset();
    const string text = "O'Brien \"quoted\" 雨\\N\\.\tTab\nLine\rEnd; $(id)";
    await source.WriteAsync(new[] { text, "Missing" });
    var artifact = await Generate(source.ManifestPath);
    try {
      source.Dispose(); // The artifact is genuinely self-contained.
      var first = await Execute(test, artifact.Path);
      Assert.Equal(0, first.Exit);
      Assert.Contains(RegionSqlArtifactGenerator.CommitMarker, first.Output);
      Assert.Contains("ChangedRecords=5", string.Join('\n', first.Errors));
      var before = await test.Db.Villages.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
      Assert.Equal(text, before[0].Name);
      var second = await Execute(test, artifact.Path);
      Assert.Equal(0, second.Exit);
      Assert.Contains("ChangedRecords=0", string.Join('\n', second.Errors));
      Assert.Equal(1, await test.Db.RegionDatasetImports.CountAsync());
      Assert.Equal(before[0].SyncedAt, (await test.Db.Villages.AsNoTracking().SingleAsync(x => x.Id == before[0].Id)).SyncedAt);
      await source.WriteAsync(new[] { "Renamed" });
      var update = await Generate(source.ManifestPath);
      try {
        var result = await Execute(test, update.Path);
        Assert.Equal(0, result.Exit);
        Assert.Contains("ChangedRecords=2", string.Join('\n', result.Errors));
        var villages = await test.Db.Villages.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        Assert.Equal("Renamed", villages[0].Name);
        Assert.True(villages[0].IsActive);
        Assert.False(villages[1].IsActive);
        Assert.Equal(before[1].SourceUpdatedAt, villages[1].SourceUpdatedAt);
      }
      finally { File.Delete(update.Path); }
    }
    finally { File.Delete(artifact.Path); }
  }

  [PsqlFact]
  public async Task Artifact_DeactivatesMissingRecordsAcrossAllFourTables() {
    await using var test = await PostgresRegionDatasetImporterTests.TestDatabase.CreateAsync();
    // Seed a separate hierarchy omitted from the incoming data.
    await test.Db.Database.ExecuteSqlRawAsync("""
        INSERT INTO esertifikasi."Province" ("Id","Code","Name","IsActive","SourceUpdatedAt","SyncedAt") VALUES (98, '98', 'Missing', true, now(), now());
        INSERT INTO esertifikasi."Regency" ("Id","Code","ProvinceId","Name","IsActive","SourceUpdatedAt","SyncedAt") VALUES (9801,'98.01',98,'Missing',true,now(),now());
        INSERT INTO esertifikasi."District" ("Id","Code","RegencyId","Name","IsActive","SourceUpdatedAt","SyncedAt") VALUES (980101,'98.01.01',9801,'Missing',true,now(),now());
        INSERT INTO esertifikasi."Village" ("Id","Code","DistrictId","Name","IsActive","SourceUpdatedAt","SyncedAt") VALUES (9801010001,'98.01.01.0001',980101,'Missing',true,now(),now());
        """);
    using var source = new RemoteImportTestDataset();
    await source.WriteAsync();
    var artifact = await Generate(source.ManifestPath);
    try {
      var result = await Execute(test, artifact.Path);
      Assert.Equal(0, result.Exit);
      Assert.Contains("ChangedRecords=9", string.Join('\n', result.Errors));
      Assert.False((await test.Db.Provinces.AsNoTracking().SingleAsync(x => x.Id == 98)).IsActive);
      Assert.False((await test.Db.Regencies.AsNoTracking().SingleAsync(x => x.Id == 9801)).IsActive);
      Assert.False((await test.Db.Districts.AsNoTracking().SingleAsync(x => x.Id == 980101)).IsActive);
      Assert.False((await test.Db.Villages.AsNoTracking().SingleAsync(x => x.Id == 9801010001)).IsActive);
    }
    finally { File.Delete(artifact.Path); }
  }

  [PsqlFact]
  public async Task Artifact_UpsertFailureRollsBackParentUpdatesAndCompletionMarker() {
    await using var test = await PostgresRegionDatasetImporterTests.TestDatabase.CreateAsync();
    using var source = new RemoteImportTestDataset();
    await source.WriteAsync(new[] { "Original" });
    var seed = await Generate(source.ManifestPath);
    try { Assert.Equal(0, (await Execute(test, seed.Path)).Exit); }
    finally { File.Delete(seed.Path); }
    await test.Db.Database.ExecuteSqlRawAsync("UPDATE esertifikasi.\"Village\" SET \"Id\"=999 WHERE \"Id\"=7301012001");
    await source.WriteAsync(new[] { "New" }, provinceName: "Must roll back");
    var artifact = await Generate(source.ManifestPath);
    try {
      var result = await Execute(test, artifact.Path);
      Assert.Equal(3, result.Exit); // psql ON_ERROR_STOP script failure.
      Assert.DoesNotContain(RegionSqlArtifactGenerator.CommitMarker, result.Output);
      Assert.Contains("IX_Village_Code", string.Join('\n', result.Errors));
      Assert.Equal("Sulawesi Selatan", (await test.Db.Provinces.AsNoTracking().SingleAsync()).Name);
      Assert.Equal("Original", (await test.Db.Villages.AsNoTracking().SingleAsync()).Name);
      Assert.Equal(999L, (await test.Db.Villages.AsNoTracking().SingleAsync()).Id);
      Assert.Equal(1, await test.Db.RegionDatasetImports.CountAsync());
      await AssertNoRemoteStaging(test);
    }
    finally { File.Delete(artifact.Path); }
  }

  [PsqlFact]
  public async Task Artifact_RejectsVersionReuseWithDifferentFingerprint() {
    await using var test = await PostgresRegionDatasetImporterTests.TestDatabase.CreateAsync();
    using var source = new RemoteImportTestDataset();
    await source.WriteAsync(version: "same-version");
    var seed = await Generate(source.ManifestPath);
    try { Assert.Equal(0, (await Execute(test, seed.Path)).Exit); }
    finally { File.Delete(seed.Path); }
    await source.WriteAsync(new[] { "Different" }, version: "same-version");
    var artifact = await Generate(source.ManifestPath);
    try {
      var result = await Execute(test, artifact.Path);
      Assert.Equal(3, result.Exit);
      Assert.Contains("different fingerprint", string.Join('\n', result.Errors));
      Assert.Equal(2, await test.Db.Villages.CountAsync());
      Assert.Equal(1, await test.Db.RegionDatasetImports.CountAsync());
    }
    finally { File.Delete(artifact.Path); }
  }

  [PsqlFact]
  public async Task Artifact_CopyFailureLeavesNoProductionWritesOrStaging() {
    await using var test = await PostgresRegionDatasetImporterTests.TestDatabase.CreateAsync();
    using var source = new RemoteImportTestDataset();
    await source.WriteAsync(new[] { new string('x', 151) });
    var artifact = await Generate(source.ManifestPath);
    try {
      var result = await Execute(test, artifact.Path);
      Assert.Equal(3, result.Exit);
      Assert.Equal(0, await test.Db.Provinces.CountAsync());
      Assert.Equal(0, await test.Db.Villages.CountAsync());
      Assert.Equal(0, await test.Db.RegionDatasetImports.CountAsync());
      await AssertNoRemoteStaging(test);
    }
    finally { File.Delete(artifact.Path); }
  }

  [PsqlFact]
  public async Task Artifact_FullDatasetCountsAreCorrect() {
    await using var test = await PostgresRegionDatasetImporterTests.TestDatabase.CreateAsync();
    var root = new DirectoryInfo(AppContext.BaseDirectory);
    while (root is not null && !File.Exists(Path.Combine(root.FullName, "data", "regions", "manifest.json"))) root = root.Parent;
    Assert.NotNull(root);
    var artifact = await Generate(Path.Combine(root!.FullName, "data", "regions", "manifest.json"));
    try {
      var result = await Execute(test, artifact.Path);
      Assert.Equal(0, result.Exit);
      Assert.Equal(38, await test.Db.Provinces.CountAsync(x => x.IsActive));
      Assert.Equal(514, await test.Db.Regencies.CountAsync(x => x.IsActive));
      Assert.Equal(7285, await test.Db.Districts.CountAsync(x => x.IsActive));
      Assert.Equal(83762, await test.Db.Villages.CountAsync(x => x.IsActive));
      Assert.Equal(83762, (await test.Db.RegionDatasetImports.SingleAsync()).VillageCount);
      Assert.Contains("ChangedRecords=91599", string.Join('\n', result.Errors));
      await AssertNoRemoteStaging(test);
    }
    finally { File.Delete(artifact.Path); }
  }

  private static async Task<RegionSqlArtifact> Generate(string manifest) =>
      await new RegionSqlArtifactGenerator().GenerateAsync(await RegionDatasetImporter.ReadValidatedDatasetAsync(manifest, CancellationToken.None), CancellationToken.None);

  private static async Task<(int Exit, string[] Output, string[] Errors)> Execute(PostgresRegionDatasetImporterTests.TestDatabase test, string artifact) {
    var cs = new NpgsqlConnectionStringBuilder(test.Db.Database.GetDbConnection().ConnectionString);
    var output = new ConcurrentQueue<string>();
    var errors = new ConcurrentQueue<string>();
    var command = new ImportProcessCommand(Environment.GetEnvironmentVariable("REGION_IMPORT_TEST_PSQL")!, new[] {
      "-X", "-w", "-v", "ON_ERROR_STOP=1", "-h", cs.Host!, "-p", cs.Port.ToString(),
      "-d", cs.Database!, "-U", cs.Username!, "-f", artifact
    });
    var exit = await new ImportProcessRunner().RunAsync(command, output.Enqueue, errors.Enqueue, CancellationToken.None);
    return (exit, output.ToArray(), errors.ToArray());
  }

  private static async Task AssertNoRemoteStaging(PostgresRegionDatasetImporterTests.TestDatabase test) {
    await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_class WHERE relname LIKE 'region_stage_%'", (NpgsqlConnection)test.Db.Database.GetDbConnection());
    Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
  }
}
