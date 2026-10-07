using System.Text.Json;
using System.ComponentModel.DataAnnotations;
namespace Esertifikasi.Api.Models;
public sealed class OperationsImportRow {
  public string Nik { get; set; } = "";
  public string LandLegalNumber { get; set; } = "";
  public string? DuplicateChoice { get; set; }
  public JsonElement Data { get; set; }
}
public sealed class OperationsImportRequest { [Required, MinLength(1), MaxLength(1000)] public List<OperationsImportRow> Rows { get; set; } = []; }
public sealed record OperationsImportPreviewRow(int RowNumber, bool PotentialDuplicate, string? DuplicateChoice, IReadOnlyList<string> Errors);
public sealed record OperationsImportPreview(bool IsValid, IReadOnlyList<OperationsImportPreviewRow> Rows);
