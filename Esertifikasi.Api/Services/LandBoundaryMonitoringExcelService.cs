using System.IO.Compression;
using System.Xml.Linq;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models;

namespace Esertifikasi.Api.Services;

public sealed class LandBoundaryMonitoringExcelService {
  public const int MaximumFileSize = 10 * 1024 * 1024;
  private static readonly string[] Headers = new[] { "NIK", "NoLegalitas", "TanggalMonitoring", "TanggalPemasangan", "JumlahPatok", "Kondisi", "Keterangan", "TindakLanjut" };

  public byte[] CreateTemplate() =>
      AuditFindingExcelService.CreateXlsx(new List<string[]> { Headers, new[] { "1234567890123456", "12.3.4567.89.0", "2026-01-15", "2022-05-01", "4", "Good", "", "" },
        Array.Empty<string>(), new[] { "Kondisi: Good/Baik, Damaged/Rusak, Missing/Hilang. Format tanggal YYYY-MM-DD." } }, "MICS 1");

  public async Task<IReadOnlyList<(int RowNumber, BulkLandBoundaryRow? Data, List<string> Errors)>> ParseAsync(IFormFile file, CancellationToken ct) {
    if (file.Length <= 0 || file.Length > MaximumFileSize || !Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
      throw new MonitoringValidationException("File import harus berupa XLSX dan berukuran maksimal 10 MB.");
    await using var input = file.OpenReadStream(); using var archive = new ZipArchive(input, ZipArchiveMode.Read, false);
    var sheet = archive.GetEntry("xl/worksheets/sheet1.xml") ?? throw new MonitoringValidationException("Worksheet pertama tidak ditemukan.");
    var shared = AuditFindingExcelService.ReadSharedStrings(archive); XDocument document; await using (var stream = sheet.Open()) document = await XDocument.LoadAsync(stream, LoadOptions.None, ct);
    XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    var rawRows = document.Descendants(ns + "row").Select(r => ((int?)r.Attribute("r") ?? 0, Values: AuditFindingExcelService.ReadCells(r, ns, shared))).ToList();
    if (rawRows.Count == 0 || !Headers.SequenceEqual(AuditFindingExcelService.Pad(rawRows[0].Values, Headers.Length).Take(Headers.Length), StringComparer.OrdinalIgnoreCase))
      throw new MonitoringValidationException("Header template tidak valid. Unduh dan gunakan template terbaru.");
    var result = new List<(int, BulkLandBoundaryRow?, List<string>)>();
    foreach (var raw in rawRows.Skip(1)) {
      var v = AuditFindingExcelService.Pad(raw.Values, Headers.Length); if (v.All(string.IsNullOrWhiteSpace)) break; var errors = new List<string>();
      if (string.IsNullOrWhiteSpace(v[0])) errors.Add("NIK wajib diisi.");
      if (!DateOnly.TryParse(v[2], out var observedOn)) errors.Add("TanggalMonitoring wajib berformat YYYY-MM-DD.");
      DateOnly? installedOn = null; if (!string.IsNullOrWhiteSpace(v[3])) { if (DateOnly.TryParse(v[3], out var parsed)) installedOn = parsed; else errors.Add("TanggalPemasangan harus berformat YYYY-MM-DD."); }
      if (!int.TryParse(v[4], out var count) || count < 0) errors.Add("JumlahPatok harus bilangan bulat >= 0.");
      var condition = ParseCondition(v[5]); if (condition is null) errors.Add("Kondisi harus Good/Baik, Damaged/Rusak, atau Missing/Hilang.");
      var data = errors.Count > 0 ? null : new BulkLandBoundaryRow { Nik = v[0].Trim(), LandLegalNumber = AuditFindingExcelService.Empty(v[1]), ObservedOn = observedOn, InstalledOn = installedOn, MarkerCount = count, Condition = condition!.Value, Notes = AuditFindingExcelService.Empty(v[6]), FollowUp = AuditFindingExcelService.Empty(v[7]) };
      result.Add((raw.Item1, data, errors));
    }
    if (result.Count == 0) throw new MonitoringValidationException("File tidak memiliki baris data.");
    return result;
  }

  private static ItemCondition? ParseCondition(string value) {
    var text = value.Trim();
    if (text.Equals("Baik", StringComparison.OrdinalIgnoreCase)) return ItemCondition.Good;
    if (text.Equals("Rusak", StringComparison.OrdinalIgnoreCase)) return ItemCondition.Damaged;
    if (text.Equals("Hilang", StringComparison.OrdinalIgnoreCase)) return ItemCondition.Missing;
    return Enum.TryParse<ItemCondition>(text, true, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;
  }
}
