using System.Diagnostics;
using System.Text;

namespace Esertifikasi.Api.Services;

public sealed class RemoteRegionImporter(IImportProcessRunner processes, RegionSqlArtifactGenerator generator) {
  public async Task RunAsync(string manifestPath, bool confirmed, RemoteRegionImportOptions options,
      Action<string> output, CancellationToken ct) {
    var clock = Stopwatch.StartNew();
    output("Validating dataset...");
    var dataset = await RegionDatasetImporter.ReadValidatedDatasetAsync(manifestPath, ct);
    output($"Remote host: {(string.IsNullOrWhiteSpace(options.Host) ? "<not configured>" : SafeDisplay(options.Host))}");
    output($"Database name: {(string.IsNullOrWhiteSpace(options.DatabaseName) ? "<not configured>" : SafeDisplay(options.DatabaseName))}");
    output($"Provinces: {dataset.Provinces.Count:N0}\nRegencies: {dataset.Regencies.Count:N0}\nDistricts: {dataset.Districts.Count:N0}\nVillages: {dataset.Villages.Count:N0}");
    if (!confirmed) {
      output("Validation complete. Add --confirm to generate, upload, and execute. No remote commands were run.");
      return;
    }
    options.Validate();
    var directory = options.RemoteTempDirectory.TrimEnd('/') + $"/region-import-{Guid.NewGuid():N}";
    var remotePath = directory + "/artifact.sql";
    RegionSqlArtifact? artifact = null;
    var directoryAttempted = false;
    var directoryCreated = false;
    var executing = false;
    async Task Run(string stage, ImportProcessCommand command, CancellationToken token, Action<string>? onOutput = null) {
      var stderr = new StringBuilder();
      var exit = await processes.RunAsync(command, line => { output("Remote: " + line); onOutput?.Invoke(line); }, line => {
        output("Remote: " + line);
        if (stderr.Length < 16_384) stderr.AppendLine(line[..Math.Min(line.Length, 16_384 - stderr.Length)]);
      }, token);
      if (exit != 0) throw new RegionImportException($"{stage} failed (exit {exit}). {stderr.ToString().Trim()}");
    }
    try {
      output("Generating SQL...");
      artifact = await generator.GenerateAsync(dataset, ct);
      output($"Artifact: {artifact.Path}\nSize: {artifact.Bytes / 1048576d:F2} MB");
      output("Uploading to EC2...");
      directoryAttempted = true;
      await Run("Remote staging directory creation", RemoteRegionImportCommands.Ssh(options, "/bin/mkdir", "-m", "700", "--", directory), ct);
      directoryCreated = true;
      await Run("Upload", RemoteRegionImportCommands.Upload(options, artifact.Path, remotePath), ct);
      output("Upload complete.");
      output("Executing import on EC2...");
      executing = true;
      var committed = false;
      await Run("Remote psql", RemoteRegionImportCommands.Execute(options, remotePath), ct,
          line => { if (line == RegionSqlArtifactGenerator.CommitMarker) committed = true; });
      if (!committed) throw new RegionImportException("Remote psql returned without the transaction commit marker; success cannot be confirmed.");
      executing = false;
    }
    catch (Exception) when (executing) {
      output("Remote execution failed or was interrupted. Its outcome may be unknown after an SSH disconnect; check RegionDatasetImport on EC2 before rerunning. No automatic retry was attempted.");
      throw;
    }
    finally {
      output("Cleaning up...");
      if (directoryCreated) {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try {
          // Delete only our generated filename and directory, never recurse through
          // a user-configured directory. Cleanup does not retry the mutation.
          await Run("Remote artifact cleanup", RemoteRegionImportCommands.Ssh(options, "/bin/rm", "-f", "--", remotePath), cleanup.Token);
          await Run("Remote directory cleanup", RemoteRegionImportCommands.Ssh(options, "/bin/rmdir", "--", directory), cleanup.Token);
          output("Remote artifact deleted.");
        }
        catch (Exception error) { output($"Cleanup warning: {error.Message}\nInspect leftover remote directory: {directory}"); }
      }
      else if (directoryAttempted) {
        output($"Remote directory creation was not confirmed; inspect {directory} if SSH disconnected during creation.");
      }
      if (artifact is not null) {
        try { File.Delete(artifact.Path); output("Local artifact deleted."); }
        catch (Exception error) { output($"Cleanup warning: {error.Message}\nInspect leftover local artifact: {artifact.Path}"); }
      }
    }
    output($"Region import completed successfully.\nElapsed: {clock.Elapsed.TotalSeconds:F1} seconds");
  }

  private static string SafeDisplay(string value) => value.All(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_') ? value : "<invalid configuration>";
}
