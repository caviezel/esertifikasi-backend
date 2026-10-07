using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Models;
using Esertifikasi.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Esertifikasi.Api.Services.OperationsAccess;
namespace Esertifikasi.Api.Controllers;
public sealed partial class FieldLogsController {
  private static readonly JsonSerializerOptions ImportJson = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
  [HttpGet("import/template")]
  public IActionResult ImportTemplate() => File(OperationsExcelService.Template(typeof(FieldLogRequest)), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "field-logs.xlsx");
  [HttpPost("import/excel-preview"), RequestSizeLimit(OperationsExcelService.MaximumFileSize + 1024 * 1024)]
  public async Task<IActionResult> ExcelPreview([FromForm] Guid associationId, [FromForm] IFormFile file, CancellationToken ct) { var input = await OperationsExcelService.Parse(file, typeof(FieldLogRequest), ct); return Ok(new { Preview = (await PrepareImport(associationId, input, ct)).Preview, InputRows = input.Rows }); }
  private async Task<(OperationsImportPreview Preview, List<FieldLog> Rows)> PrepareImport(Guid associationId, OperationsImportRequest request, CancellationToken ct) {
    Require(request.Rows.Count is > 0 and <= 1000, "Pilih 1-1000 baris."); if (!await scope.ViewAssociation(User, associationId, ct)) throw new UnauthorizedAccessException();
    var results = new List<OperationsImportPreviewRow>(); var output = new List<FieldLog>(); var seen = new HashSet<string>(); int number = 1;
    foreach (var input in request.Rows) {
      var errors = new List<string>(); bool duplicate = false;
      try {
        Require(!string.IsNullOrWhiteSpace(input.Nik) && !string.IsNullOrWhiteSpace(input.LandLegalNumber), "NIK dan No Legalitas wajib.");
        var lands = await db.Lahan.Where(x => x.Petani.Nik == input.Nik.Trim() && x.NoLegalitas == input.LandLegalNumber.Trim() && x.Petani.Poktan.AssociationId == associationId).Select(x => x.Id).ToListAsync(ct); Require(lands.Count == 1, "Identitas lahan tidak ditemukan atau ambigu.");
        FieldLogRequest r; try { r = input.Data.Deserialize<FieldLogRequest>(ImportJson) ?? throw new JsonException(); } catch (JsonException) { throw new MonitoringValidationException("Payload tidak valid."); }
        r.LahanId = lands[0]; OperationsExcelService.Validate(r); var row = await Prepare(r, null, ct);
        var key = $"{row.LahanId}/{row.Activity}/{row.ActivityDate}/{row.MaterialName}/{row.Buyer}";
        duplicate = !seen.Add(key) || await db.Set<FieldLog>().AnyAsync(x => x.LahanId == row.LahanId && x.Activity == row.Activity && x.ActivityDate == row.ActivityDate && x.MaterialName == row.MaterialName && x.Buyer == row.Buyer, ct);
        Require(input.DuplicateChoice is null or "" or "skip" or "append", "DuplicateChoice harus skip atau append.");
        Require(!duplicate || input.DuplicateChoice is "skip" or "append", "Duplikat potensial: pilih skip atau append."); if (input.DuplicateChoice != "skip") output.Add(row);
      } catch (MonitoringValidationException e) { errors.Add(e.Message); }
      results.Add(new OperationsImportPreviewRow(number++, duplicate, input.DuplicateChoice, errors));
    }
    return (new OperationsImportPreview(results.All(x => x.Errors.Count == 0), results), output);
  }
  [HttpPost("import/preview")]
  public async Task<IActionResult> ImportPreview(Guid associationId, OperationsImportRequest r, CancellationToken ct) => Ok((await PrepareImport(associationId, r, ct)).Preview);
  [HttpPost("import")]
  public async Task<IActionResult> Import(Guid associationId, OperationsImportRequest r, CancellationToken ct) {
    await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
    var prepared = await PrepareImport(associationId, r, ct); if (!prepared.Preview.IsValid) return BadRequest(prepared.Preview);
    foreach (var row in prepared.Rows) { await AssignNumber(row, ct); db.Add(row); }
    await db.SaveChangesAsync(ct); if (transaction is not null) await transaction.CommitAsync(ct); return Ok(new { Imported = prepared.Rows.Count, Ids = prepared.Rows.Select(x => x.Id) });
  }
}
public sealed partial class MonitoringController {
  [HttpGet("import/template")]
  public IActionResult ObservationTemplate(MonitoringType type, string? rowKind) => File(OperationsExcelService.Template(RowType(type, rowKind)), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"mics-{(int)type + 1}.xlsx");
  [HttpPost("submissions/{id:guid}/import/excel-preview"), RequestSizeLimit(OperationsExcelService.MaximumFileSize + 1024 * 1024)]
  public async Task<IActionResult> ObservationExcelPreview(Guid id, [FromForm] string? rowKind, [FromForm] IFormFile file, CancellationToken ct) { var h = await Editable(id, ct); var input = await OperationsExcelService.Parse(file, RowType(h.Type, rowKind), ct); return Ok(new { Preview = (await PrepareObservationImport(h, rowKind, input, ct)).Preview, InputRows = input.Rows }); }
  private async Task<(OperationsImportPreview Preview, List<object> Rows)> PrepareObservationImport(MonitoringSubmission h, string? rowKind, OperationsImportRequest request, CancellationToken ct) {
    Require(request.Rows.Count is > 0 and <= 1000, "Pilih 1-1000 baris."); var results = new List<OperationsImportPreviewRow>(); var output = new List<object>(); var seen = new HashSet<string>(); int number = 1;
    var existing = DetailRows((await DetailAsync(h, ct))!).ToList();
    foreach (var input in request.Rows) {
      bool duplicate = false; var errors = new List<string>();
      try {
        var data = JsonNode.Parse(input.Data.GetRawText()) as JsonObject ?? throw new MonitoringValidationException("Data harus objek.");
        Guid? landId = null;
        if (h.Type != MonitoringType.FirstAidKit) {
          var anonymous = h.Type == MonitoringType.MemberComplaint && data["isAnonymous"]?.GetValue<bool>() == true;
          if (!anonymous) {
            Require(!string.IsNullOrWhiteSpace(input.Nik), "NIK wajib.");
            if (h.Type != MonitoringType.MemberComplaint || !string.IsNullOrWhiteSpace(input.LandLegalNumber)) {
              Require(!string.IsNullOrWhiteSpace(input.LandLegalNumber), "No Legalitas wajib.");
              var lands = await _db.Lahan.Where(x => x.Petani.Nik == input.Nik.Trim() && x.NoLegalitas == input.LandLegalNumber.Trim() && x.Petani.PoktanId == h.PoktanId).Select(x => new { x.Id, x.PetaniId }).ToListAsync(ct); Require(lands.Count == 1, "Identitas lahan tidak ditemukan atau ambigu."); landId = lands[0].Id; data["lahanId"] = landId; data["petaniId"] = lands[0].PetaniId;
            } else { var farmers = await _db.Petani.Where(x => x.Nik == input.Nik.Trim() && x.PoktanId == h.PoktanId).Select(x => x.Id).ToListAsync(ct); Require(farmers.Count == 1, "NIK tidak ditemukan atau ambigu."); data["petaniId"] = farmers[0]; data["lahanId"] = null; }
          } else { data["petaniId"] = null; data["lahanId"] = null; }
        }
        var rows = await PrepareObservation(h, new ObservationRequest { RowKind = rowKind, Rows = [JsonSerializer.SerializeToElement(data)] }, Guid.NewGuid(), ct);
        var candidate = rows[0]; var date = candidate is FarmerLandMonitoringRow f ? f.ObservedOn : candidate is MemberComplaint c ? c.ReceivedOn : ((FirstAidKitInspection)candidate).ObservedOn;
        var key = $"{landId}/{input.Nik}/{date}/{rowKind}";
        duplicate = !seen.Add(key) || existing.Any(x => x.GetType() == candidate.GetType() && (x is FarmerLandMonitoringRow prior && prior.LahanId == landId && prior.ObservedOn == date || x is MemberComplaint complaint && complaint.LahanId == landId && complaint.ReceivedOn == date || x is FirstAidKitInspection kit && candidate is FirstAidKitInspection next && kit.LocationId == next.LocationId && kit.ObservedOn == date));
        Require(input.DuplicateChoice is null or "" or "skip" or "append", "DuplicateChoice harus skip atau append."); Require(!duplicate || input.DuplicateChoice is "skip" or "append", "Duplikat potensial: pilih skip atau append."); if (input.DuplicateChoice != "skip") output.AddRange(rows);
      } catch (MonitoringValidationException e) { errors.Add(e.Message); }
      results.Add(new OperationsImportPreviewRow(number++, duplicate, input.DuplicateChoice, errors));
    }
    return (new OperationsImportPreview(results.All(x => x.Errors.Count == 0), results), output);
  }
  [HttpPost("submissions/{id:guid}/import/preview")]
  public async Task<IActionResult> ObservationImportPreview(Guid id, string? rowKind, OperationsImportRequest r, CancellationToken ct) => Ok((await PrepareObservationImport(await Editable(id, ct), rowKind, r, ct)).Preview);
  [HttpPost("submissions/{id:guid}/import")]
  public async Task<IActionResult> ObservationImport(Guid id, string? rowKind, OperationsImportRequest r, CancellationToken ct) {
    await using var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
    var h = await Editable(id, ct); var prepared = await PrepareObservationImport(h, rowKind, r, ct); if (!prepared.Preview.IsValid) return BadRequest(prepared.Preview);
    if (prepared.Rows.Count > 0) await ClearZeroAsync(h, ct); foreach (var row in prepared.Rows) _db.Add(row); await _db.SaveChangesAsync(ct); if (transaction is not null) await transaction.CommitAsync(ct); return Ok(new { Imported = prepared.Rows.Count });
  }
}
