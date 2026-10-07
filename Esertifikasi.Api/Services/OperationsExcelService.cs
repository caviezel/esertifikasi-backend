using System.IO.Compression;
using System.Xml.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.ComponentModel.DataAnnotations;
using Esertifikasi.Api.Models;
namespace Esertifikasi.Api.Services;
public static class OperationsExcelService {
  public const int MaximumFileSize = 10 * 1024 * 1024;
  public static byte[] Template(Type dataType) => AuditFindingExcelService.CreateXlsx([Headers(dataType)], "Input");
  private static string[] Headers(Type t) => new[] { "Nik", "LandLegalNumber", "DuplicateChoice" }.Concat(t.GetProperties().Where(p => p.Name is not ("LahanId" or "PetaniId" or "Id" or "MonitoringSubmissionId" or "ObservationId" or "Petani" or "Lahan" or "FarmerNameSnapshot" or "NikSnapshot" or "LandLegalNumberSnapshot" or "LandAreaSnapshot" or "IsDeleted")).Select(p => p.Name)).ToArray();
  public static async Task<OperationsImportRequest> Parse(IFormFile file, Type dataType, CancellationToken ct) {
    OperationsAccess.Require(file.Length > 0 && file.Length <= MaximumFileSize && Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase), "File harus XLSX, maksimum 10 MB.");
    await using var input = file.OpenReadStream();
    try {
      using var zip = new ZipArchive(input, ZipArchiveMode.Read);
      OperationsAccess.Require(zip.Entries.Sum(x => x.Length) <= 40 * 1024 * 1024, "File hasil ekstraksi terlalu besar.");
      var sheet = zip.GetEntry("xl/worksheets/sheet1.xml") ?? throw new MonitoringValidationException("Worksheet pertama tidak ditemukan.");
      var shared = AuditFindingExcelService.ReadSharedStrings(zip); await using var stream = sheet.Open(); var doc = await XDocument.LoadAsync(stream, LoadOptions.None, ct); XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
      var records = doc.Descendants(ns + "row").ToList(); OperationsAccess.Require(records.Count > 1 && records.Count <= 1001, "File harus memuat 1-1000 baris.");
      Dictionary<int, string> Cells(XElement row) => row.Elements(ns + "c").ToDictionary(c => Column((string?)c.Attribute("r") ?? "A1"), c => { var v = c.Element(ns + "v")?.Value ?? ""; return (string?)c.Attribute("t") == "s" ? shared[int.Parse(v)] : (string?)c.Attribute("t") == "inlineStr" ? string.Concat(c.Descendants(ns + "t").Select(t => t.Value)) : v; });
      var headings = Cells(records[0]); OperationsAccess.Require(headings.Values.Contains("Nik") && headings.Values.Contains("LandLegalNumber"), "Header Nik dan LandLegalNumber wajib.");
      var result = new OperationsImportRequest();
      foreach (var record in records.Skip(1)) {
        var cells = Cells(record); if (cells.Values.All(string.IsNullOrWhiteSpace)) continue;
        string Value(string name) => headings.Where(x => x.Value == name).Select(x => cells.GetValueOrDefault(x.Key, "")).FirstOrDefault() ?? "";
        var data = new JsonObject(); foreach (var heading in headings.Where(x => x.Value is not ("Nik" or "LandLegalNumber" or "DuplicateChoice"))) {
          var raw = cells.GetValueOrDefault(heading.Key, ""); if (string.IsNullOrWhiteSpace(raw)) continue;
          var prop = dataType.GetProperty(heading.Value); OperationsAccess.Require(prop is not null, $"Kolom tidak dikenal: {heading.Value}."); var type = Nullable.GetUnderlyingType(prop!.PropertyType) ?? prop.PropertyType;
          try { data[JsonNamingPolicy.CamelCase.ConvertName(prop.Name)] = type == typeof(string) || type.IsEnum || type == typeof(DateOnly) || type == typeof(Guid) ? JsonValue.Create(raw) : type == typeof(bool) && raw is "1" or "0" ? JsonValue.Create(raw == "1") : JsonNode.Parse(raw); } catch (JsonException) { throw new MonitoringValidationException($"Nilai kolom {heading.Value} baris {record.Attribute("r")?.Value} tidak valid."); }
        }
        result.Rows.Add(new OperationsImportRow { Nik = Value("Nik"), LandLegalNumber = Value("LandLegalNumber"), DuplicateChoice = Value("DuplicateChoice"), Data = JsonSerializer.SerializeToElement(data) });
      }
      return result;
    } catch (Exception e) when (e is InvalidDataException or System.Xml.XmlException or ArgumentException or IndexOutOfRangeException or FormatException) { throw new MonitoringValidationException("File XLSX rusak atau struktur tidak valid."); }
  }
  private static int Column(string address) { var result = 0; foreach (var c in address.TakeWhile(char.IsLetter)) result = result * 26 + char.ToUpperInvariant(c) - 'A' + 1; return result - 1; }
  public static void Validate(object request) { var errors = new List<ValidationResult>(); OperationsAccess.Require(Validator.TryValidateObject(request, new ValidationContext(request), errors, true), string.Join(" ", errors.Select(x => x.ErrorMessage))); }
}
