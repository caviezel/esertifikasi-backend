using Esertifikasi.Api.Services.Documents;
using Esertifikasi.Api.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api.Infrastructure;

public sealed class GlobalExceptionHandler : IExceptionHandler {
  private readonly ILogger<GlobalExceptionHandler> _logger;

  public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) {
    _logger = logger;
  }

  public async ValueTask<bool> TryHandleAsync(
      HttpContext context,
      Exception exception,
    CancellationToken ct) {
    if (exception is DbUpdateConcurrencyException concurrencyException) {
      foreach (var entry in concurrencyException.Entries) {
        _logger.LogWarning("Concurrency conflict while saving {EntityType} in state {EntityState}.",
            entry.Metadata.ClrType.Name, entry.State);
      }
    }
    var (status, title, detail) = Map(exception);
    if (status >= StatusCodes.Status500InternalServerError) {
      _logger.LogError(exception, "Permintaan gagal. TraceId: {TraceId}", context.TraceIdentifier);
    }
    else {
      _logger.LogWarning(exception, "Permintaan ditolak. TraceId: {TraceId}", context.TraceIdentifier);
    }

    var problem = new ProblemDetails {
      Type = $"https://httpstatuses.com/{status}",
      Title = title,
      Status = status,
      Detail = detail,
      Instance = context.Request.Path
    };
    problem.Extensions["traceId"] = context.TraceIdentifier;
    if (exception is CertificationWorkflowException workflowException)
      problem.Extensions["code"] = workflowException.Code;

    context.Response.StatusCode = status;
    await context.Response.WriteAsJsonAsync(problem, ct);
    return true;
  }

  private static (int Status, string Title, string Detail) Map(Exception exception) {
    return exception switch {
      CertificationWorkflowException => (
          StatusCodes.Status409Conflict,
          "Transisi workflow tidak valid",
          exception.Message),
      RegionImportException => (
          StatusCodes.Status400BadRequest,
          "Data wilayah tidak valid",
          exception.Message),
      MonitoringValidationException => (
          StatusCodes.Status400BadRequest,
          "Data monitoring tidak valid",
          exception.Message),
      AuditFindingImportException => (
          StatusCodes.Status400BadRequest,
          "File temuan audit tidak valid",
          exception.Message),
      DocumentConflictException => (
          StatusCodes.Status409Conflict,
          "Konflik data",
          exception.Message),
      FileValidationException or DocumentOperationException => (
          StatusCodes.Status400BadRequest,
          "Berkas tidak valid",
          exception.Message),
      DbUpdateConcurrencyException => (
          StatusCodes.Status409Conflict,
          "Data telah berubah",
          "Data telah diubah oleh proses lain. Muat ulang data lalu coba kembali."),
      DbUpdateException => (
          StatusCodes.Status409Conflict,
          "Konflik data",
          "Data tidak dapat disimpan karena melanggar aturan atau memiliki referensi yang masih digunakan."),
      KeyNotFoundException => (
          StatusCodes.Status404NotFound,
          "Data tidak ditemukan",
          exception.Message),
      UnauthorizedAccessException => (
          StatusCodes.Status403Forbidden,
          "Akses ditolak",
          "Anda tidak memiliki izin untuk melakukan tindakan ini."),
      BadHttpRequestException => (
          StatusCodes.Status400BadRequest,
          "Permintaan tidak valid",
          "Format atau isi permintaan tidak valid."),
      _ => (
          StatusCodes.Status500InternalServerError,
          "Terjadi kesalahan pada server",
          "Permintaan tidak dapat diproses. Gunakan traceId saat menghubungi administrator.")
    };
  }
}
