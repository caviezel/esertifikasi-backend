using System.Collections.Concurrent;
using Esertifikasi.Api.Services;

namespace Esertifikasi.Api.IntegrationTests.Regions;

public sealed class RemoteRegionImporterTests {
  [Fact]
  public async Task SqlGeneration_ProducesOneCopyArtifactWithCountsAndDependencyOrder() {
    using var source = new RemoteImportTestDataset();
    await source.WriteAsync(new[] { "O'Brien \"quoted\" 雨\\N\\.\ttab\nnewline", "Desa Dua" });
    var dataset = await RegionDatasetImporter.ReadValidatedDatasetAsync(source.ManifestPath, CancellationToken.None);
    Assert.Equal(2, dataset.Villages.Count);
    var artifact = await new RegionSqlArtifactGenerator().GenerateAsync(dataset, CancellationToken.None);
    try {
      var sql = await File.ReadAllTextAsync(artifact.Path);
      Assert.Equal(new FileInfo(artifact.Path).Length, artifact.Bytes);
      Assert.Equal(5, sql.Split("FROM stdin WITH").Length - 1);
      Assert.Contains("Village staging loaded: 2", sql);
      Assert.Contains("O'Brien \"quoted\" 雨\\\\N\\\\.\\ttab\\nnewline", sql);
      var tables = new[] { "Province", "Regency", "District", "Village", "RegionDatasetImport" };
      var positions = tables.Select(x => sql.IndexOf($"INSERT INTO \"esertifikasi\".\"{x}\"", StringComparison.Ordinal)).ToArray();
      Assert.All(positions, x => Assert.True(x > 0));
      Assert.Equal(positions.Order(), positions);
      Assert.Contains("BEGIN;", sql);
      Assert.Contains("ON COMMIT DROP", sql);
      Assert.Contains("ON CONFLICT (\"Id\") DO UPDATE SET", sql);
      Assert.Contains("COMMIT;", sql);
      Assert.DoesNotContain("INSERT INTO \"esertifikasi\".\"Village\" VALUES", sql);
      if (!OperatingSystem.IsWindows()) Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(artifact.Path));
    }
    finally { File.Delete(artifact.Path); }
  }

  [Theory]
  [InlineData(null, "\\N")]
  [InlineData("\\N", "\\\\N")]
  [InlineData("\\.", "\\\\.")]
  [InlineData("'\"é雨\\\t\n\r\b\f\v", "'\"é雨\\\\\\t\\n\\r\\b\\f\\v")]
  public void CopyEncoding_PreservesTextAndNulls(string? value, string expected) => Assert.Equal(expected, RegionSqlArtifactGenerator.EncodeCopyField(value));

  [Fact]
  public async Task WithoutConfirm_OnlyValidatesAndNeverStartsRemoteProcesses() {
    using var source = new RemoteImportTestDataset();
    await source.WriteAsync();
    var runner = new FakeRunner();
    var output = new List<string>();
    await Workflow(runner).RunAsync(source.ManifestPath, false, new RemoteRegionImportOptions(), output.Add, CancellationToken.None);
    Assert.Empty(runner.Commands);
    Assert.Contains(output, x => x.Contains("--confirm"));
    Assert.DoesNotContain(output, x => x.Contains("Artifact:"));
  }

  [Fact]
  public async Task Cli_WithoutConfirmExitsSuccessfullyWithoutConnectingToDatabase() {
    using var source = new RemoteImportTestDataset();
    await source.WriteAsync();
    var output = new ConcurrentQueue<string>();
    var errors = new ConcurrentQueue<string>();
    var command = new ImportProcessCommand("dotnet", new[] {
      typeof(RegionDatasetImporter).Assembly.Location, "import-regions-remote", source.ManifestPath
    });
    var exit = await new ImportProcessRunner().RunAsync(command, output.Enqueue, errors.Enqueue, CancellationToken.None);
    Assert.Equal(0, exit);
    Assert.Contains(output, x => x.Contains("No remote commands were run"));
    Assert.Empty(errors);
  }

  [Fact]
  public async Task Success_CleansLocalAndRemoteFilesAndNeverWritesKeyCredentials() {
    using var source = new RemoteImportTestDataset();
    await source.WriteAsync();
    var keyPath = Path.Combine(source.DirectoryPath, "private key.pem");
    const string secret = "TEST_PRIVATE_KEY_SECRET_DO_NOT_LEAK";
    await File.WriteAllTextAsync(keyPath, secret);
    var options = Options();
    options.IdentityFile = keyPath;
    var runner = new FakeRunner();
    var output = new List<string>();
    await Workflow(runner).RunAsync(source.ManifestPath, true, options, output.Add, CancellationToken.None);
    Assert.False(File.Exists(runner.LocalArtifact));
    Assert.Contains(runner.Commands, x => x.Arguments.Last().StartsWith("'/bin/rm' '-f' '--' '/tmp/region-import-"));
    Assert.Contains(runner.Commands, x => x.Arguments.Last().StartsWith("'/bin/rmdir' '--' '/tmp/region-import-"));
    Assert.Contains(output, x => x.Contains("completed successfully"));
    Assert.DoesNotContain(secret, runner.ArtifactText);
    Assert.DoesNotContain(secret, string.Join('\n', output));
    Assert.DoesNotContain(keyPath, string.Join('\n', output));
  }

  [Theory]
  [InlineData("mkdir", 255)]
  [InlineData("scp", 1)]
  [InlineData("psql", 3)]
  [InlineData("psql", 255)]
  public async Task Failure_StopsExecutionReportsExitAndCleansFiles(string failureStage, int exit) {
    using var source = new RemoteImportTestDataset();
    await source.WriteAsync();
    var runner = new FakeRunner { FailureStage = failureStage, Exit = exit };
    var output = new List<string>();
    var error = await Assert.ThrowsAsync<RegionImportException>(() => Workflow(runner).RunAsync(source.ManifestPath, true, Options(), output.Add, CancellationToken.None));
    Assert.Contains($"exit {exit}", error.Message);
    Assert.Contains("injected stderr", error.Message);
    Assert.DoesNotContain(output, x => x.Contains("completed successfully"));
    if (failureStage != "psql") Assert.DoesNotContain(runner.Commands, IsPsql);
    if (runner.LocalArtifact is not null) Assert.False(File.Exists(runner.LocalArtifact));
    if (failureStage != "mkdir") Assert.Contains(runner.Commands, x => x.Arguments.Last().StartsWith("'/bin/rm'"));
    var artifactLine = output.Single(x => x.StartsWith("Artifact:"));
    Assert.False(File.Exists(artifactLine.Split('\n')[0]["Artifact: ".Length..]));
  }

  [Fact]
  public async Task MissingCommitMarker_IsNeverReportedAsSuccess() {
    using var source = new RemoteImportTestDataset();
    await source.WriteAsync();
    var runner = new FakeRunner { EmitCommit = false };
    var output = new List<string>();
    await Assert.ThrowsAsync<RegionImportException>(() => Workflow(runner).RunAsync(source.ManifestPath, true, Options(), output.Add, CancellationToken.None));
    Assert.False(File.Exists(runner.LocalArtifact));
    Assert.DoesNotContain(output, x => x.Contains("completed successfully"));
  }

  [Fact]
  public async Task CleanupFailure_IsReportedWithoutMaskingCommittedImport() {
    using var source = new RemoteImportTestDataset();
    await source.WriteAsync();
    var runner = new FakeRunner { FailureStage = "rm", Exit = 1 };
    var output = new List<string>();
    await Workflow(runner).RunAsync(source.ManifestPath, true, Options(), output.Add, CancellationToken.None);
    Assert.Contains(output, x => x.Contains("Cleanup warning"));
    Assert.Contains(output, x => x.Contains("completed successfully"));
    Assert.False(File.Exists(runner.LocalArtifact));
  }

  [Fact]
  public async Task GenerationFailure_UploadsNothing() {
    using var source = new RemoteImportTestDataset();
    await source.WriteAsync(new[] { "invalid\0name" });
    var runner = new FakeRunner();
    await Assert.ThrowsAsync<RegionImportException>(() => Workflow(runner).RunAsync(source.ManifestPath, true, Options(), _ => { }, CancellationToken.None));
    Assert.Empty(runner.Commands);
  }

  [Fact]
  public async Task Cancellation_KillsExecutionAndStillCleansWithAnIndependentToken() {
    using var source = new RemoteImportTestDataset();
    await source.WriteAsync();
    using var cts = new CancellationTokenSource();
    var runner = new FakeRunner { CancelOnUpload = cts };
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Workflow(runner).RunAsync(source.ManifestPath, true, Options(), _ => { }, cts.Token));
    Assert.False(File.Exists(runner.LocalArtifact));
    Assert.DoesNotContain(runner.Commands, IsPsql);
    Assert.Contains(runner.Commands, x => x.Arguments.Last().StartsWith("'/bin/rm'"));
    Assert.All(runner.CleanupTokens, x => Assert.False(x.IsCancellationRequested));
  }

  [Theory]
  [InlineData("Host", "server;touch /tmp/injected")]
  [InlineData("Host", "-oProxyCommand=evil")]
  [InlineData("User", "user$(id)")]
  [InlineData("DatabaseName", "host=evil password=secret")]
  [InlineData("DatabaseName", "db'; DROP DATABASE prod;--")]
  [InlineData("DatabaseUser", "-Uevil")]
  [InlineData("RemoteTempDirectory", "/tmp/../home")]
  [InlineData("RemoteTempDirectory", "/tmp/$(id)")]
  [InlineData("PsqlPath", "/bin/psql;evil")]
  public void Configuration_RejectsOptionsConnectionStringsAndMetacharacters(string property, string value) {
    var options = Options();
    typeof(RemoteRegionImportOptions).GetProperty(property)!.SetValue(options, value);
    Assert.Throws<RegionImportException>(options.Validate);
  }

  [Fact]
  public void CommandArguments_AreNoninteractiveAndRemoteTokensAreQuoted() {
    var options = Options();
    var command = RemoteRegionImportCommands.Execute(options, "/tmp/region-import-id/artifact.sql");
    Assert.Equal("ssh", command.FileName);
    Assert.Contains("StrictHostKeyChecking=yes", command.Arguments);
    Assert.Contains("BatchMode=yes", command.Arguments);
    Assert.Contains("'-X' '-w' '-v' 'ON_ERROR_STOP=1' '-h' '127.0.0.1'", command.Arguments.Last());
    Assert.Contains("'-d' 'myappdb' '-U' 'importer'", command.Arguments.Last());
    Assert.Equal("'a'\"'\"';$(id)'", RemoteRegionImportCommands.QuotePosix("a';$(id)"));
  }

  [Fact]
  public async Task ProcessRunner_DrainsBothStreamsAndReturnsTheActualExitCode() {
    var stdout = new ConcurrentQueue<string>();
    var stderr = new ConcurrentQueue<string>();
    var command = new ImportProcessCommand("python3", new[] { "-c", "import sys; print(sys.argv[1]); print('stderr',file=sys.stderr); sys.exit(7)", "' ; $(id) 雨" });
    var exit = await new ImportProcessRunner().RunAsync(command, stdout.Enqueue, stderr.Enqueue, CancellationToken.None);
    Assert.Equal(7, exit);
    Assert.Contains("' ; $(id) 雨", stdout);
    Assert.Contains("stderr", stderr);
  }

  [Fact]
  public async Task ProcessRunner_CancellationTerminatesTheChild() {
    using var cts = new CancellationTokenSource();
    var command = new ImportProcessCommand("python3", new[] { "-u", "-c", "import time; print('started',flush=True); time.sleep(60)" });
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ImportProcessRunner().RunAsync(command,
        _ => cts.Cancel(), _ => { }, cts.Token).WaitAsync(TimeSpan.FromSeconds(10)));
  }

  private static RemoteRegionImporter Workflow(FakeRunner runner) => new(runner, new RegionSqlArtifactGenerator());
  private static RemoteRegionImportOptions Options() => new() { Host = "ec2.example.test", User = "ec2-user", DatabaseName = "myappdb", DatabaseUser = "importer" };
  private static bool IsPsql(ImportProcessCommand command) => command.FileName == "ssh" && command.Arguments.Last().Contains("'ON_ERROR_STOP=1'");

  private sealed class FakeRunner : IImportProcessRunner {
    public List<ImportProcessCommand> Commands { get; } = new();
    public List<CancellationToken> CleanupTokens { get; } = new();
    public string? LocalArtifact { get; private set; }
    public string ArtifactText { get; private set; } = "";
    public string? FailureStage { get; init; }
    public int Exit { get; init; }
    public bool EmitCommit { get; init; } = true;
    public CancellationTokenSource? CancelOnUpload { get; init; }
    public Task<int> RunAsync(ImportProcessCommand command, Action<string> stdout, Action<string> stderr, CancellationToken ct) {
      Commands.Add(command);
      var stage = command.FileName == "scp" ? "scp" : IsPsql(command) ? "psql" :
          command.Arguments.Last().StartsWith("'/bin/mkdir'") ? "mkdir" :
          command.Arguments.Last().StartsWith("'/bin/rm'") ? "rm" : "rmdir";
      if (stage == "scp") {
        LocalArtifact = command.Arguments[^2];
        ArtifactText = File.ReadAllText(LocalArtifact);
        if (CancelOnUpload is not null) { CancelOnUpload.Cancel(); ct.ThrowIfCancellationRequested(); }
      }
      if (stage is "rm" or "rmdir") CleanupTokens.Add(ct);
      if (stage == FailureStage) { stderr("injected stderr"); return Task.FromResult(Exit); }
      if (stage == "psql" && EmitCommit) stdout(RegionSqlArtifactGenerator.CommitMarker);
      return Task.FromResult(0);
    }
  }
}
