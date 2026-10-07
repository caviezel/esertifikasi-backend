using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Esertifikasi.Api.IntegrationTests.Regions;

// These tests create/drop their own uniquely named databases, never the configured
// application database. Supply a PostgreSQL test server with CREATEDB permission.
public sealed class PostgresRegionDatasetImporterTests {
  private const string TestConnectionVariable = "REGION_IMPORT_TEST_POSTGRES";

  public sealed class PostgresFactAttribute : FactAttribute {
    public PostgresFactAttribute() {
      if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(TestConnectionVariable)))
        Skip = $"Set {TestConnectionVariable} to an isolated PostgreSQL test server.";
    }
  }

  [PostgresFact]
  public async Task Import_InsertsAllFieldsWithoutVillageSaveChangesAndDropsStaging() {
    await using var test = await TestDatabase.CreateAsync();
    var logger = new ImportLogger();
    var sourceAt = new DateTimeOffset(2026, 9, 3, 5, 4, 7, TimeSpan.Zero);
    var manifest = await test.WriteDatasetAsync(new[] { Row(2001, "Desa 'Satu' — 雨") }, sourceAt);
    var importer = new RegionDatasetImporter(test.Db, logger);
    var result = await importer.ImportAsync(manifest, CancellationToken.None);
    var village = await test.Db.Villages.AsNoTracking().SingleAsync();

    Assert.Equal(4, result.ChangedRecords);
    Assert.Equal(7301012001L, village.Id);
    Assert.Equal("73.01.01.2001", village.Code);
    Assert.Equal(730101L, village.DistrictId);
    Assert.Equal("Desa 'Satu' — 雨", village.Name);
    Assert.True(village.IsActive);
    Assert.Equal(sourceAt.ToUniversalTime(), village.SourceUpdatedAt);
    Assert.InRange((village.SyncedAt - result.AppliedAt).Duration(), TimeSpan.Zero, TimeSpan.FromMilliseconds(1));
    Assert.Empty(test.Db.ChangeTracker.Entries());
    Assert.Null(test.Db.Database.CurrentTransaction);
    await test.AssertNoStagingAsync();
    foreach (var operation in new[] { "CreateStaging", "BinaryCOPY", "Upsert", "Deactivate", "Commit" }) {
      Assert.Contains(logger.Messages, x => x.StartsWith($"BEFORE {operation}: Villages") && x.Contains("InputRows=1"));
      Assert.Contains(logger.Messages, x => x.StartsWith($"AFTER {operation}: Villages") && x.Contains("ElapsedMs=") && x.Contains("Rows="));
    }
    Assert.Contains(logger.Messages, x => x.StartsWith("AFTER BinaryCOPY:") && x.EndsWith("Rows=1"));
    Assert.True((await importer.ImportAsync(manifest, CancellationToken.None)).AlreadyApplied);
  }

  [PostgresFact]
  public async Task Import_UpsertsExistingIdsReactivatesAndDeactivatesMissingOnlyOnce() {
    await using var test = await TestDatabase.CreateAsync();
    var importer = new RegionDatasetImporter(test.Db);
    var sourceAt = DateTimeOffset.UtcNow.AddDays(-1);
    await importer.ImportAsync(await test.WriteDatasetAsync(new[] { Row(2001), Row(2002), Row(2003) }, sourceAt), CancellationToken.None);
    await test.Db.Database.ExecuteSqlRawAsync("UPDATE esertifikasi.\"Village\" SET \"IsActive\"=FALSE WHERE \"Id\"=7301012001");
    var updatedAt = sourceAt.AddHours(1);
    var result = await importer.ImportAsync(await test.WriteDatasetAsync(new[] { Row(2001, "Renamed"), Row(2002) }, updatedAt), CancellationToken.None);
    Assert.Equal(2, result.ChangedRecords); // One rename/reactivation and one missing row.
    var villages = await test.Db.Villages.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
    Assert.Equal(3, villages.Count);
    Assert.True(villages[0].IsActive);
    Assert.Equal("Renamed", villages[0].Name);
    Assert.Equal(updatedAt.ToUnixTimeMilliseconds(), villages[0].SourceUpdatedAt.ToUnixTimeMilliseconds());
    Assert.False(villages[2].IsActive);
    Assert.Equal(sourceAt.ToUnixTimeMilliseconds(), villages[2].SourceUpdatedAt.ToUnixTimeMilliseconds());
    var deactivatedAt = villages[2].SyncedAt;

    result = await importer.ImportAsync(await test.WriteDatasetAsync(new[] { Row(2001, "Renamed"), Row(2002) }, updatedAt.AddHours(1)), CancellationToken.None);
    Assert.Equal(0, result.ChangedRecords); // Timestamp-only refresh does not count.
    Assert.Equal(deactivatedAt, (await test.Db.Villages.AsNoTracking().SingleAsync(x => x.Id == 7301012003L)).SyncedAt);
    Assert.Equal(2, await test.Db.Villages.CountAsync(x => x.IsActive));
    await test.AssertNoStagingAsync();
  }

  [PostgresFact]
  public async Task Import_RollsBackUpsertAndDeactivationTogetherAndCleansStaging() {
    var injected = new InvalidOperationException("injected after server-side deactivation");
    var interceptor = new DeactivationFailureInterceptor(injected);
    await using var test = await TestDatabase.CreateAsync(interceptor);
    var importer = new RegionDatasetImporter(test.Db);
    await importer.ImportAsync(await test.WriteDatasetAsync(new[] { Row(2001), Row(2002) }), CancellationToken.None);
    var before = await test.Db.Villages.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
    var manifest = await test.WriteDatasetAsync(new[] { Row(2001, "Changed"), Row(2004) });
    interceptor.Fail = true;
    var error = await Assert.ThrowsAsync<RegionImportException>(() => importer.ImportAsync(manifest, CancellationToken.None));
    Assert.Same(injected, error.InnerException);
    Assert.Contains("Deactivate", error.Message);
    var after = await test.Db.Villages.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
    Assert.Equal(before.Select(Snapshot), after.Select(Snapshot));
    Assert.Equal(1, await test.Db.RegionDatasetImports.CountAsync());
    Assert.True(test.Db.ChangeTracker.AutoDetectChangesEnabled);
    Assert.Null(test.Db.Database.CurrentTransaction);
    await test.AssertNoStagingAsync();

    // Explicitly invoke another import on the same open session after rollback.
    interceptor.Fail = false;
    await importer.ImportAsync(manifest, CancellationToken.None);
    Assert.Equal(2, await test.Db.Villages.CountAsync(x => x.IsActive));
    await test.AssertNoStagingAsync();
  }

  [PostgresFact]
  public async Task Import_CopyFailureRollsBackAndCleansStaging() {
    await using var test = await TestDatabase.CreateAsync();
    var manifest = await test.WriteDatasetAsync(new[] { Row(2001, new string('x', 151)) });
    var error = await Assert.ThrowsAsync<RegionImportException>(() => new RegionDatasetImporter(test.Db).ImportAsync(manifest, CancellationToken.None));
    Assert.Contains("BinaryCOPY", error.Message);
    Assert.Equal("22001", Assert.IsType<PostgresException>(error.InnerException).SqlState);
    Assert.Equal(0, await test.Db.Villages.CountAsync());
    Assert.Equal(0, await test.Db.RegionDatasetImports.CountAsync());
    Assert.Equal(1, await test.Db.Districts.CountAsync()); // Parent stages stay committed.
    await test.AssertNoStagingAsync();
    await new RegionDatasetImporter(test.Db).ImportAsync(await test.WriteDatasetAsync(new[] { Row(2001) }), CancellationToken.None);
    Assert.Equal(1, await test.Db.Villages.CountAsync());
  }

  [PostgresFact]
  public async Task Import_UniqueCodeConflictIsReportedAndNeverOverwritesAnotherId() {
    await using var test = await TestDatabase.CreateAsync();
    await new RegionDatasetImporter(test.Db).ImportAsync(await test.WriteDatasetAsync(new[] { Row(2001) }), CancellationToken.None);
    await test.Db.Database.ExecuteSqlRawAsync("UPDATE esertifikasi.\"Village\" SET \"Id\"=999 WHERE \"Id\"=7301012001");
    var manifest = await test.WriteDatasetAsync(new[] { Row(2001, "Replacement"), Row(2002) });
    var error = await Assert.ThrowsAsync<RegionImportException>(() => new RegionDatasetImporter(test.Db).ImportAsync(manifest, CancellationToken.None));
    Assert.Contains("Upsert", error.Message);
    Assert.Equal("23505", Assert.IsType<PostgresException>(error.InnerException).SqlState);
    Assert.Equal(999L, (await test.Db.Villages.AsNoTracking().SingleAsync()).Id);
    Assert.Equal(1, await test.Db.RegionDatasetImports.CountAsync());
    await test.AssertNoStagingAsync();
  }

  [PostgresFact]
  public async Task Import_RejectsDuplicateCsvIdsBeforeAnyStageIsWritten() {
    await using var test = await TestDatabase.CreateAsync();
    var manifest = await test.WriteDatasetAsync(new[] { Row(2001), Row(2001, "Duplicate") });
    var error = await Assert.ThrowsAsync<RegionImportException>(() => new RegionDatasetImporter(test.Db).ImportAsync(manifest, CancellationToken.None));
    Assert.Contains("ID duplikat", error.Message);
    Assert.Equal(0, await test.Db.Provinces.CountAsync());
    Assert.Equal(0, await test.Db.Villages.CountAsync());
    await test.AssertNoStagingAsync();
  }

  [PostgresFact]
  public async Task Import_CancellationBeforeCopyRollsBackAndCleansStaging() {
    await using var test = await TestDatabase.CreateAsync();
    using var cts = new CancellationTokenSource();
    var logger = new ImportLogger {
      OnMessage = message => { if (message.StartsWith("BEFORE BinaryCOPY:")) cts.Cancel(); }
    };
    var manifest = await test.WriteDatasetAsync(new[] { Row(2001) });
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RegionDatasetImporter(test.Db, logger).ImportAsync(manifest, cts.Token));
    Assert.Equal(0, await test.Db.Villages.CountAsync());
    Assert.Null(test.Db.Database.CurrentTransaction);
    Assert.True(test.Db.ChangeTracker.AutoDetectChangesEnabled);
    await test.AssertNoStagingAsync();
  }

  [PostgresFact]
  public async Task Import_FullDatasetCopies83762VillagesWithoutVillageSaveChanges() {
    await using var test = await TestDatabase.CreateAsync();
    var root = new DirectoryInfo(AppContext.BaseDirectory);
    while (root is not null && !File.Exists(Path.Combine(root.FullName, "data", "regions", "manifest.json"))) root = root.Parent;
    Assert.NotNull(root);
    var manifest = Path.Combine(root!.FullName, "data", "regions", "manifest.json");
    var result = await new RegionDatasetImporter(test.Db).ImportAsync(manifest, CancellationToken.None);
    Assert.Equal(83762, result.Villages);
    Assert.Equal(83762, await test.Db.Villages.CountAsync(x => x.IsActive));
    Assert.Equal(38 + 514 + 7285 + 83762, result.ChangedRecords);
    Assert.Equal(83762, (await test.Db.RegionDatasetImports.SingleAsync()).VillageCount);
    await test.AssertNoStagingAsync();
  }

  private static string Row(int suffix, string name = "Batu Lekke") => $"730101{suffix:0000},73.01.01.{suffix:0000},730101,{name}\n";
  private static object Snapshot(Village x) => new { x.Id, x.Code, x.DistrictId, x.Name, x.IsActive, x.SourceUpdatedAt, x.SyncedAt };

  private sealed class ImportLogger : ILogger<RegionDatasetImporter> {
    public List<string> Messages { get; } = new();
    public Action<string>? OnMessage { get; init; }
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
      var message = formatter(state, exception);
      Messages.Add(message);
      OnMessage?.Invoke(message);
    }
  }

  private sealed class DeactivationFailureInterceptor(Exception failure) : DbCommandInterceptor {
    public bool Fail { get; set; }
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        int result, CancellationToken cancellationToken = default) {
      if (Fail && command.CommandText.StartsWith("UPDATE \"esertifikasi\".\"Village\" AS v SET")) throw failure;
      return ValueTask.FromResult(result);
    }
  }

  private sealed class VillageSaveGuard : SaveChangesInterceptor {
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default) {
      Assert.DoesNotContain(eventData.Context!.ChangeTracker.Entries(), x => x.Entity is Village);
      return ValueTask.FromResult(result);
    }
  }

  // Use the real production region mappings, removing only unrelated tables.
  public sealed class PostgresRegionModelCustomizer(ModelCustomizerDependencies dependencies) : ModelCustomizer(dependencies) {
    public override void Customize(ModelBuilder builder, DbContext context) {
      base.Customize(builder, context);
      var regionTypes = new[] { typeof(Province), typeof(Regency), typeof(District), typeof(Village), typeof(RegionDatasetImport) };
      foreach (var type in builder.Model.GetEntityTypes().Select(x => x.ClrType).ToList())
        if (!regionTypes.Contains(type)) builder.Ignore(type);
      builder.Entity<Village>().Ignore(x => x.Petani).Ignore(x => x.Lahan);
    }
  }

  internal sealed class TestDatabase : IAsyncDisposable {
    private readonly string _adminConnection;
    private readonly string _databaseName;
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"region-postgres-{Guid.NewGuid():N}");
    public AppDbContext Db { get; }

    private TestDatabase(string adminConnection, string databaseName, AppDbContext db) {
      _adminConnection = adminConnection;
      _databaseName = databaseName;
      Db = db;
      Directory.CreateDirectory(_directory);
    }

    public static async Task<TestDatabase> CreateAsync(params IInterceptor[] interceptors) {
      var admin = Environment.GetEnvironmentVariable(TestConnectionVariable)!;
      var name = $"region_import_test_{Guid.NewGuid():N}";
      await using (var connection = new NpgsqlConnection(admin)) {
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
        await command.ExecuteNonQueryAsync();
      }
      var cs = new NpgsqlConnectionStringBuilder(admin) { Database = name, Pooling = false };
      var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
          .UseNpgsql(cs.ConnectionString)
          .ReplaceService<IModelCustomizer, PostgresRegionModelCustomizer>()
          .AddInterceptors(interceptors.Append(new VillageSaveGuard())).Options);
      var test = new TestDatabase(admin, name, db);
      try {
        await db.Database.EnsureCreatedAsync();
        // Hold the same physical session open so cleanup tests cannot pass simply
        // because EF closed the connection and discarded the temporary table.
        await db.Database.OpenConnectionAsync();
        return test;
      }
      catch { await test.DisposeAsync(); throw; }
    }

    public async Task AssertNoStagingAsync() {
      Assert.Null(Db.Database.CurrentTransaction);
      await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_class WHERE relnamespace=pg_my_temp_schema() AND relname LIKE 'region_village_stage_%'", (NpgsqlConnection)Db.Database.GetDbConnection());
      Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
    }

    public async Task<string> WriteDatasetAsync(IEnumerable<string> villages, DateTimeOffset? sourceAt = null) {
      var rows = villages.ToList();
      var contents = new[] {
        (Key: "provinces", Content: "id,code,name\n73,73,Sulawesi Selatan\n", Count: 1),
        (Key: "regencies", Content: "id,code,province_id,name\n7301,73.01,73,Kabupaten Kepulauan Selayar\n", Count: 1),
        (Key: "districts", Content: "id,code,regency_id,name\n730101,73.01.01,7301,Bontomatene\n", Count: 1),
        (Key: "villages", Content: "id,code,district_id,name\n" + string.Concat(rows), Count: rows.Count)
      };
      var metadata = new Dictionary<string, object>();
      var hashes = new List<string>();
      foreach (var file in contents) {
        var path = file.Key + ".csv";
        await File.WriteAllTextAsync(Path.Combine(_directory, path), file.Content, new UTF8Encoding(false));
        var sha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(_directory, path)))).ToLowerInvariant();
        hashes.Add(sha256);
        metadata[file.Key] = new { path, count = file.Count, sha256 };
      }
      var manifest = new {
        schemaVersion = 1, version = $"test-{Guid.NewGuid():N}", source = "PostgreSQL test",
        generatedAt = sourceAt ?? DateTimeOffset.UtcNow,
        datasetSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', hashes) + "\n"))).ToLowerInvariant(),
        files = metadata
      };
      var manifestPath = Path.Combine(_directory, "manifest.json");
      await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest));
      return manifestPath;
    }

    public async ValueTask DisposeAsync() {
      await Db.DisposeAsync();
      await using var connection = new NpgsqlConnection(_adminConnection);
      await connection.OpenAsync();
      await using var command = new NpgsqlCommand($"DROP DATABASE \"{_databaseName}\" WITH (FORCE)", connection);
      await command.ExecuteNonQueryAsync();
      Directory.Delete(_directory, true);
    }
  }
}
