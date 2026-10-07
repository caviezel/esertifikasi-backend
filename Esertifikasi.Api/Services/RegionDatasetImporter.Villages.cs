using Esertifikasi.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace Esertifikasi.Api.Services;

public sealed partial class RegionDatasetImporter {
  private async Task<int> BulkUpsertVillagesAsync(Dictionary<long, VillageRow> incoming,
      DateTimeOffset sourceAt, DateTimeOffset now, CancellationToken ct) {
    var connection = (NpgsqlConnection)_db.Database.GetDbConnection();
    // Generated identifiers only. All later references explicitly use pg_temp so a
    // permanent table/search_path can never be mistaken for the staging table.
    var stagingName = $"region_village_stage_{Guid.NewGuid():N}";
    var staging = $"pg_temp.\"{stagingName}\"";
    var target = $"\"{DatabaseConstants.Schema}\".\"Village\"";
    const string columns = "\"Id\", \"Code\", \"DistrictId\", \"IsActive\", \"Name\", \"SourceUpdatedAt\", \"SyncedAt\"";
    var operation = "BeginTransaction";
    IDbContextTransaction? transaction = null;
    Exception? failure = null;

    Task<T> Observe<T>(string name, Func<Task<T>> action, Func<T, long>? rows = null) {
      operation = name;
      return ObserveOperationAsync(name, "Villages", 1, incoming.Count, incoming.Count, action, rows);
    }
    async Task ObserveVoid(string name, Func<Task> action) =>
        await Observe(name, async () => { await action(); return 0; }, x => x);
    Task<int> Execute(string name, string sql, params object[] parameters) =>
        Observe(name, () => _db.Database.ExecuteSqlRawAsync(sql, parameters, ct), x => Math.Max(0, x));

    try {
      ct.ThrowIfCancellationRequested();
      transaction = await Observe("BeginTransaction", () => _db.Database.BeginTransactionAsync(ct));
      await Execute("SetLocalLockTimeout", "SET LOCAL lock_timeout = '10s'");
      // LIKE copies the live column types and NOT NULL rules, but not identity
      // generation/defaults/FKs. Target constraints still apply to the UPSERT.
      await Execute("CreateStaging", $"""
          CREATE TEMP TABLE "{stagingName}"
          (LIKE {target}, PRIMARY KEY ("Id"), UNIQUE ("Code")) ON COMMIT DROP
          """);
      await Observe("BinaryCOPY", async () => {
        await using var writer = await connection.BeginBinaryImportAsync(
            $"COPY {staging} ({columns}) FROM STDIN (FORMAT BINARY)", ct);
        // Reuse the existing command timeout, including the CLI's configured value.
        writer.Timeout = TimeSpan.FromSeconds(_db.Database.GetCommandTimeout() ?? connection.CommandTimeout);
        foreach (var row in incoming.Values) {
          ct.ThrowIfCancellationRequested();
          await writer.StartRowAsync(ct);
          await writer.WriteAsync(row.Id, NpgsqlDbType.Bigint, ct);
          await writer.WriteAsync(row.Code, NpgsqlDbType.Varchar, ct);
          await writer.WriteAsync(row.DistrictId, NpgsqlDbType.Bigint, ct);
          await writer.WriteAsync(true, NpgsqlDbType.Boolean, ct);
          await writer.WriteAsync(row.Name, NpgsqlDbType.Varchar, ct);
          await writer.WriteAsync(sourceAt.ToUniversalTime(), NpgsqlDbType.TimestampTz, ct);
          await writer.WriteAsync(now.ToUniversalTime(), NpgsqlDbType.TimestampTz, ct);
        }
        var copied = await writer.CompleteAsync(ct);
        if (copied != (ulong)incoming.Count)
          throw new InvalidOperationException($"Village COPY completed with {copied} rows; expected {incoming.Count}.");
        return copied;
      }, x => checked((long)x));

      // Preserve ChangedRecords semantics: timestamp refreshes alone are not changes.
      var changed = await Observe("CountChanges", async () => {
        await using var command = new NpgsqlCommand($"""
            SELECT count(*) FROM {staging} AS s
            LEFT JOIN {target} AS v ON v."Id" = s."Id"
            WHERE v."Id" IS NULL OR
              (v."Code", v."DistrictId", v."IsActive", v."Name") IS DISTINCT FROM
              (s."Code", s."DistrictId", s."IsActive", s."Name")
            """, connection, (NpgsqlTransaction)transaction.GetDbTransaction());
        command.CommandTimeout = _db.Database.GetCommandTimeout() ?? connection.CommandTimeout;
        return checked((int)(long)(await command.ExecuteScalarAsync(ct))!);
      }, x => x);

      await Execute("Upsert", $"""
          INSERT INTO {target} ({columns})
          SELECT {columns} FROM {staging}
          ON CONFLICT ("Id") DO UPDATE SET
            "Code" = EXCLUDED."Code",
            "DistrictId" = EXCLUDED."DistrictId",
            "IsActive" = EXCLUDED."IsActive",
            "Name" = EXCLUDED."Name",
            "SourceUpdatedAt" = EXCLUDED."SourceUpdatedAt",
            "SyncedAt" = EXCLUDED."SyncedAt"
          """);
      var deactivated = await Execute("Deactivate", $"""
          UPDATE {target} AS v SET "IsActive" = FALSE, "SyncedAt" = @syncedAt
          WHERE v."IsActive" AND NOT EXISTS
            (SELECT 1 FROM {staging} AS s WHERE s."Id" = v."Id")
          """, new NpgsqlParameter("syncedAt", NpgsqlDbType.TimestampTz) { Value = now.ToUniversalTime() });
      await ObserveVoid("Commit", () => transaction.CommitAsync(ct));
      _logger?.LogInformation("Villages: {Processed}/{Total}; ChangedRows={ChangedRows} DeactivatedRows={DeactivatedRows}",
          incoming.Count, incoming.Count, changed, deactivated);
      return checked(changed + deactivated);
    }
    catch (Exception error) {
      failure = error;
      var failedOperation = operation;
      if (transaction is not null) {
        try { await ObserveVoid("Rollback", () => transaction.RollbackAsync(CancellationToken.None)); }
        catch (Exception rollbackError) {
          _logger?.LogWarning(rollbackError, "Village bulk rollback failed; original import exception preserved.");
        }
      }
      _logger?.LogError(error, "Village bulk import failed during {Operation}; Rows={Rows}.", failedOperation, incoming.Count);
      if (error is OperationCanceledException) throw;
      for (Exception? cause = error; cause is not null; cause = cause.InnerException) {
        if (cause is PostgresException { SqlState: "55P03" })
          throw new RegionImportException($"Villages bulk {failedOperation}: Import wilayah diblokir oleh transaksi PostgreSQL lain. Previously committed parent stages are retained.", error);
      }
      throw new RegionImportException($"Villages bulk {failedOperation}: Region import failed; previously committed parent stages are retained.", error);
    }
    finally {
      LogOperation("BEFORE", "Clear", "Villages", 1, incoming.Count, incoming.Count, 0);
      _db.ChangeTracker.Clear();
      LogOperation("AFTER", "Clear", "Villages", 1, incoming.Count, incoming.Count, 0);
      if (transaction is not null) {
        // ON COMMIT DROP removes staging on success; rolling back CREATE TEMP TABLE
        // removes it on failure. Dispose also rolls back an uncommitted transaction.
        try { await ObserveVoid("Dispose", () => transaction.DisposeAsync().AsTask()); }
        catch (Exception disposeError) when (failure is not null) {
          _logger?.LogWarning(disposeError, "Village bulk transaction disposal failed; original import exception preserved.");
        }
        catch (Exception disposeError) {
          throw new RegionImportException("Villages bulk Dispose: Village stage committed, but transaction disposal failed.", disposeError);
        }
      }
    }
  }
}
