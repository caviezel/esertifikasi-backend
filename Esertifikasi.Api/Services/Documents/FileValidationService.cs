using System.Security.Cryptography;
using Esertifikasi.Api.Domain.Entities;

namespace Esertifikasi.Api.Services.Documents;

public sealed class ValidatedFile : IAsyncDisposable {
  public MemoryStream Content { get; init; } = null!;
  public string OriginalFileName { get; init; } = string.Empty;
  public string ContentType { get; init; } = string.Empty;
  public string Extension { get; init; } = string.Empty;
  public long Size { get; init; }
  public string Sha256Hash { get; init; } = string.Empty;

  public ValueTask DisposeAsync() => Content.DisposeAsync();
}

public sealed class FileValidationException : Exception {
  public FileValidationException(string message) : base(message) { }
}

public sealed class FileValidationService {
  public async Task<ValidatedFile> ValidateAsync(IFormFile file, DocumentType type, CancellationToken ct) {
    if (file.Length <= 0) {
      throw new FileValidationException("Berkas yang diunggah kosong.");
    }

    if (file.Length > type.MaximumFileSize) {
      throw new FileValidationException($"Ukuran berkas melebihi batas maksimum {type.MaximumFileSize} byte.");
    }

    var originalName = Path.GetFileName(file.FileName);
    if (string.IsNullOrWhiteSpace(originalName) || originalName.Length > 255) {
      throw new FileValidationException("Nama berkas asli tidak valid.");
    }

    var extension = Path.GetExtension(originalName).ToLowerInvariant();
    var allowed = type.AllowedExtensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(x => x.StartsWith('.') ? x.ToLowerInvariant() : $".{x.ToLowerInvariant()}")
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    if (!allowed.Contains(extension)) {
      throw new FileValidationException($"Ekstensi {extension} tidak diizinkan untuk jenis dokumen ini.");
    }

    var content = new MemoryStream((int)file.Length);
    try {
      await file.CopyToAsync(content, ct);
      content.Position = 0;
      var detected = Detect(content);
      if (detected.Extension != extension && !(detected.Extension == ".jpg" && extension == ".jpeg")) {
        throw new FileValidationException("Isi berkas tidak sesuai dengan ekstensinya.");
      }

      var hash = Convert.ToHexString(SHA256.HashData(content));
      content.Position = 0;

      return new ValidatedFile {
        Content = content,
        OriginalFileName = originalName,
        ContentType = detected.ContentType,
        Extension = extension,
        Size = file.Length,
        Sha256Hash = hash
      };
    }
    catch {
      await content.DisposeAsync();
      throw;
    }
  }

  private static (string Extension, string ContentType) Detect(Stream content) {
    Span<byte> header = stackalloc byte[8];
    var length = content.Read(header);
    content.Position = 0;

    if (length >= 5 && header[..5].SequenceEqual(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D })) return (".pdf", "application/pdf");
    if (length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return (".jpg", "image/jpeg");
    if (length >= 8 && header.SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return (".png", "image/png");

    throw new FileValidationException("Hanya berkas PDF, JPEG, dan PNG yang valid yang didukung.");
  }
}
