# Remote production region import

The direct `import-regions` command and its Binary COPY Village path remain available. `import-regions-remote` instead validates on the Mac, uploads one SQL artifact through SSH/SCP, and runs psql on EC2 against **127.0.0.1:5432**. It does not initialize the API's DbContext, seeder, or JWT authentication and does not connect directly to PostgreSQL from the Mac.

## Configuration on the Mac

Configure the `RemoteImport` section in local configuration or these environment variables. Empty defaults deliberately prevent execution against an unspecified target. Replace the examples with your actual EC2 login, key path, and database role; there are no passwords in this configuration.

A non-secret JSON template is provided in [remote-import.example.json](remote-import.example.json). Merge its `RemoteImport` section into your local, git-ignored `Esertifikasi.Api/appsettings.json`, or use the environment variables below. The template itself is documentation and is not automatically loaded.

```bash
export RemoteImport__Host='YOUR_EC2_DNS_NAME'
export RemoteImport__Port='22'
export RemoteImport__User='YOUR_EC2_SSH_USER'
export RemoteImport__IdentityFile='/absolute/path/to/your-key.pem'
export RemoteImport__RemoteTempDirectory='/tmp'
export RemoteImport__PsqlPath='/usr/bin/psql'
export RemoteImport__DatabaseName='YOUR_DATABASE_NAME'
export RemoteImport__DatabaseUser='YOUR_DATABASE_ROLE'
```

`IdentityFile` may be empty when using an SSH agent/default OpenSSH identity. Tilde expansion is not performed. Plain DNS/IPv4 hosts, simple database/login identifiers, and absolute POSIX paths without shell metacharacters or `.`/`..` components are supported. Confirm the actual `psql` location on EC2. The remote temporary parent directory must already exist and be writable by the SSH user. A unique mode-0700 subdirectory is created for every execution.

Preview and validate (no SSH, upload, artifact, or database mutation):

```bash
dotnet run --project Esertifikasi.Api -- import-regions-remote data/regions/manifest.json
```

First production test after configuring and verifying EC2:

```bash
dotnet run --project Esertifikasi.Api -- import-regions-remote data/regions/manifest.json --confirm
```

The terminal displays the remote host, explicit database name, and all four dataset counts before executing. `--confirm` is required for the remote mutation. Unknown flags fail with exit code 2. Do not run the direct and remote importers concurrently.

## One-time EC2 setup

No existing EC2 PostgreSQL authentication or deployment configuration is represented in this repository. Have the server administrator verify the following; the command does not create database credentials or change PostgreSQL settings.

1. Install the PostgreSQL `psql` client on EC2 and verify the configured executable. The database and current application migrations must already exist, including the `esertifikasi` schema and `RegionDatasetImport` table.
2. Verify key-based SSH access from the Mac. Verify EC2's SSH host-key fingerprint through a trusted channel and add it to your Mac's `known_hosts` before running this noninteractive command. It uses `BatchMode=yes` and `StrictHostKeyChecking=yes`; it never accepts unknown keys automatically. Unlock a passphrase-protected key in the SSH agent if needed. Mac requires `ssh` and `scp` in PATH; no SSH library is added.
3. Configure secure authentication for the **SSH user's EC2-side psql process**, for example `~/.pgpass`, mode **0600**, containing a matching entry in this format:

   ```text
   127.0.0.1:5432:YOUR_DATABASE_NAME:YOUR_DATABASE_ROLE:YOUR_DATABASE_PASSWORD
   ```

   Create/edit that file securely on EC2; never place the real password in this repository, shell command arguments, or the SQL artifact. Escape `:` and `\` inside pgpass fields. Server-side `PGPASSFILE` or another already-configured noninteractive libpq authentication method may also be used. `-w` prohibits password prompts. Existing EC2 authentication must permit this role to connect locally. See [PostgreSQL password-file documentation](https://www.postgresql.org/docs/16/libpq-pgpass.html).
4. The database role needs CONNECT and TEMP on the database; USAGE on `esertifikasi`; SELECT, INSERT, UPDATE on Province, Regency, District, Village, and RegionDatasetImport. The completion-table lock also requires UPDATE privilege. No superuser, server-side file-read privilege, permanent staging-table DDL, or bulk-extension package is needed. Do not grant broader privileges merely for this importer.
5. Verify the authentication as the SSH user on EC2, using the actual database and role:

   ```bash
   /usr/bin/psql -X -w -h 127.0.0.1 -p 5432 -d YOUR_DATABASE_NAME -U YOUR_DATABASE_ROLE -c 'SELECT current_database(), current_user;'
   ```

## SQL and transaction behavior

The artifact is UTF-8 without a BOM, mode 0600 locally, and contains inline PostgreSQL **text COPY FROM stdin** sections for all four region staging tables plus import metadata. There are no companion CSV files or independent per-row INSERT statements. Text COPY escapes tabs, line breaks, backslashes, and nulls; apostrophes, quotes, Unicode, and literal `\N`/`\.` remain data. PostgreSQL text cannot store NUL, so generation rejects it before upload. All current region fields are NOT NULL; null encoding does not relax schema constraints. See [COPY format documentation](https://www.postgresql.org/docs/16/sql-copy.html).

Staging tables inherit the real production table column types using `LIKE`, with staging primary-key/unique-code checks. The target's foreign keys, unique indexes, lengths, and other constraints still apply. A single transaction stages data, checks the dataset marker, upserts Province → Regency → District → Village by `Id`, deactivates missing active rows, and inserts the completion marker. Unlike the direct path's independent stage commits, **this remote workflow rolls back all four types together on SQL failure**.

A transaction-scoped lock on RegionDatasetImport serializes this workflow's marker checks. A previously applied matching version/fingerprint is a no-op; version reuse with a different fingerprint is an error. Business-field inserts/updates/reactivations and newly deactivated rows contribute to the reported `ChangedRecords`; timestamp-only refreshes do not. Missing-row deactivation changes IsActive and SyncedAt and preserves SourceUpdatedAt. Temp tables use `ON COMMIT DROP`; rollback undoes their creation. No PostgreSQL configuration or existing direct-import timeout/batching is changed.

psql runs with `-X -w -v ON_ERROR_STOP=1 -h 127.0.0.1 -p 5432 -d ... -U ... -f ...`. The artifact also sets ON_ERROR_STOP. Progress notices and COPY counts are relayed locally. Success requires both SSH exit code 0 and the artifact's post-COMMIT marker. SQL errors stop psql, return non-zero, and roll back the open transaction when its connection closes. There are no automatic retries. See [psql error-handling documentation](https://www.postgresql.org/docs/16/app-psql.html).

Local processes use `ProcessStartInfo.ArgumentList` with `UseShellExecute=false`. Because SSH's server executes remote commands using the login shell, each remote token is separately POSIX-quoted, and configuration is validated before upload. Credentials are not passed as process arguments, printed, or written to the artifact.

## Failure, cancellation, and cleanup

Upload/directory creation failure prevents psql execution. Failures return exit code 1; Ctrl-C cancels subprocesses and returns 130. Stdout and stderr drain concurrently. After an acknowledged directory creation, cleanup uses separate SSH calls to remove only the generated artifact and then its unique directory, with no recursive deletion. Local artifacts are removed after success or failure. Cleanup uses an independent cancellation token so Ctrl-C does not immediately cancel cleanup. Warnings identify leftover artifact paths without masking the original failure or a confirmed commit.

An SSH disconnect or canceled SSH process **cannot prove that the remote psql process stopped or rolled back**. The transaction may have committed while its acknowledgment was lost. The tool reports this uncertainty and never retries. On EC2, check the dataset's Version and DatasetSha256 in `esertifikasi."RegionDatasetImport"` and check whether its psql process is still running before considering another invocation. If directory creation itself was not acknowledged, the tool reports its generated path for inspection rather than deleting a directory it cannot confirm it created.

## Tests

Workflow tests mock SSH/SCP; none contacts production. Real PostgreSQL tests create/drop uniquely named test databases, and real artifact tests invoke a local psql client. Supply only an isolated test server with CREATEDB permission. A password-authenticated test server requires corresponding local pgpass authentication for psql.

```bash
export REGION_IMPORT_TEST_POSTGRES='Host=127.0.0.1;Port=55483;Database=postgres;Username=postgres;Pooling=false'
export REGION_IMPORT_TEST_PSQL='/absolute/path/to/psql'
dotnet test Esertifikasi.Api.IntegrationTests --filter 'FullyQualifiedName~RegionDatasetImporterTests|FullyQualifiedName~RemoteRegionImporterTests|FullyQualifiedName~PostgresRegionSqlArtifactTests'
```

Without these test variables, external PostgreSQL/psql tests are explicitly skipped; validation, generation, process-runner, safety, and mocked transport tests still run. The real tests include the complete 38 Province / 514 Regency / 7,285 District / 83,762 Village dataset, Unicode/escaping, dependency order, upserts, all-level deactivation, idempotence, marker conflicts, rollback, and staging cleanup.
