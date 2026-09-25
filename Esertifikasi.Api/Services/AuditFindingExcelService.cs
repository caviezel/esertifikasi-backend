using System.IO.Compression;
using System.Security;
using System.Text;
using System.Xml.Linq;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models;

namespace Esertifikasi.Api.Services;

public sealed class AuditFindingImportException : Exception {
  public AuditFindingImportException(string message) : base(message) { }
}

public sealed class AuditFindingExcelService {
  public const int MaximumFileSize = 10 * 1024 * 1024;
  private static readonly string[] Headers = new[] { "Code", "PoktanId", "Severity", "Description", "DueDate", "PenyebabAnalisis", "Corrections", "CorrectiveAction" };

  public byte[] CreateTemplate(IEnumerable<(Guid Id, string Name)> poktan) {
    var rows = new List<string[]> { Headers, new[] { "F-001", poktan.FirstOrDefault().Id.ToString(), "Minor", "Deskripsi temuan", "2026-12-31", "", "", "" } };
    rows.Add(Array.Empty<string>()); rows.Add(new[] { "Daftar Poktan" }); rows.AddRange(poktan.Select(x => new[] { x.Id.ToString(), x.Name }));
    return CreateXlsx(rows);
  }

  public async Task<IReadOnlyList<(int RowNumber, BulkAuditFindingRow? Data, List<string> Errors)>> ParseAsync(IFormFile file, CancellationToken ct) {
    if (file.Length <= 0 || file.Length > MaximumFileSize || !Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
      throw new AuditFindingImportException("File import harus berupa XLSX dan berukuran maksimal 10 MB.");
    await using var input = file.OpenReadStream(); using var archive = new ZipArchive(input, ZipArchiveMode.Read, false);
    var sheet = archive.GetEntry("xl/worksheets/sheet1.xml") ?? throw new AuditFindingImportException("Worksheet pertama tidak ditemukan.");
    var shared = ReadSharedStrings(archive); XDocument document; await using (var stream = sheet.Open()) document = await XDocument.LoadAsync(stream, LoadOptions.None, ct);
    XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    var rawRows = document.Descendants(ns + "row").Select(r => ((int?)r.Attribute("r") ?? 0, Values: ReadCells(r, ns, shared))).ToList();
    if (rawRows.Count == 0 || !Headers.SequenceEqual(Pad(rawRows[0].Values, Headers.Length).Take(Headers.Length), StringComparer.OrdinalIgnoreCase))
      throw new AuditFindingImportException("Header template audit tidak valid. Unduh dan gunakan template terbaru.");
    var result = new List<(int, BulkAuditFindingRow?, List<string>)>();
    foreach (var raw in rawRows.Skip(1)) {
      var v = Pad(raw.Values, Headers.Length); if (v.All(string.IsNullOrWhiteSpace)) break; var errors = new List<string>();
      if (string.IsNullOrWhiteSpace(v[0])) errors.Add("Code wajib diisi.");
      if (!Guid.TryParse(v[1], out var poktanId)) errors.Add("PoktanId tidak valid.");
      if (!Enum.TryParse<FindingSeverity>(v[2], true, out var severity) || !Enum.IsDefined(severity)) errors.Add("Severity harus Minor atau Major.");
      if (string.IsNullOrWhiteSpace(v[3])) errors.Add("Description wajib diisi.");
      DateOnly? dueDate = null; if (!string.IsNullOrWhiteSpace(v[4])) { if (DateOnly.TryParse(v[4], out var parsed)) dueDate = parsed; else errors.Add("DueDate harus berformat YYYY-MM-DD."); }
      var data = errors.Count > 0 ? null : new BulkAuditFindingRow { Code = v[0].Trim(), PoktanId = poktanId, Severity = severity, Description = v[3].Trim(), DueDate = dueDate, PenyebabAnalisis = Empty(v[5]), Corrections = Empty(v[6]), CorrectiveAction = Empty(v[7]) };
      result.Add((raw.Item1, data, errors));
    }
    if (result.Count == 0) throw new AuditFindingImportException("File tidak memiliki baris temuan.");
    return result;
  }

  internal static string? Empty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
  internal static List<string> Pad(List<string> values, int count) { while (values.Count < count) values.Add(""); return values; }
  internal static List<string> ReadSharedStrings(ZipArchive archive) { var entry = archive.GetEntry("xl/sharedStrings.xml"); if (entry is null) return new List<string>(); using var stream = entry.Open(); var doc = XDocument.Load(stream); XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"; return doc.Descendants(ns + "si").Select(x => string.Concat(x.Descendants(ns + "t").Select(t => t.Value))).ToList(); }
  internal static List<string> ReadCells(XElement row, XNamespace ns, List<string> shared) {
    var result = new List<string>(); foreach (var cell in row.Elements(ns + "c")) { var reference = (string?)cell.Attribute("r") ?? "A1"; var index = ColumnIndex(reference); while (result.Count <= index) result.Add(""); var type = (string?)cell.Attribute("t"); var value = type == "inlineStr" ? string.Concat(cell.Descendants(ns + "t").Select(x => x.Value)) : cell.Element(ns + "v")?.Value ?? ""; if (type == "s" && int.TryParse(value, out var sharedIndex) && sharedIndex < shared.Count) value = shared[sharedIndex]; result[index] = value; } return result;
  }
  internal static int ColumnIndex(string reference) { var index = 0; foreach (var c in reference.TakeWhile(char.IsLetter)) index = index * 26 + char.ToUpperInvariant(c) - 'A' + 1; return index - 1; }
  internal static byte[] CreateXlsx(IEnumerable<string[]> rows, string sheetName = "Temuan") {
    using var memory = new MemoryStream(); using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true)) {
      Write(zip, "[Content_Types].xml", "<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
      Write(zip, "_rels/.rels", "<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
      Write(zip, "xl/workbook.xml", $"<?xml version=\"1.0\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"{SecurityElement.Escape(sheetName)}\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
      Write(zip, "xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
      var xml = new StringBuilder("<?xml version=\"1.0\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>"); var rowNumber = 1; foreach (var row in rows) { xml.Append($"<row r=\"{rowNumber}\">"); for (var i = 0; i < row.Length; i++) xml.Append($"<c r=\"{ColumnName(i)}{rowNumber}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(row[i])}</t></is></c>"); xml.Append("</row>"); rowNumber++; } xml.Append("</sheetData></worksheet>"); Write(zip, "xl/worksheets/sheet1.xml", xml.ToString());
    } return memory.ToArray();
  }
  private static string ColumnName(int index) { var name = ""; for (index++; index > 0; index = (index - 1) / 26) name = (char)('A' + (index - 1) % 26) + name; return name; }
  private static void Write(ZipArchive archive, string path, string text) { var entry = archive.CreateEntry(path); using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)); writer.Write(text); }
}
