namespace Esertifikasi.Api.Services.Documents;

public sealed class FileStorageOptions {
  public const string SectionName = "FileStorage";
  public string LocalPath { get; set; } = "storage/documents";
}

public sealed record StoredFile(string StorageKey, Stream Content);

public interface IFileStorage {
  Task<string> SaveAsync(Stream content, string extension, CancellationToken ct);
  Task<StoredFile?> OpenReadAsync(string storageKey, CancellationToken ct);
  Task DeleteAsync(string storageKey, CancellationToken ct);
}

public sealed class LocalFileStorage : IFileStorage {
  private readonly string _root;

  public LocalFileStorage(IConfiguration configuration, IWebHostEnvironment environment) {
    var configuredPath = configuration[$"{FileStorageOptions.SectionName}:LocalPath"] ?? "storage/documents";
    _root = Path.GetFullPath(Path.IsPathRooted(configuredPath)
        ? configuredPath
        : Path.Combine(environment.ContentRootPath, configuredPath));
  }

  public async Task<string> SaveAsync(Stream content, string extension, CancellationToken ct) {
    var now = DateTimeOffset.UtcNow;
    var relativeDirectory = Path.Combine(now.Year.ToString("0000"), now.Month.ToString("00"));
    var fileName = $"{Guid.NewGuid():N}{extension}";
    var storageKey = Path.Combine(relativeDirectory, fileName).Replace(Path.DirectorySeparatorChar, '/');
    var path = Resolve(storageKey);

    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    await using var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
    await content.CopyToAsync(destination, ct);

    return storageKey;
  }

  public Task<StoredFile?> OpenReadAsync(string storageKey, CancellationToken ct) {
    var path = Resolve(storageKey);
    if (!File.Exists(path)) {
      return Task.FromResult<StoredFile?>(null);
    }

    Stream content = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
    return Task.FromResult<StoredFile?>(new StoredFile(storageKey, content));
  }

  public Task DeleteAsync(string storageKey, CancellationToken ct) {
    var path = Resolve(storageKey);
    if (File.Exists(path)) {
      File.Delete(path);
    }

    return Task.CompletedTask;
  }

  private string Resolve(string storageKey) {
    var normalizedKey = storageKey.Replace('/', Path.DirectorySeparatorChar);
    var path = Path.GetFullPath(Path.Combine(_root, normalizedKey));
    var requiredPrefix = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
    if (!path.StartsWith(requiredPrefix, StringComparison.Ordinal)) {
      throw new InvalidOperationException("Kunci penyimpanan tidak valid.");
    }

    return path;
  }
}
