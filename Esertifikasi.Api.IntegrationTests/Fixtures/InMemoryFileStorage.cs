using Esertifikasi.Api.Services.Documents;

namespace Esertifikasi.Api.IntegrationTests.Fixtures;

public sealed class InMemoryFileStorage : IFileStorage {
  private readonly Dictionary<string, byte[]> _files = new();

  public async Task<string> SaveAsync(Stream content, string extension, CancellationToken ct) {
    var key = $"test/{Guid.NewGuid():N}{extension}";
    using var buffer = new MemoryStream();
    await content.CopyToAsync(buffer, ct);
    _files[key] = buffer.ToArray();
    return key;
  }

  public Task<StoredFile?> OpenReadAsync(string storageKey, CancellationToken ct) {
    return Task.FromResult(_files.TryGetValue(storageKey, out var content)
        ? new StoredFile(storageKey, new MemoryStream(content, writable: false))
        : null);
  }

  public Task DeleteAsync(string storageKey, CancellationToken ct) {
    _files.Remove(storageKey);
    return Task.CompletedTask;
  }
}
