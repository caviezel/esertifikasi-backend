using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.IntegrationTests.Fixtures;
namespace Esertifikasi.Api.IntegrationTests.Operations;
public sealed class OperationsFrontendContractTests(EsertifikasiWebApplicationFactory factory) : IClassFixture<EsertifikasiWebApplicationFactory>, IAsyncLifetime {
  public Task InitializeAsync() => factory.SeedAsync(); public Task DisposeAsync() => Task.CompletedTask;
  [Fact]
  public async Task AllThirteenMicsSupportDocumentedObservationWriteAndFinalize() {
    using var client = factory.CreateClient(); client.AuthenticateAs(TestIds.AssociationAdminA, AppRoles.AssociationAdmin);
    var locationResponse = await client.PostAsJsonAsync($"/api/operations/catalog/first-aid-locations?associationId={TestIds.AssociationA}", new { name = "Contract test office" }); Assert.Equal(HttpStatusCode.OK, locationResponse.StatusCode); var location = (await locationResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    var subject = new JsonObject { ["petaniId"] = TestIds.PetaniA, ["lahanId"] = TestIds.LahanA, ["observedOn"] = "2045-04-01" };
    JsonObject Row(params (string Name, JsonNode? Value)[] values) { var r = (JsonObject)subject.DeepClone(); foreach (var (name,value) in values) r[name] = value; return r; }
    var cases = new (string Route, string Collection, bool Semi, JsonObject Row)[] {
      ("land-boundaries", "inspections", false, Row(("installationYear", 2020), ("markerCount", 4), ("condition", "Good"))),
      ("turnera", "inspections", false, Row(("condition", "Good"))),
      ("chemical-buffers", "inspections", false, Row(("hasRiverBoundaryMarker", true))),
      ("woody-plants", "inspections", false, Row(("observations", new JsonArray(new JsonObject { ["treeName"] = "Matoa", ["quantity"] = 3 })))),
      ("first-aid-kits", "inspections", true, new JsonObject { ["locationId"] = location, ["observedOn"] = "2045-04-01", ["items"] = new JsonArray(new JsonObject { ["itemName"] = "Kasa", ["condition"] = "Good" }) }),
      ("ppe", "inspections", false, Row(("activity", "Harvesting"), ("items", new JsonArray(new JsonObject { ["itemName"] = "Helm", ["isUsed"] = true })))),
      ("high-conservation-values", "speciesObservations", true, Row(("speciesKind", "Animal"), ("speciesName", "Monyet"))),
      ("fires", "incidents", false, Row(("incidentDate", "2045-03-01"), ("severity", "Light"), ("chronology", "Small fire"))),
      ("workplace-accidents", "incidents", false, Row(("incidentDate", "2045-03-02"), ("category", "Minor"), ("caseCount", 2), ("chronology", "Two people injured"))),
      ("weeds", "inspections", false, Row(("weedType", "Pakis"))),
      ("plant-diseases", "inspections", false, Row(("diseaseType", "Ganoderma"))),
      ("pests", "inspections", false, Row(("pestType", "Ulat"), ("severity", "Light"), ("observedDensity", 3), ("densityUnit", "ulat/pokok"))),
      ("member-complaints", "complaints", false, new JsonObject { ["petaniId"] = TestIds.PetaniA, ["receivedOn"] = "2045-04-01", ["complaintType"] = "Bibit", ["description"] = "Need seeds" })
    };
    foreach (var item in cases) {
      var request = new JsonObject { ["poktanId"] = TestIds.PoktanA1, ["periodStart"] = "2045-01-01", ["periodEnd"] = item.Semi ? "2045-06-30" : "2045-12-31", [item.Collection] = new JsonArray() };
      var created = await client.PostAsJsonAsync("/api/monitoring/" + item.Route, request); Assert.Equal(HttpStatusCode.Created, created.StatusCode); var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
      var saved = await client.PostAsJsonAsync($"/api/monitoring/submissions/{id}/observations", new JsonObject { ["rows"] = new JsonArray(item.Row) }); Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
      Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/monitoring/submissions/{id}/finalize", null)).StatusCode);
      if (item.Route == "ppe") { var table = await client.GetFromJsonAsync<JsonElement>($"/api/monitoring/ppe-table?associationId={TestIds.AssociationA}&year=2045"); Assert.Equal(1, table.GetProperty("items")[0].GetProperty("observations").GetArrayLength()); }
      if (item.Route == "first-aid-kits") { var table = await client.GetFromJsonAsync<JsonElement>($"/api/monitoring/first-aid-table?associationId={TestIds.AssociationA}&year=2045"); Assert.Equal(1, table.GetProperty("items")[0].GetProperty("observations").GetArrayLength()); }
      if (item.Route == "member-complaints") { var table = await client.GetFromJsonAsync<JsonElement>($"/api/monitoring/complaints-table?associationId={TestIds.AssociationA}&year=2045"); Assert.Equal(1, table.GetProperty("items")[0].GetProperty("recordCount").GetInt32()); }
    }
  }
  [Fact]
  public async Task ExcelPreviewProvidesNormalizedRowsThatCanBeCommittedWithoutClientParsing() {
    using var client = factory.CreateClient(); client.AuthenticateAs(TestIds.AssociationAdminA, AppRoles.AssociationAdmin);
    using var memory = new MemoryStream();
    using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true)) {
      var entry = zip.CreateEntry("xl/worksheets/sheet1.xml"); using var writer = new StreamWriter(entry.Open(), Encoding.UTF8); XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
      string[][] rows = [["Nik", "LandLegalNumber", "Activity", "ActivityDate", "MaterialName", "TreeCount", "DosePerTreeKg", "UnitPrice"], ["1111111111111111", "LAHAN-A", "Fertilizing", "2046-02-01", "Urea", "10", "2", "5000"]];
      var xml = new XElement(ns + "worksheet", new XElement(ns + "sheetData", rows.Select((row,index) => new XElement(ns + "row", new XAttribute("r", index + 1), row.Select((value,column) => new XElement(ns + "c", new XAttribute("r", $"{(char)('A' + column)}{index + 1}"), new XAttribute("t", "inlineStr"), new XElement(ns + "is", new XElement(ns + "t", value)))))))); writer.Write(xml.ToString());
    }
    using var form = new MultipartFormDataContent(); form.Add(new StringContent(TestIds.AssociationA.ToString()), "associationId"); form.Add(new ByteArrayContent(memory.ToArray()), "file", "input.xlsx");
    var previewResponse = await client.PostAsync("/api/field-logs/import/excel-preview", form); Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode); var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>(); Assert.True(preview.GetProperty("preview").GetProperty("isValid").GetBoolean());
    var commit = await client.PostAsJsonAsync($"/api/field-logs/import?associationId={TestIds.AssociationA}", new { rows = preview.GetProperty("inputRows") }); Assert.Equal(HttpStatusCode.OK, commit.StatusCode);
    Assert.Equal(1, (await commit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("imported").GetInt32());
  }
  [Fact]
  public async Task ExplicitZeroDeclarationIsRequiredForEmptyEventReport() {
    using var client = factory.CreateClient(); client.AuthenticateAsSuperAdmin();
    var created = await client.PostAsJsonAsync("/api/monitoring/fires", new { poktanId = TestIds.PoktanA1, periodStart = "2047-01-01", periodEnd = "2047-12-31", incidents = new object[0] }); Assert.Equal(HttpStatusCode.Created, created.StatusCode); var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/monitoring/submissions/{id}/finalize", null)).StatusCode);
    Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/monitoring/submissions/{id}/zero-declaration", new { declaration = "Tidak ada kebakaran" })).StatusCode);
    Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/monitoring/submissions/{id}/finalize", null)).StatusCode);
  }
}
