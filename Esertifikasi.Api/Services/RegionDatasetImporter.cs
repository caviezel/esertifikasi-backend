using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Esertifikasi.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.VisualBasic.FileIO;
using Npgsql;

namespace Esertifikasi.Api.Services;

public sealed partial class RegionDatasetImporter {
  private static readonly Regex Sha256Pattern = new("^[a-f0-9]{64}$", RegexOptions.Compiled);
  private const int VillageBatchSize = 100;
  private readonly TimeSpan _diagnosticDeadline;
  private readonly AppDbContext _db;
  private readonly ILogger<RegionDatasetImporter>? _logger;

  public RegionDatasetImporter(AppDbContext db, ILogger<RegionDatasetImporter>? logger = null,
      TimeSpan? diagnosticDeadline = null) {
    _db = db;
    _logger = logger;
    _diagnosticDeadline = diagnosticDeadline ?? TimeSpan.FromSeconds(30);
    if (_diagnosticDeadline <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(diagnosticDeadline));
  }

  public async Task<RegionDatasetImportResult> ImportAsync(string manifestPath, CancellationToken ct) {
    var fullManifestPath = ResolveManifestPath(manifestPath);
    if (!File.Exists(fullManifestPath)) throw new RegionImportException($"Manifest tidak ditemukan: {fullManifestPath}");
    var manifestBytes = await File.ReadAllBytesAsync(fullManifestPath, ct);
    var manifest = JsonSerializer.Deserialize<RegionDatasetManifest>(manifestBytes, new JsonSerializerOptions {
      PropertyNameCaseInsensitive = true
    }) ?? throw new RegionImportException("Manifest tidak dapat dibaca.");
    ValidateManifest(manifest);

    var previous = await ObserveOperationAsync("ReadPreviousImport", "RegionDatasetImport", 0, 0, 0,
        () => _db.RegionDatasetImports.AsNoTracking().SingleOrDefaultAsync(x => x.Version == manifest.Version, ct));
    if (previous is not null) {
      if (previous.DatasetSha256 != manifest.DatasetSha256)
        throw new RegionImportException($"Versi dataset '{manifest.Version}' sudah digunakan oleh dataset yang berbeda.");
      return new RegionDatasetImportResult(manifest.Version, true, previous.ProvinceCount, previous.RegencyCount,
          previous.DistrictCount, previous.VillageCount, 0, previous.AppliedAt);
    }

    var baseDirectory = Path.GetDirectoryName(fullManifestPath)!;
    var provinces = await ReadCsvAsync(baseDirectory, manifest.Files.Provinces,
        new[] { "id", "code", "name" }, ParseProvince, ct);
    var regencies = await ReadCsvAsync(baseDirectory, manifest.Files.Regencies,
        new[] { "id", "code", "province_id", "name" }, ParseRegency, ct);
    var districts = await ReadCsvAsync(baseDirectory, manifest.Files.Districts,
        new[] { "id", "code", "regency_id", "name" }, ParseDistrict, ct);
    var villages = await ReadCsvAsync(baseDirectory, manifest.Files.Villages,
        new[] { "id", "code", "district_id", "name" }, ParseVillage, ct);
    ValidateHierarchy(provinces, regencies, districts, villages);

    if (_db.Database.CurrentTransaction is not null)
      throw new RegionImportException("Region import cannot run inside an existing transaction; each stage must commit independently.");
    var now = DateTimeOffset.UtcNow;
    var changed = 0;
    var autoDetectChanges = _db.ChangeTracker.AutoDetectChangesEnabled;
    _db.ChangeTracker.AutoDetectChangesEnabled = false;
    try {
      _logger?.LogInformation("Import wilayah {Version}: {ProvinceCount} provinsi, {RegencyCount} kabupaten, {DistrictCount} kecamatan, {VillageCount} desa.", manifest.Version, provinces.Count, regencies.Count, districts.Count, villages.Count);
      changed += await UpsertProvincesAsync(provinces, manifest.GeneratedAt, now, ct);
      changed += await UpsertRegenciesAsync(regencies, manifest.GeneratedAt, now, ct);
      changed += await UpsertDistrictsAsync(districts, manifest.GeneratedAt, now, ct);
      changed += await UpsertVillagesAsync(villages, manifest.GeneratedAt, now, ct);
      await CommitBatchAsync("RegionDatasetImport", new[] { new PendingWrite(new RegionDatasetImport {
        Id = Guid.NewGuid(), Version = manifest.Version, Source = manifest.Source,
        DatasetSha256 = manifest.DatasetSha256, ProvinceCount = provinces.Count, RegencyCount = regencies.Count,
        DistrictCount = districts.Count, VillageCount = villages.Count, AppliedAt = now
      }, EntityState.Added) }, 1, 1, 1, ct);
    }
    finally {
      _db.ChangeTracker.AutoDetectChangesEnabled = autoDetectChanges;
    }
    return new RegionDatasetImportResult(manifest.Version, false, provinces.Count, regencies.Count,
        districts.Count, villages.Count, changed, now);
  }

  private async Task<int> UpsertProvincesAsync(Dictionary<long, ProvinceRow> incoming, DateTimeOffset sourceAt, DateTimeOffset now, CancellationToken ct) {
    var existing = await ObserveOperationAsync("ReadExisting", "Provinces", 0, 0, incoming.Count,
        () => _db.Provinces.AsNoTracking().ToDictionaryAsync(x => x.Id, ct));
    var writes = new List<PendingWrite>(incoming.Count);
    var changed = 0;
    foreach (var row in incoming.Values) {
      ct.ThrowIfCancellationRequested();
      var found = existing.TryGetValue(row.Id, out var entity);
      entity ??= new Province { Id = row.Id };
      if (!found || entity.Code != row.Code || entity.Name != row.Name || !entity.IsActive) changed++;
      entity.Code = row.Code; entity.Name = row.Name; entity.IsActive = true;
      entity.SourceUpdatedAt = sourceAt; entity.SyncedAt = now;
      writes.Add(new PendingWrite(entity, found ? EntityState.Modified : EntityState.Added));
    }
    var missing = PrepareDeactivations(existing, incoming.Keys, now);
    changed += missing.Count;
    writes.AddRange(missing);
    await CommitBatchAsync("Provinces", writes, 1, writes.Count, writes.Count, ct);
    return changed;
  }

  private async Task<int> UpsertRegenciesAsync(Dictionary<long, RegencyRow> incoming, DateTimeOffset sourceAt, DateTimeOffset now, CancellationToken ct) {
    var existing = await ObserveOperationAsync("ReadExisting", "Regencies", 0, 0, incoming.Count,
        () => _db.Regencies.AsNoTracking().ToDictionaryAsync(x => x.Id, ct));
    var writes = new List<PendingWrite>(incoming.Count);
    var changed = 0;
    foreach (var row in incoming.Values) {
      ct.ThrowIfCancellationRequested();
      var found = existing.TryGetValue(row.Id, out var entity);
      entity ??= new Regency { Id = row.Id };
      if (!found || entity.Code != row.Code || entity.Name != row.Name || entity.ProvinceId != row.ProvinceId || !entity.IsActive) changed++;
      entity.Code = row.Code; entity.Name = row.Name; entity.ProvinceId = row.ProvinceId; entity.IsActive = true;
      entity.SourceUpdatedAt = sourceAt; entity.SyncedAt = now;
      writes.Add(new PendingWrite(entity, found ? EntityState.Modified : EntityState.Added));
    }
    var missing = PrepareDeactivations(existing, incoming.Keys, now);
    changed += missing.Count;
    writes.AddRange(missing);
    await CommitBatchAsync("Regencies", writes, 1, writes.Count, writes.Count, ct);
    return changed;
  }

  private async Task<int> UpsertDistrictsAsync(Dictionary<long, DistrictRow> incoming, DateTimeOffset sourceAt, DateTimeOffset now, CancellationToken ct) {
    var existing = await ObserveOperationAsync("ReadExisting", "Districts", 0, 0, incoming.Count,
        () => _db.Districts.AsNoTracking().ToDictionaryAsync(x => x.Id, ct));
    var writes = new List<PendingWrite>(incoming.Count);
    var changed = 0;
    foreach (var row in incoming.Values) {
      ct.ThrowIfCancellationRequested();
      var found = existing.TryGetValue(row.Id, out var entity);
      entity ??= new District { Id = row.Id };
      if (!found || entity.Code != row.Code || entity.Name != row.Name || entity.RegencyId != row.RegencyId || !entity.IsActive) changed++;
      entity.Code = row.Code; entity.Name = row.Name; entity.RegencyId = row.RegencyId; entity.IsActive = true;
      entity.SourceUpdatedAt = sourceAt; entity.SyncedAt = now;
      writes.Add(new PendingWrite(entity, found ? EntityState.Modified : EntityState.Added));
    }
    var missing = PrepareDeactivations(existing, incoming.Keys, now);
    changed += missing.Count;
    writes.AddRange(missing);
    await CommitBatchAsync("Districts", writes, 1, writes.Count, writes.Count, ct);
    return changed;
  }

  private async Task<int> UpsertVillagesAsync(Dictionary<long, VillageRow> incoming, DateTimeOffset sourceAt, DateTimeOffset now, CancellationToken ct) {
    if (_db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
      return await BulkUpsertVillagesAsync(incoming, sourceAt, now, ct);
    // Non-PostgreSQL providers are retained for the existing in-memory/SQLite tests.
    var existing = await ObserveOperationAsync("ReadExisting", "Villages", 0, 0, incoming.Count,
        () => _db.Villages.AsNoTracking().ToDictionaryAsync(x => x.Id, ct));
    var writes = new List<PendingWrite>(incoming.Count);
    var changed = 0;
    foreach (var row in incoming.Values) {
      ct.ThrowIfCancellationRequested();
      var found = existing.TryGetValue(row.Id, out var entity);
      entity ??= new Village { Id = row.Id };
      if (!found || entity.Code != row.Code || entity.Name != row.Name || entity.DistrictId != row.DistrictId || !entity.IsActive) changed++;
      entity.Code = row.Code; entity.Name = row.Name; entity.DistrictId = row.DistrictId; entity.IsActive = true;
      entity.SourceUpdatedAt = sourceAt; entity.SyncedAt = now;
      writes.Add(new PendingWrite(entity, found ? EntityState.Modified : EntityState.Added));
    }
    var missing = PrepareDeactivations(existing, incoming.Keys, now);
    changed += missing.Count;
    await CommitBatchesAsync("Villages", writes, ct);
    await CommitBatchesAsync("Villages (deactivate)", missing, ct);
    return changed;
  }

  private static List<PendingWrite> PrepareDeactivations<TEntity>(Dictionary<long, TEntity> existing,
      IEnumerable<long> incomingIds, DateTimeOffset now) where TEntity : class {
    var incoming = incomingIds.ToHashSet();
    var writes = new List<PendingWrite>();
    foreach (var pair in existing.Where(x => !incoming.Contains(x.Key))) {
      switch (pair.Value) {
        case Province x when x.IsActive: x.IsActive = false; x.SyncedAt = now; break;
        case Regency x when x.IsActive: x.IsActive = false; x.SyncedAt = now; break;
        case District x when x.IsActive: x.IsActive = false; x.SyncedAt = now; break;
        case Village x when x.IsActive: x.IsActive = false; x.SyncedAt = now; break;
        default: continue;
      }
      writes.Add(new PendingWrite(pair.Value, EntityState.Modified));
    }
    return writes;
  }

  private async Task CommitBatchesAsync(string entityType, List<PendingWrite> writes, CancellationToken ct) {
    for (var offset = 0; offset < writes.Count; offset += VillageBatchSize) {
      var count = Math.Min(VillageBatchSize, writes.Count - offset);
      await CommitBatchAsync(entityType, writes.GetRange(offset, count), offset + 1, offset + count, writes.Count, ct);
    }
  }

  private async Task CommitBatchAsync(string entityType, IReadOnlyList<PendingWrite> writes,
      int start, int end, int total, CancellationToken ct) {
    if (writes.Count == 0) return;
    var batchClock = Stopwatch.StartNew();
    void Log(string phase, string operation, long elapsed) =>
        LogOperation(phase, operation, entityType, start, end, total, elapsed);
    Task<T> Await<T>(string operation, Func<Task<T>> action) =>
        ObserveOperationAsync(operation, entityType, start, end, total, action);
    async Task AwaitVoid(string operation, Func<Task> action) =>
        await Await(operation, async () => { await action(); return true; });
    void Clear() {
      Log("BEFORE", "Clear", batchClock.ElapsedMilliseconds);
      _db.ChangeTracker.Clear();
      Log("AFTER", "Clear", batchClock.ElapsedMilliseconds);
    }
    IDbContextTransaction? transaction = null;
    try {
      ct.ThrowIfCancellationRequested();
      // Reads, file processing, and entity preparation happen before opening a transaction.
      Log("BEFORE", "Attach/DetectChanges", batchClock.ElapsedMilliseconds);
      foreach (var write in writes) _db.Entry(write.Entity).State = write.State;
      _db.ChangeTracker.DetectChanges();
      Log("AFTER", "Attach/DetectChanges", batchClock.ElapsedMilliseconds);
      if (_db.Database.IsRelational()) {
        transaction = await Await("BeginTransaction", () => _db.Database.BeginTransactionAsync(ct));
        if (_db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
          await Await("SetLocalLockTimeout", () => _db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '10s'", ct));
      }
      await Await("SaveChanges", () => _db.SaveChangesAsync(ct));
      if (transaction is not null) await AwaitVoid("Commit", () => transaction.CommitAsync(ct));
      Clear();
      _logger?.LogInformation("{EntityType}: {Processed}/{Total}", entityType, end, total);
    }
    catch (Exception importError) {
      if (transaction is not null) {
        try { await AwaitVoid("Rollback", () => transaction.RollbackAsync(CancellationToken.None)); }
        catch (Exception rollbackError) {
          _logger?.LogWarning(rollbackError, "Rollback {EntityType} batch {Start}-{End} failed; original import exception preserved.", entityType, start, end);
        }
      }
      _logger?.LogError(importError, "Region import failed: {EntityType} batch {Start}-{End}/{Total}.", entityType, start, end, total);
      if (importError is OperationCanceledException) throw;
      var context = $"{entityType} batch {start}-{end}/{total}: ";
      for (Exception? cause = importError; cause is not null; cause = cause.InnerException) {
        if (cause is PostgresException { SqlState: "55P03" })
          throw new RegionImportException(context + "Import wilayah diblokir oleh transaksi PostgreSQL lain. Selesaikan atau rollback transaksi tersebut sebelum mengulang import. Periksa pg_stat_activity dan pg_blocking_pids; penambahan timeout tidak mengatasi transaksi yang masih terbuka.", importError);
      }
      throw new RegionImportException(context + "Region import failed; previously committed stages and batches are retained. Retry the same manifest to complete the import.", importError);
    }
    finally {
      Clear();
      if (transaction is not null) await AwaitVoid("Dispose", () => transaction.DisposeAsync().AsTask());
    }
  }

  // Diagnostic deadline only: never abandon an in-flight operation and race it with
  // rollback/disposal on the same context. Existing provider timeouts remain intact.
  private async Task<T> ObserveOperationAsync<T>(string operation, string entityType,
      int start, int end, int total, Func<Task<T>> action, Func<T, long>? rowCount = null) {
    var clock = Stopwatch.StartNew();
    LogOperation("BEFORE", operation, entityType, start, end, total, 0, rows: rowCount is null ? null : 0);
    try {
      var pending = action();
      try { await pending.WaitAsync(_diagnosticDeadline); }
      catch (TimeoutException) {
        if (!pending.IsCompleted) LogOperation("STALLED", operation, entityType, start, end, total, clock.ElapsedMilliseconds, LogLevel.Error);
      }
      var result = await pending;
      LogOperation("AFTER", operation, entityType, start, end, total, clock.ElapsedMilliseconds, rows: rowCount?.Invoke(result));
      return result;
    }
    catch (Exception error) {
      LogOperation("FAILED", operation, entityType, start, end, total, clock.ElapsedMilliseconds, LogLevel.Error, error);
      throw;
    }
  }

  private void LogOperation(string phase, string operation, string entityType, int start,
      int end, int total, long elapsed, LogLevel level = LogLevel.Information, Exception? error = null, long? rows = null) =>
      _logger?.Log(level, error,
          "{Phase} {Operation}: {EntityType} batch {Start}-{End}/{Total}; PID={ProcessId} Thread={ThreadId} UTC={UtcTimestamp:o} ElapsedMs={ElapsedMs} ContextId={ContextId} InputRows={InputRows} Rows={RowCount}",
          phase, operation, entityType, start, end, total, Environment.ProcessId,
          Environment.CurrentManagedThreadId, DateTimeOffset.UtcNow, elapsed, _db.ContextId, total, rows);

  private sealed record PendingWrite(object Entity, EntityState State);

  private static async Task<Dictionary<long, T>> ReadCsvAsync<T>(string baseDirectory, RegionDatasetFile file,
      string[] expectedHeader, Func<string[], int, T> parser, CancellationToken ct) where T : IRegionRow {
    var path = ResolveChildPath(baseDirectory, file.Path);
    if (!File.Exists(path)) throw new RegionImportException($"File dataset tidak ditemukan: {path}");
    if (!Sha256Pattern.IsMatch(file.Sha256) || !string.Equals(await HashFileAsync(path, ct), file.Sha256, StringComparison.OrdinalIgnoreCase))
      throw new RegionImportException($"Checksum tidak cocok untuk {file.Path}.");
    var rows = new Dictionary<long, T>();
    using var parserInstance = new TextFieldParser(path) { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true };
    parserInstance.SetDelimiters(",");
    var header = parserInstance.ReadFields() ?? Array.Empty<string>();
    if (!header.SequenceEqual(expectedHeader, StringComparer.Ordinal))
      throw new RegionImportException($"Header {file.Path} tidak valid. Diharapkan: {string.Join(',', expectedHeader)}.");
    var line = 1;
    while (!parserInstance.EndOfData) {
      ct.ThrowIfCancellationRequested();
      line++;
      var fields = parserInstance.ReadFields() ?? throw new RegionImportException($"Baris kosong pada {file.Path}:{line}.");
      T row;
      try { row = parser(fields, line); }
      catch (RegionImportException) { throw; }
      catch (Exception exception) { throw new RegionImportException($"Data tidak valid pada {file.Path}:{line}: {exception.Message}"); }
      if (!rows.TryAdd(row.Id, row)) throw new RegionImportException($"ID duplikat pada {file.Path}:{line}: {row.Id}.");
    }
    if (rows.Count != file.Count) throw new RegionImportException($"Jumlah baris {file.Path} adalah {rows.Count}, manifest menyatakan {file.Count}.");
    return rows;
  }

  private static ProvinceRow ParseProvince(string[] fields, int line) {
    EnsureFieldCount(fields, 3, line); return new(ParseId(fields[0]), ParseCode(fields[1], 2), ParseName(fields[2]));
  }
  private static RegencyRow ParseRegency(string[] fields, int line) {
    EnsureFieldCount(fields, 4, line); return new(ParseId(fields[0]), ParseCode(fields[1], 5), ParseId(fields[2]), ParseName(fields[3]));
  }
  private static DistrictRow ParseDistrict(string[] fields, int line) {
    EnsureFieldCount(fields, 4, line); return new(ParseId(fields[0]), ParseCode(fields[1], 8), ParseId(fields[2]), ParseName(fields[3]));
  }
  private static VillageRow ParseVillage(string[] fields, int line) {
    EnsureFieldCount(fields, 4, line); return new(ParseId(fields[0]), ParseCode(fields[1], 13), ParseId(fields[2]), ParseName(fields[3]));
  }
  private static long ParseId(string value) => long.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
  private static string ParseCode(string value, int length) {
    if (value.Length != length || value.Any(x => !char.IsDigit(x) && x != '.')) throw new FormatException($"Kode '{value}' tidak valid.");
    var normalized = value.Replace(".", string.Empty);
    _ = ParseId(normalized);
    return value;
  }
  private static string ParseName(string value) => string.IsNullOrWhiteSpace(value) ? throw new FormatException("Nama kosong.") : value.Trim();
  private static void EnsureFieldCount(string[] fields, int expected, int line) {
    if (fields.Length != expected) throw new RegionImportException($"Jumlah kolom pada baris {line} adalah {fields.Length}, seharusnya {expected}.");
  }

  private static void ValidateHierarchy(Dictionary<long, ProvinceRow> provinces, Dictionary<long, RegencyRow> regencies,
      Dictionary<long, DistrictRow> districts, Dictionary<long, VillageRow> villages) {
    foreach (var row in provinces.Values) EnsureNormalizedId(row.Id, row.Code);
    foreach (var row in regencies.Values) {
      EnsureNormalizedId(row.Id, row.Code);
      if (!provinces.ContainsKey(row.ProvinceId) || row.ProvinceId != NormalizedParentId(row.Code))
        throw new RegionImportException($"Parent Province untuk {row.Code} tidak valid.");
    }
    foreach (var row in districts.Values) {
      EnsureNormalizedId(row.Id, row.Code);
      if (!regencies.ContainsKey(row.RegencyId) || row.RegencyId != NormalizedParentId(row.Code))
        throw new RegionImportException($"Parent Regency untuk {row.Code} tidak valid.");
    }
    foreach (var row in villages.Values) {
      EnsureNormalizedId(row.Id, row.Code);
      if (!districts.ContainsKey(row.DistrictId) || row.DistrictId != NormalizedParentId(row.Code))
        throw new RegionImportException($"Parent District untuk {row.Code} tidak valid.");
    }
  }
  private static void EnsureNormalizedId(long id, string code) {
    if (id != ParseId(code.Replace(".", string.Empty))) throw new RegionImportException($"ID {id} tidak cocok dengan kode {code}.");
  }
  private static long NormalizedParentId(string code) => ParseId(code[..code.LastIndexOf('.')].Replace(".", string.Empty));

  private static void ValidateManifest(RegionDatasetManifest manifest) {
    if (manifest.SchemaVersion != 1) throw new RegionImportException($"Versi schema manifest {manifest.SchemaVersion} tidak didukung.");
    if (string.IsNullOrWhiteSpace(manifest.Version) || string.IsNullOrWhiteSpace(manifest.Source))
      throw new RegionImportException("Version dan source manifest wajib diisi.");
    if (manifest.GeneratedAt == default) throw new RegionImportException("generatedAt manifest tidak valid.");
    foreach (var file in manifest.Files.All) {
      if (string.IsNullOrWhiteSpace(file.Path) || file.Count <= 0 || !Sha256Pattern.IsMatch(file.Sha256))
        throw new RegionImportException("Metadata file pada manifest tidak valid.");
    }
    var fingerprintInput = string.Join('\n', manifest.Files.All.Select(x => x.Sha256.ToLowerInvariant())) + "\n";
    var calculatedFingerprint = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(fingerprintInput))).ToLowerInvariant();
    if (!Sha256Pattern.IsMatch(manifest.DatasetSha256) || manifest.DatasetSha256 != calculatedFingerprint)
      throw new RegionImportException("Fingerprint dataset pada manifest tidak valid.");
  }
  private static string ResolveChildPath(string baseDirectory, string relativePath) {
    if (Path.IsPathRooted(relativePath)) throw new RegionImportException("Path file dataset harus relatif terhadap manifest.");
    var path = Path.GetFullPath(Path.Combine(baseDirectory, relativePath));
    var prefix = Path.GetFullPath(baseDirectory) + Path.DirectorySeparatorChar;
    if (!path.StartsWith(prefix, StringComparison.Ordinal)) throw new RegionImportException("Path file dataset keluar dari direktori manifest.");
    return path;
  }
  private static string ResolveManifestPath(string manifestPath) {
    if (Path.IsPathRooted(manifestPath)) return Path.GetFullPath(manifestPath);

    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory is not null) {
      var candidate = Path.GetFullPath(Path.Combine(directory.FullName, manifestPath));
      if (File.Exists(candidate)) return candidate;
      directory = directory.Parent;
    }
    return Path.GetFullPath(manifestPath);
  }
  private static async Task<string> HashFileAsync(string path, CancellationToken ct) {
    await using var stream = File.OpenRead(path);
    return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
  }

  private interface IRegionRow { long Id { get; } }
  private sealed record ProvinceRow(long Id, string Code, string Name) : IRegionRow;
  private sealed record RegencyRow(long Id, string Code, long ProvinceId, string Name) : IRegionRow;
  private sealed record DistrictRow(long Id, string Code, long RegencyId, string Name) : IRegionRow;
  private sealed record VillageRow(long Id, string Code, long DistrictId, string Name) : IRegionRow;
}

public sealed class RegionDatasetManifest {
  public int SchemaVersion { get; set; }
  public string Version { get; set; } = string.Empty;
  public string Source { get; set; } = string.Empty;
  public DateTimeOffset GeneratedAt { get; set; }
  public string DatasetSha256 { get; set; } = string.Empty;
  public RegionDatasetFiles Files { get; set; } = new();
}
public sealed class RegionDatasetFiles {
  public RegionDatasetFile Provinces { get; set; } = new();
  public RegionDatasetFile Regencies { get; set; } = new();
  public RegionDatasetFile Districts { get; set; } = new();
  public RegionDatasetFile Villages { get; set; } = new();
  public IEnumerable<RegionDatasetFile> All => new[] { Provinces, Regencies, Districts, Villages };
}
public sealed class RegionDatasetFile {
  public string Path { get; set; } = string.Empty;
  public int Count { get; set; }
  public string Sha256 { get; set; } = string.Empty;
}
public sealed record RegionDatasetImportResult(
    string Version, bool AlreadyApplied, int Provinces, int Regencies, int Districts,
    int Villages, int ChangedRecords, DateTimeOffset AppliedAt);
