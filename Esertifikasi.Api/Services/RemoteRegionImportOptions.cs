using System.Text.RegularExpressions;

namespace Esertifikasi.Api.Services;

public sealed class RemoteRegionImportOptions {
  public const string SectionName = "RemoteImport";
  public string Host { get; set; } = "";
  public int Port { get; set; } = 22;
  public string User { get; set; } = "";
  public string IdentityFile { get; set; } = "";
  public string RemoteTempDirectory { get; set; } = "/tmp";
  public string PsqlPath { get; set; } = "/usr/bin/psql";
  public string DatabaseName { get; set; } = "";
  public string DatabaseUser { get; set; } = "";

  public void Validate() {
    // Reject options, libpq connection strings/URIs, remote path metacharacters,
    // control characters, and ambiguous scp destinations before starting processes.
    Require(Host, "Host", "^[A-Za-z0-9][A-Za-z0-9.-]*$");
    Require(User, "User", "^[A-Za-z_][A-Za-z0-9_-]*$");
    Require(DatabaseName, "DatabaseName", "^[A-Za-z_][A-Za-z0-9_-]*$");
    Require(DatabaseUser, "DatabaseUser", "^[A-Za-z_][A-Za-z0-9_-]*$");
    if (Port is < 1 or > 65535) throw new RegionImportException("RemoteImport:Port must be between 1 and 65535.");
    ValidatePath(RemoteTempDirectory, "RemoteTempDirectory");
    ValidatePath(PsqlPath, "PsqlPath");
    if (!string.IsNullOrEmpty(IdentityFile) && !File.Exists(IdentityFile))
      throw new RegionImportException("RemoteImport:IdentityFile does not exist; use an absolute path (tilde is not expanded).");
  }

  private static void ValidatePath(string value, string name) {
    Require(value, name, "^/[A-Za-z0-9_./-]+$");
    if (value.Split('/').Any(x => x is "." or ".."))
      throw new RegionImportException($"RemoteImport:{name} must not contain '.' or '..' path components.");
  }
  private static void Require(string value, string name, string pattern) {
    if (string.IsNullOrEmpty(value) || !Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant) || value.Any(char.IsControl))
      throw new RegionImportException($"RemoteImport:{name} is required and must use a plain safe hostname, identifier, or absolute path.");
  }
}

public sealed record ImportProcessCommand(string FileName, IReadOnlyList<string> Arguments);

public static class RemoteRegionImportCommands {
  public static ImportProcessCommand Ssh(RemoteRegionImportOptions options, params string[] remoteArguments) {
    var arguments = SshOptions(options, "-p");
    arguments.Add("--");
    arguments.Add($"{options.User}@{options.Host}");
    // ssh always hands its remote command to the server's login shell. Local
    // ArgumentList alone does not protect that boundary: quote EVERY remote token.
    arguments.Add(string.Join(" ", remoteArguments.Select(QuotePosix)));
    return new ImportProcessCommand("ssh", arguments);
  }

  public static ImportProcessCommand Upload(RemoteRegionImportOptions options, string localPath, string remotePath) {
    var arguments = SshOptions(options, "-P");
    arguments.Add("--");
    arguments.Add(Path.GetFullPath(localPath));
    arguments.Add($"{options.User}@{options.Host}:{remotePath}");
    return new ImportProcessCommand("scp", arguments);
  }

  public static ImportProcessCommand Execute(RemoteRegionImportOptions options, string remotePath) =>
      Ssh(options, options.PsqlPath, "-X", "-w", "-v", "ON_ERROR_STOP=1",
          "-h", "127.0.0.1", "-p", "5432", "-d", options.DatabaseName, "-U", options.DatabaseUser, "-f", remotePath);

  public static string QuotePosix(string value) => "'" + value.Replace("'", "'\"'\"'") + "'";

  private static List<string> SshOptions(RemoteRegionImportOptions options, string portFlag) {
    var arguments = new List<string> { portFlag, options.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
      "-o", "BatchMode=yes", "-o", "StrictHostKeyChecking=yes", "-o", "ConnectTimeout=15", "-o", "ConnectionAttempts=1" };
    if (!string.IsNullOrEmpty(options.IdentityFile)) { arguments.Add("-i"); arguments.Add(Path.GetFullPath(options.IdentityFile)); }
    return arguments;
  }
}
