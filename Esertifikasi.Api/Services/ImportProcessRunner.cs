using System.Diagnostics;

namespace Esertifikasi.Api.Services;

public interface IImportProcessRunner {
  Task<int> RunAsync(ImportProcessCommand command, Action<string> stdout, Action<string> stderr, CancellationToken ct);
}

public sealed class ImportProcessRunner : IImportProcessRunner {
  public async Task<int> RunAsync(ImportProcessCommand command, Action<string> stdout, Action<string> stderr, CancellationToken ct) {
    ct.ThrowIfCancellationRequested();
    var startInfo = new ProcessStartInfo(command.FileName) {
      UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
      RedirectStandardInput = true, CreateNoWindow = true
    };
    foreach (var argument in command.Arguments) startInfo.ArgumentList.Add(argument);
    using var process = new Process { StartInfo = startInfo };
    process.Start();
    process.StandardInput.Close(); // Authentication must be noninteractive.
    using var registration = ct.Register(() => {
      try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
      catch (InvalidOperationException) { }
      catch (System.ComponentModel.Win32Exception) { }
    });
    // Drain both pipes concurrently to prevent subprocess output backpressure.
    var output = DrainAsync(process.StandardOutput, stdout);
    var errors = DrainAsync(process.StandardError, stderr);
    await process.WaitForExitAsync(CancellationToken.None);
    await Task.WhenAll(output, errors);
    ct.ThrowIfCancellationRequested();
    return process.ExitCode;
  }

  private static async Task DrainAsync(StreamReader reader, Action<string> receive) {
    while (await reader.ReadLineAsync() is { } line) receive(line);
  }
}
