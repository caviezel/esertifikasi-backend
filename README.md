# Esertifikasi API

ASP.NET Core 8 API backed by PostgreSQL and Entity Framework Core.

## VS Code setup

Both projects explicitly use C# 12. Install or update Microsoft's C# extension
(`ms-dotnettools.csharp`) and open `esertifikasi-backend.sln` from this folder.
Use the Roslyn language server by setting `"dotnet.server.useOmnisharp": false`
in workspace settings. C# Dev Kit is optional.

Older C# extensions such as 1.25.0 cannot parse the collection expressions (`[]`)
and primary constructors used here, even when `dotnet build` succeeds. Update
the extension, then run **Developer: Reload Window** from the command palette.
Do not hide editor diagnostics to work around an outdated language server.

## Domain

- `Association` has many `Poktan`.
- `Poktan` belongs to one `Association` and has many `Petani`.
- `Petani` belongs to one `Poktan` and has many `Lahan`.
- `Lahan` belongs to one `Petani`.

Authorization follows the same hierarchy. Association administrators are scoped
to assigned associations, Poktan administrators to assigned Poktan, and approved
Tani Baik members to their linked Petani record.

## Run locally

All `appsettings` files are intentionally ignored. Configure secrets using
environment variables or .NET user secrets:

```bash
dotnet user-secrets init --project Esertifikasi.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5432;Database=esertifikasi;Username=postgres;Password=change-me" \
  --project Esertifikasi.Api
dotnet user-secrets set "Jwt:Key" "replace-with-at-least-32-random-characters" \
  --project Esertifikasi.Api
dotnet user-secrets set "BootstrapAdmin:Email" "admin@example.com" \
  --project Esertifikasi.Api
dotnet user-secrets set "BootstrapAdmin:Password" "replace-with-a-strong-password" \
  --project Esertifikasi.Api
```

Create the database and run the API:

```bash
dotnet restore
dotnet tool restore
dotnet tool run dotnet-ef database update \
  --project Esertifikasi.Api --startup-project Esertifikasi.Api
dotnet run --project Esertifikasi.Api
```

Swagger is available in Development at `/swagger`; health is at `/health`.
The configured bootstrap administrator is created on first startup and receives
the `SuperAdmin` role. Remove the bootstrap password configuration afterward.

## Access model

- `SuperAdmin`: global access and scoped administrator provisioning.
- `AssociationAdmin`: manages assigned associations and their descendants.
- `PoktanAdmin`: manages assigned Poktan and their descendants.
- `MemberTaniBaik`: read-only access to the linked Petani and Lahan data.

Tani Baik registration verification is deliberately disabled outside Development
until its OIDC/API contract is implemented. The Development-only bypass is
`TaniBaik:AllowUnverifiedDevelopmentRegistrations=true`.

## Document management

Petani and Lahan documents are private, versioned, and stored outside `wwwroot`.
The default development location is `Esertifikasi.Api/storage/documents`; override
it with `FileStorage:LocalPath`. Supported files are PDF, JPEG, and PNG, up to the
limit configured on each document type (maximum 10 MB).

SuperAdmin manages document types through `/api/document-types`. Administrators
upload documents through `/api/petani/{id}/documents` and
`/api/lahan/{id}/documents`. Shared version, download, preview, review, and delete
operations are under `/api/documents/{id}`. Files and metadata are soft-deleted;
physical cleanup is intentionally deferred for retention handling.

## Certification workflow

Creating an Association also creates its active Initial Certification cycle.
Certification data is cycle-specific and retains participant, Lahan, step,
disclosure, training, monitoring, land-mapping, baseline-assessment, audit,
finding, certificate, and workflow-transition history. The legacy Petani and
Lahan certification booleans remain available for client compatibility but are
not the source of certification history.

Cycle reads, enrollment, workflow transitions, progress, configuration, and
history are under `/api/certification-cycles`. Activity commands are under
`/api/certification`, while cycle, participant, and participant-Lahan documents
use their corresponding nested `/documents` endpoints. Association dashboards
can read the current state from
`/api/associations/{associationId}/certification-dashboard`.

Progress uses equal stage weights. Detailed activity targets and the partial
percentage awarded for a performed-but-incomplete audit are stored as cycle
configuration, allowing the pending client values to be supplied without a
schema change.

## Administrative regions

For production imports over an unreliable Mac-to-PostgreSQL connection, use the separate `import-regions-remote` workflow. It generates one self-contained SQL artifact, uploads over SSH/SCP, and runs psql locally on EC2. See [remote region import setup, confirmation, and commands](docs/region-import-remote.md). The existing `import-regions` command remains available.

Petani and Lahan store a nullable 64-bit `DesaId`. The API validates supplied
IDs against a local Province → Regency → District → Village reference hierarchy,
so normal requests do not depend on an external region service being available.

Authenticated clients can load cascading options from `/api/regions/provinces`,
`/api/regions/provinces/{id}/regencies`,
`/api/regions/regencies/{id}/districts`, and
`/api/regions/districts/{id}/villages`. Region data is read-only over HTTP and is
managed exclusively through the versioned command-line dataset importer.

The repository also contains a versioned full dataset in `data/regions`. It was
generated from `wilayah.sql` with:

```bash
python3 scripts/convert-wilayah-sql.py /path/to/wilayah.sql data/regions
```

After applying the database migration, import it from the repository root with
the normal application configuration (including the PostgreSQL connection
string) available:

```bash
dotnet run --project Esertifikasi.Api -- import-regions data/regions/manifest.json
```

The command validates file checksums, row counts, normalized codes, duplicate
IDs, and every parent relationship before writing. The four-level upsert and
dataset-version record are committed in one database transaction. Re-running an
already applied version with identical content is a no-op; reusing that version
for different content is rejected. Regions missing from a newer full dataset are
marked inactive rather than deleted.

`MemberTaniBaik` is strictly read-only: members can list, preview, and download
documents belonging to their linked Petani and Lahan, but cannot upload, replace,
submit, review, or delete them.

Lahan document requirements and uploads are available at
`GET /api/lahan/{lahanId}/documents/requirements` and
`POST /api/lahan/{lahanId}/documents`. Required seeded types cover Sertifikat
Tanah/SKT, SPPT PBB, Surat Keterangan Desa, four directional land photos, and
Peta Sketsa Lahan. Existing certification cycles are backfilled with the matching
participant-Lahan requirements when the application starts.

Lahan create and update requests may include `boundary` as GeoJSON with type
`MultiPolygon`. It is stored in PostgreSQL as `jsonb` and returned by the Lahan
detail endpoint. Coordinates use GeoJSON order `[longitude, latitude]`. The API
rejects invalid nesting, out-of-range coordinates, empty polygons/rings,
unclosed rings, fewer than three distinct ring positions, and payloads exceeding
50,000 positions. The polygon boundary is the canonical Lahan location; separate
latitude and longitude fields are not stored.

## Association documents

Association legal and organizational documents are stored as reusable master
documents under `/api/associations/{associationId}/documents`. Each document has
immutable versions through the standard `/api/documents/{id}/versions` endpoint.
The seeded required types are Akta Pendirian, SK Kemenkumham, NPWP Organisasi,
Surat Keterangan Domisili, Struktur Organisasi, AD/ART, Profil Organisasi, Daftar
Anggota, and Peta Wilayah Kerja. Completeness is available at
`/api/associations/{associationId}/documents/completeness`.

A certification cycle does not copy these files. Instead,
`/api/certification-cycles/{cycleId}/association-documents` attaches and records
the exact `DocumentVersion` submitted for each cycle requirement. Association
admins can attach versions and submit a complete package; SuperAdmin verifies or
rejects each item. A document pinned by a cycle submission cannot be deleted, so
later document versions never alter the historical submission.

Certification phase readiness is available at
`GET /api/certification-cycles/{cycleId}/readiness`. It returns the next phase,
whether transition is allowed, and structured blocker codes/counts covering
participant and Lahan confirmation, mapping, all baseline assessment types,
disclosure, audits/findings, and the certificate. `POST
/api/certification-cycles/{cycleId}/transition` applies the same checks server-side.
Required documents and training are reviewed when certificate
recipients are selected; monitoring is continuous master data. None of these blocks the
transition to Internal Audit. Certificate issuance revalidates required documents for
qualified recipients. Training attendance remains an admin-reviewed input until configurable
training rules are introduced; monitoring never contributes to cycle readiness or percentage.
Existing `RequiredTarget` fields are reserved for a future configurable training policy.

## Error responses

Unhandled application, database, and document errors are returned as safe Problem
Details responses with an HTTP status and `traceId`. Validation, authentication,
authorization, Identity, and application messages are currently presented in
Bahasa Indonesia. Internal exception details remain in server logs and are not
returned to clients.

## Tests

The integration-test project runs the real ASP.NET Core request pipeline with an
isolated EF Core database and in-memory file storage. It covers cross-scope access,
member read-only document permissions, soft deletion, Indonesian validation
responses, and safe global exception responses.

```bash
dotnet test esertifikasi-backend.sln
```

## Database migrations

The certification model currently has no generated migration. Generate the next
migration only after replacing or reverting the existing baseline migration as
appropriate for the target database.

```bash
dotnet tool restore
dotnet tool run dotnet-ef migrations add InitialEsertifikasi \
  --project Esertifikasi.Api --startup-project Esertifikasi.Api \
  --output-dir Migrations
dotnet tool run dotnet-ef database update \
  --project Esertifikasi.Api --startup-project Esertifikasi.Api
```

## Operations integration

- [Frontend guide: association training, field logs, and MICS](docs/operations-frontend-guide.md)
- [Manual migration and historical-data rollout](docs/operations-migration-notes.md)

Region import commits Province, Regency, and District as separate transactional stages in FK order. On PostgreSQL, Village import uses Npgsql Binary COPY into a transaction-local temporary table, then one server-side UPSERT by `Id` and a server-side update to deactivate missing villages. These Village operations commit together; failure rolls back the entire Village stage while retaining previously committed parent stages. The temporary table drops on commit and its creation is undone on rollback. Village writes bypass EF `SaveChangesAsync`; the existing in-memory/SQLite test paths retain their EF implementation. CSV parsing, checksums, and hierarchy validation are unchanged. The dataset completion marker is saved only after every stage succeeds. Existing command batching, command timeouts, and diagnostic stall reporting remain in place. Bulk-operation logs include elapsed time, input row counts, and affected/copied row counts.

PostgreSQL importer tests create and drop uniquely named test databases. Set `REGION_IMPORT_TEST_POSTGRES` to an isolated PostgreSQL server connection string with `CREATEDB` permission, then run:

```bash
dotnet test Esertifikasi.Api.IntegrationTests --filter FullyQualifiedName~RegionDatasetImporterTests
```

Without that variable, PostgreSQL tests are skipped; the in-memory/SQLite importer tests still run. The PostgreSQL tests exercise the checked-in 83,762-Village dataset, upserts, missing-row deactivation, rollback, temporary-table cleanup on the same open session, duplicate input, unique-code conflicts, and cancellation.

If an import is blocked by another transaction, PostgreSQL now stops the lock wait after 10 seconds and reports a region-import lock error. Inspect `pg_stat_activity` / `pg_blocking_pids`, and finish or roll back the blocking transaction before retrying. Do not terminate another session without confirming that its uncommitted work may be discarded.
