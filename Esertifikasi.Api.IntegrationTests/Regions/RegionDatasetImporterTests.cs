using System.Data.Common;
using System.Security.Cryptography;
using Esertifikasi.Api.Domain.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Esertifikasi.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

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
  public async Task Import_IsValidatedAndIdempotent() {
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

  [Fact]
  public async Task Import_BatchesLargeDatasetsAndDeactivatesMissingRowsOnNextVersion() {
    var directory = Path.Combine(Path.GetTempPath(), $"region-import-batched-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try {
      var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"region-import-batched-{Guid.NewGuid():N}").Options;
      await using var db = new AppDbContext(options);
      await db.Database.EnsureCreatedAsync();
      var importer = new RegionDatasetImporter(db);
      var result = await importer.ImportAsync(await WriteDatasetAsync(directory, 1201), CancellationToken.None);
      Assert.Equal(1201, result.Villages);
      Assert.Equal(1204, result.ChangedRecords);
      Assert.Equal(1201, await db.Villages.CountAsync(x => x.IsActive));
      Assert.DoesNotContain(db.ChangeTracker.Entries(), e => e.Entity is Esertifikasi.Api.Domain.Entities.Village);
      result = await importer.ImportAsync(await WriteDatasetAsync(directory, 600), CancellationToken.None);
      Assert.Equal(601, result.ChangedRecords);
      Assert.Equal(600, await db.Villages.CountAsync(x => x.IsActive));
      Assert.Equal(601, await db.Villages.CountAsync(x => !x.IsActive));
      Assert.Equal(2, await db.RegionDatasetImports.CountAsync());
    }
    finally { Directory.Delete(directory, recursive: true); }
  }

  [Fact]
  public async Task Import_ReportsWrappedPostgresLockTimeoutAndPreservesOriginalException() {
    var directory = Path.Combine(Path.GetTempPath(), $"region-import-lock-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try {
      var postgres = new PostgresException("canceling statement due to lock timeout", "ERROR", "ERROR", "55P03");
      var failure = new InvalidOperationException("Transient failure", new DbUpdateException("Save failed", postgres));
      var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"region-import-lock-{Guid.NewGuid():N}").AddInterceptors(new FailingSaveInterceptor(failure)).Options;
      await using var db = new AppDbContext(options);
      var manifestPath = await WriteDatasetAsync(directory);
      var error = await Assert.ThrowsAsync<RegionImportException>(() => new RegionDatasetImporter(db).ImportAsync(manifestPath, CancellationToken.None));
      Assert.Contains("diblokir", error.Message);
      Assert.Same(failure, error.InnerException);
      Assert.True(db.ChangeTracker.AutoDetectChangesEnabled);
    }
    finally { Directory.Delete(directory, recursive: true); }
  }

  [Fact]
  public async Task Import_CommitsStagesAnd100VillageBatchesBeforeContinuing() {
    var directory = Path.Combine(Path.GetTempPath(), $"region-boundaries-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    var trace = new TransactionTrace();
    var commands = new ImportCommandInterceptor();
    var logger = new ProgressLogger();
    try {
      await using var db = CreateRelationalContext(connection, trace, commands);
      await db.Database.EnsureCreatedAsync();
      trace.Commits.Clear();
      var result = await new RegionDatasetImporter(db, logger).ImportAsync(await WriteDatasetAsync(directory, 1201), CancellationToken.None);
      Assert.Equal(1204, result.ChangedRecords);
      Assert.Equal(new[] { "Province:1", "Regency:1", "District:1" }
          .Concat(Enumerable.Repeat("Village:100", 12)).Concat(new[] { "Village:1", "RegionDatasetImport:1" }), trace.Commits);
      Assert.Equal(0, trace.Rollbacks);
      Assert.Contains("Villages: 100/1201", logger.Messages);
      Assert.Contains("Villages: 1000/1201", logger.Messages);
      Assert.Contains("Villages: 1201/1201", logger.Messages);
      foreach (var operation in new[] { "BeginTransaction", "SaveChanges", "Commit", "Clear", "Dispose" }) {
        Assert.Contains(logger.Messages, x => x.StartsWith($"BEFORE {operation}: Villages batch 1-100/1201"));
        Assert.Contains(logger.Messages, x => x.StartsWith($"AFTER {operation}: Villages batch 1-100/1201"));
      }
      Assert.False(commands.ReadInsideTransaction);
      Assert.Empty(db.ChangeTracker.Entries());
      Assert.Null(db.Database.CurrentTransaction);
    }
    finally { Directory.Delete(directory, true); }
  }

  [Fact]
  public async Task Import_RollsBackOnlyFailedVillageBatchAndCanRetrySameManifest() {
    var directory = Path.Combine(Path.GetTempPath(), $"region-batch-failure-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    var trace = new TransactionTrace();
    var commands = new ImportCommandInterceptor { FailVillageWrite = 550 };
    try {
      await using var db = CreateRelationalContext(connection, trace, commands);
      await db.Database.EnsureCreatedAsync();
      trace.Commits.Clear();
      var manifest = await WriteDatasetAsync(directory, 1201);
      var error = await Assert.ThrowsAsync<RegionImportException>(() => new RegionDatasetImporter(db).ImportAsync(manifest, CancellationToken.None));
      Assert.Contains("Villages batch 501-600/1201", error.Message);
      Assert.Contains("injected", error.InnerException!.ToString());
      Assert.Equal(1, trace.Rollbacks);
      Assert.Equal(new[] { "Province:1", "Regency:1", "District:1" }
          .Concat(Enumerable.Repeat("Village:100", 5)), trace.Commits);
      Assert.Empty(db.ChangeTracker.Entries());
      Assert.Null(db.Database.CurrentTransaction);
      Assert.True(db.ChangeTracker.AutoDetectChangesEnabled);
      Assert.Equal(500, await db.Villages.CountAsync());
      Assert.Equal(1, await db.Provinces.CountAsync());
      Assert.Equal(1, await db.Regencies.CountAsync());
      Assert.Equal(1, await db.Districts.CountAsync());
      Assert.Equal(0, await db.RegionDatasetImports.CountAsync());
      commands.FailVillageWrite = null;
      var result = await new RegionDatasetImporter(db).ImportAsync(manifest, CancellationToken.None);
      Assert.Equal(701, result.ChangedRecords);
      Assert.Equal(1201, await db.Villages.CountAsync());
      Assert.Equal(1, await db.RegionDatasetImports.CountAsync());
      Assert.True((await new RegionDatasetImporter(db).ImportAsync(manifest, CancellationToken.None)).AlreadyApplied);
    }
    finally { Directory.Delete(directory, true); }
  }

  [Fact]
  public async Task Import_RollsBackFailedRegencyStageAndDoesNotStartChildren() {
    var directory = Path.Combine(Path.GetTempPath(), $"region-stage-failure-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    var trace = new TransactionTrace();
    var commands = new ImportCommandInterceptor { FailRegency = true };
    try {
      await using var db = CreateRelationalContext(connection, trace, commands);
      await db.Database.EnsureCreatedAsync();
      trace.Commits.Clear();
      var manifest = await WriteDatasetAsync(directory);
      var error = await Assert.ThrowsAsync<RegionImportException>(() => new RegionDatasetImporter(db).ImportAsync(manifest, CancellationToken.None));
      Assert.Contains("Regencies batch 1-1/1", error.Message);
      Assert.Equal(new[] { "Province:1" }, trace.Commits);
      Assert.Equal(1, trace.Rollbacks);
      Assert.Equal(1, await db.Provinces.CountAsync());
      Assert.Equal(0, await db.Regencies.CountAsync());
      Assert.Equal(0, await db.Districts.CountAsync());
      Assert.Equal(0, await db.Villages.CountAsync());
      Assert.Equal(0, await db.RegionDatasetImports.CountAsync());
    }
    finally { Directory.Delete(directory, true); }
  }

  [Fact]
  public async Task Import_BatchesVillageDeactivationAndRejectsAnOuterTransaction() {
    var directory = Path.Combine(Path.GetTempPath(), $"region-deactivation-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    var trace = new TransactionTrace();
    var commands = new ImportCommandInterceptor();
    try {
      await using var db = CreateRelationalContext(connection, trace, commands);
      await db.Database.EnsureCreatedAsync();
      var importer = new RegionDatasetImporter(db);
      await importer.ImportAsync(await WriteDatasetAsync(directory, 1201), CancellationToken.None);
      trace.Commits.Clear();
      var manifest = await WriteDatasetAsync(directory, 600);
      await using (var outer = await db.Database.BeginTransactionAsync()) {
        var error = await Assert.ThrowsAsync<RegionImportException>(() => importer.ImportAsync(manifest, CancellationToken.None));
        Assert.Contains("existing transaction", error.Message);
        await outer.RollbackAsync();
      }
      var logger = new ProgressLogger();
      var result = await new RegionDatasetImporter(db, logger).ImportAsync(manifest, CancellationToken.None);
      Assert.Equal(601, result.ChangedRecords);
      Assert.Equal(new[] { "Province:1", "Regency:1", "District:1" }
          .Concat(Enumerable.Repeat("Village:100", 12)).Concat(new[] { "Village:1", "RegionDatasetImport:1" }), trace.Commits);
      Assert.Contains("Villages (deactivate): 100/601", logger.Messages);
      Assert.Contains("Villages (deactivate): 601/601", logger.Messages);
      Assert.Equal(600, await db.Villages.CountAsync(x => x.IsActive));
      Assert.Equal(601, await db.Villages.CountAsync(x => !x.IsActive));
      Assert.Empty(db.ChangeTracker.Entries());
    }
    finally { Directory.Delete(directory, true); }
  }

  [Fact]
  public async Task Import_LogsStalledSaveAndWaitsBeforeStartingAnotherOperation() {
    var directory = Path.Combine(Path.GetTempPath(), $"region-stalled-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var gate = new PausedSaveInterceptor();
    var logger = new ProgressLogger();
    try {
      var manifest = await WriteDatasetAsync(directory);
      var options = new DbContextOptionsBuilder<AppDbContext>()
          .UseInMemoryDatabase($"region-stalled-{Guid.NewGuid():N}").AddInterceptors(gate).Options;
      await using var db = new AppDbContext(options);
      var import = new RegionDatasetImporter(db, logger, TimeSpan.FromMilliseconds(20))
          .ImportAsync(manifest, CancellationToken.None);
      try {
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(100);
        Assert.False(import.IsCompleted);
        Assert.Equal(1, gate.Calls);
        Assert.Contains(logger.Messages, x => x.Contains("STALLED SaveChanges: Provinces batch 1-1/1")
            && x.Contains("PID=") && x.Contains("Thread=") && x.Contains("UTC=") && x.Contains("ElapsedMs="));
        Assert.DoesNotContain(logger.Messages, x => x.Contains("AFTER SaveChanges"));
      }
      finally { gate.Release.TrySetResult(); }
      await import.WaitAsync(TimeSpan.FromSeconds(5));
      Assert.Contains(logger.Messages, x => x.Contains("AFTER SaveChanges"));
      Assert.Empty(db.ChangeTracker.Entries());
    }
    finally { Directory.Delete(directory, true); }
  }

  private sealed class PausedSaveInterceptor : SaveChangesInterceptor {
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Calls { get; private set; }
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default) {
      if (++Calls == 1) {
        Entered.TrySetResult();
        await Release.Task.WaitAsync(cancellationToken);
      }
      return result;
    }
  }

  private static AppDbContext CreateRelationalContext(SqliteConnection connection, params IInterceptor[] interceptors) =>
      new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection)
          .ReplaceService<IModelCustomizer, RegionOnlyModelCustomizer>().AddInterceptors(interceptors).Options);

  // Test only the region tables with a real relational provider and enforced FKs.
  public sealed class RegionOnlyModelCustomizer(ModelCustomizerDependencies dependencies) : ModelCustomizer(dependencies) {
    public override void Customize(ModelBuilder builder, DbContext context) {
      var regionTypes = new[] { typeof(Province), typeof(Regency), typeof(District), typeof(Village), typeof(RegionDatasetImport) };
      foreach (var type in builder.Model.GetEntityTypes().Select(x => x.ClrType).ToList())
        if (!regionTypes.Contains(type)) builder.Ignore(type);
      builder.Entity<Province>();
      builder.Entity<Regency>().HasOne(x => x.Province).WithMany(x => x.Regencies).HasForeignKey(x => x.ProvinceId);
      builder.Entity<District>().HasOne(x => x.Regency).WithMany(x => x.Districts).HasForeignKey(x => x.RegencyId);
      builder.Entity<Village>().HasOne(x => x.District).WithMany(x => x.Villages).HasForeignKey(x => x.DistrictId);
      builder.Entity<Village>().Ignore(x => x.Petani).Ignore(x => x.Lahan);
      builder.Entity<RegionDatasetImport>().HasIndex(x => x.Version).IsUnique();
    }
  }

  private sealed class TransactionTrace : DbTransactionInterceptor {
    public List<string> Commits { get; } = new();
    public int Rollbacks { get; private set; }
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
      var entries = eventData.Context!.ChangeTracker.Entries().ToList();
      if (entries.Count > 0) Commits.Add($"{entries[0].Entity.GetType().Name}:{entries.Count}");
      return Task.CompletedTask;
    }
    public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
      Rollbacks++;
      return Task.CompletedTask;
    }
  }

  private sealed class ImportCommandInterceptor : DbCommandInterceptor {
    public int? FailVillageWrite { get; set; }
    public bool FailRegency { get; init; }
    public bool ReadInsideTransaction { get; private set; }
    private int _villageWrites;
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
      if (command.CommandText.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) && command.Transaction is not null) ReadInsideTransaction = true;
      if (command.CommandText.StartsWith("INSERT INTO \"Villages\"", StringComparison.Ordinal) && ++_villageWrites == FailVillageWrite)
        throw new InvalidOperationException("injected village batch failure");
      if (FailRegency && command.CommandText.StartsWith("INSERT INTO \"Regencies\"", StringComparison.Ordinal))
        throw new InvalidOperationException("injected regency stage failure");
      return ValueTask.FromResult(result);
    }
  }

  private sealed class ProgressLogger : ILogger<RegionDatasetImporter> {
    public System.Collections.Concurrent.ConcurrentQueue<string> Messages { get; } = new();
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Enqueue(formatter(state, exception));
  }

  private sealed class FailingSaveInterceptor(Exception failure) : SaveChangesInterceptor {
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) => throw failure;
  }

  private static async Task<string> WriteDatasetAsync(string directory, int villageCount = 1) {
    var files = new Dictionary<string, (string Name, string Content, int Count)> {
      ["provinces"] = ("provinces.csv", "id,code,name\n73,73,Sulawesi Selatan\n", 1),
      ["regencies"] = ("regencies.csv", "id,code,province_id,name\n7301,73.01,73,Kabupaten Kepulauan Selayar\n", 1),
      ["districts"] = ("districts.csv", "id,code,regency_id,name\n730101,73.01.01,7301,Bontomatene\n", 1),
      ["villages"] = ("villages.csv", "id,code,district_id,name\n" + string.Join("", Enumerable.Range(2001, villageCount).Select(i => $"730101{i:0000},73.01.01.{i:0000},730101,Batu Lekke\n")), villageCount)
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
