using System.Globalization;
using System.Text;
using Esertifikasi.Api.Infrastructure;

namespace Esertifikasi.Api.Services;

public sealed record RegionSqlArtifact(string Path, long Bytes);

public sealed class RegionSqlArtifactGenerator {
  public const string CommitMarker = "REGION_IMPORT_COMMITTED";
  private static readonly (string Table, string Stage, string? Parent)[] Tables = {
    ("Province", "region_stage_province", null),
    ("Regency", "region_stage_regency", "ProvinceId"),
    ("District", "region_stage_district", "RegencyId"),
    ("Village", "region_stage_village", "DistrictId")
  };

  public async Task<RegionSqlArtifact> GenerateAsync(ValidatedRegionDataset dataset, CancellationToken ct) {
    var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"region-import-{Guid.NewGuid():N}.sql");
    var fileOptions = new FileStreamOptions {
      Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
      Options = FileOptions.Asynchronous
    };
    if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    try {
      await using (var file = new FileStream(path, fileOptions)) {
        await using var writer = new StreamWriter(file, new UTF8Encoding(false, true)) { NewLine = "\n" };
        Task Line(string value) => writer.WriteLineAsync(value.AsMemory(), ct);
        await Line("\\set ON_ERROR_STOP on\n\\encoding UTF8\nBEGIN;");
        foreach (var (table, stage, _) in Tables)
          await Line($"CREATE TEMP TABLE {stage} (LIKE {Target(table)}, PRIMARY KEY (\"Id\"), UNIQUE (\"Code\")) ON COMMIT DROP;");
        await Line($"CREATE TEMP TABLE region_stage_import (LIKE {Target("RegionDatasetImport")}) ON COMMIT DROP;");
        await Line("\\echo staging created");
        var syncedAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var sourceAt = dataset.Manifest.GeneratedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        var rows = new[] { dataset.Provinces, dataset.Regencies, dataset.Districts, dataset.Villages };
        for (var i = 0; i < Tables.Length; i++) {
          var (_, stage, parent) = Tables[i];
          await Line($"COPY pg_temp.{stage} ({Columns(parent)}) FROM stdin WITH (FORMAT text, ENCODING 'UTF8');");
          foreach (var row in rows[i]) {
            ct.ThrowIfCancellationRequested();
            var values = new List<string?> { row.Id.ToString(CultureInfo.InvariantCulture), row.Code };
            if (parent is not null) values.Add(row.ParentId?.ToString(CultureInfo.InvariantCulture));
            values.AddRange(new[] { row.Name, "t", sourceAt, syncedAt });
            await Line(string.Join('\t', values.Select(EncodeCopyField)));
          }
          await Line("\\.");
          await Line($"\\echo {Tables[i].Table} staging loaded: {rows[i].Count}");
        }
        const string metadataColumns = "\"Id\", \"Version\", \"Source\", \"DatasetSha256\", \"ProvinceCount\", \"RegencyCount\", \"DistrictCount\", \"VillageCount\", \"AppliedAt\"";
        await Line($"COPY pg_temp.region_stage_import ({metadataColumns}) FROM stdin WITH (FORMAT text, ENCODING 'UTF8');");
        await Line(string.Join('\t', new[] {
          Guid.NewGuid().ToString(), dataset.Manifest.Version, dataset.Manifest.Source, dataset.Manifest.DatasetSha256,
          dataset.Provinces.Count.ToString(CultureInfo.InvariantCulture), dataset.Regencies.Count.ToString(CultureInfo.InvariantCulture),
          dataset.Districts.Count.ToString(CultureInfo.InvariantCulture), dataset.Villages.Count.ToString(CultureInfo.InvariantCulture), syncedAt
        }.Select(EncodeCopyField)));
        await Line("\\.");
        // Serialize remote imports around the marker check. This is transaction
        // ownership, not a PostgreSQL configuration change or retry mechanism.
        await Line($"LOCK TABLE {Target("RegionDatasetImport")} IN SHARE ROW EXCLUSIVE MODE;");
        await Line($"""
            DO $region_import$
            DECLARE
              metadata pg_temp.region_stage_import%ROWTYPE;
              previous_hash text;
              changed bigint := 0;
              affected bigint;
              stage_changes bigint;
            BEGIN
              SELECT * INTO STRICT metadata FROM pg_temp.region_stage_import;
              SELECT "DatasetSha256" INTO previous_hash FROM {Target("RegionDatasetImport")} WHERE "Version" = metadata."Version";
              IF FOUND THEN
                IF previous_hash IS DISTINCT FROM metadata."DatasetSha256" THEN
                  RAISE EXCEPTION 'Dataset version already belongs to a different fingerprint';
                END IF;
                RAISE NOTICE 'Dataset already applied; ChangedRecords=0';
                RETURN;
              END IF;
            """);
        for (var i = 0; i < Tables.Length; i++) {
          var (table, stage, parent) = Tables[i];
          var business = parent is null ? "\"Code\", \"Name\", \"IsActive\"" : $"\"Code\", \"Name\", \"IsActive\", \"{parent}\"";
          string Tuple(string alias) => string.Join(", ", business.Split(", ").Select(x => alias + "." + x));
          var assignments = Columns(parent).Split(", ").Where(x => x != "\"Id\"").Select(x => $"{x} = EXCLUDED.{x}");
          await Line($"""
                IF (SELECT count(*) FROM pg_temp.{stage}) <> metadata."{table}Count" THEN
                  RAISE EXCEPTION '{table} staging row count mismatch';
                END IF;
                SELECT count(*) INTO stage_changes FROM pg_temp.{stage} AS s
                LEFT JOIN {Target(table)} AS t ON t."Id" = s."Id"
                WHERE t."Id" IS NULL OR ({Tuple("t")}) IS DISTINCT FROM ({Tuple("s")});
                changed := changed + stage_changes;
                INSERT INTO {Target(table)} ({Columns(parent)})
                SELECT {Columns(parent)} FROM pg_temp.{stage}
                ON CONFLICT ("Id") DO UPDATE SET {string.Join(", ", assignments)};
                GET DIAGNOSTICS affected = ROW_COUNT;
                RAISE NOTICE '{table} processed: rows=%, changed=%', affected, stage_changes;
              """);
        }
        foreach (var (table, stage, _) in Tables) {
          await Line($"""
                UPDATE {Target(table)} AS t SET "IsActive" = FALSE, "SyncedAt" = metadata."AppliedAt"
                WHERE t."IsActive" AND NOT EXISTS (SELECT 1 FROM pg_temp.{stage} AS s WHERE s."Id" = t."Id");
                GET DIAGNOSTICS affected = ROW_COUNT;
                changed := changed + affected;
                RAISE NOTICE '{table} missing records deactivated: %', affected;
              """);
        }
        await Line($"""
              INSERT INTO {Target("RegionDatasetImport")} ({metadataColumns})
              SELECT {metadataColumns} FROM pg_temp.region_stage_import;
              RAISE NOTICE 'ChangedRecords=%', changed;
            END;
            $region_import$;
            COMMIT;
            \echo {CommitMarker}
            \echo transaction committed
            """);
      }
      return new RegionSqlArtifact(path, new FileInfo(path).Length);
    }
    catch {
      File.Delete(path);
      throw;
    }
  }

  private static string Target(string table) => $"\"{DatabaseConstants.Schema}\".\"{table}\"";
  private static string Columns(string? parent) => "\"Id\", \"Code\", " +
      (parent is null ? "" : $"\"{parent}\", ") + "\"Name\", \"IsActive\", \"SourceUpdatedAt\", \"SyncedAt\"";

  // PostgreSQL text COPY escaping, not SQL literal escaping. Literal backslashes
  // are doubled so \N / \. / psql metacommands in text cannot become controls.
  public static string EncodeCopyField(string? value) {
    if (value is null) return "\\N";
    var encoded = new StringBuilder(value.Length);
    foreach (var character in value) {
      encoded.Append(character switch {
        '\0' => throw new RegionImportException("Region text contains NUL, which PostgreSQL text cannot store."),
        '\\' => "\\\\", '\t' => "\\t", '\n' => "\\n", '\r' => "\\r",
        '\b' => "\\b", '\f' => "\\f", '\v' => "\\v", _ => character.ToString()
      });
    }
    return encoded.ToString();
  }
}
