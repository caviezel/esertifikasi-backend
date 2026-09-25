using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.IntegrationTests.Fixtures;

namespace Esertifikasi.Api.IntegrationTests.Monitoring;

public sealed class MonitoringWorkflowTests : IClassFixture<EsertifikasiWebApplicationFactory>, IAsyncLifetime {
  private readonly EsertifikasiWebApplicationFactory _factory;
  public MonitoringWorkflowTests(EsertifikasiWebApplicationFactory factory) => _factory = factory;
  public Task InitializeAsync() => _factory.SeedAsync();
  public Task DisposeAsync() => Task.CompletedTask;

  [Fact]
  public async Task AllThirteenTypedMonitoringFormsCanBeCreatedAndFinalized() {
    using var client = _factory.CreateClient();
    client.AuthenticateAs(TestIds.AssociationAdminA, AppRoles.AssociationAdmin);
    var annual = Common(false);
    var semi = Common(true);
    var farmerRow = new JsonObject { ["petaniId"] = TestIds.PetaniA, ["lahanId"] = TestIds.LahanA, ["observedOn"] = "2026-04-25" };
    var requests = new (string Route, JsonObject Body)[] {
      ("land-boundaries", With(annual, "inspections", Row(farmerRow, ("markerCount", 4), ("condition", "Good")))),
      ("turnera", With(annual, "inspections", Row(farmerRow, ("condition", "Good")))),
      ("chemical-buffers", With(annual, "inspections", Row(farmerRow, ("hasRiverBoundaryMarker", true), ("noChemicalActivityWithinFiveMeters", true)))),
      ("woody-plants", With(annual, "inspections", Row(farmerRow, ("observations", new JsonArray(new JsonObject { ["treeName"] = "Matoa", ["quantity"] = 2 }))))),
      ("first-aid-kits", With(semi, "inspections", new JsonArray(new JsonObject { ["location"] = "Kantor", ["observedOn"] = "2026-04-25", ["items"] = new JsonArray(new JsonObject { ["itemName"] = "Kasa", ["condition"] = "Good" }) }))),
      ("ppe", With(annual, "inspections", Row(farmerRow, ("activity", "Harvesting"), ("items", new JsonArray(new JsonObject { ["itemName"] = "Helm", ["isAvailable"] = true, ["isUsed"] = true }))))),
      ("high-conservation-values", With(With(semi, "locations", Row(farmerRow, ("semester", 1), ("location", "Bantaran sungai"))), "speciesObservations", new JsonArray())),
      ("fires", With(With(annual, "noIncidents", true), "zeroIncidentDeclaration", "Tidak ada kebakaran")),
      ("workplace-accidents", With(With(annual, "noIncidents", true), "zeroIncidentDeclaration", "Tidak ada kecelakaan")),
      ("weeds", With(annual, "inspections", Row(farmerRow, ("weedType", "Pakis")))),
      ("plant-diseases", With(annual, "inspections", Row(farmerRow, ("diseaseType", "Tidak ada")))),
      ("pests", With(annual, "inspections", Row(farmerRow, ("pestType", "Ulat"), ("severity", "Light")))),
      ("member-complaints", With(With(annual, "noComplaints", true), "zeroComplaintDeclaration", "Tidak ada pengaduan"))
    };
    foreach (var (route, body) in requests) {
      var response = await client.PostAsJsonAsync($"/api/monitoring/{route}", body);
      Assert.Equal(HttpStatusCode.Created, response.StatusCode);
      var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
      Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/monitoring/submissions/{id}/finalize", null)).StatusCode);
      Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/monitoring/submissions/{id}")).StatusCode);
      Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/monitoring/submissions/{id}/export")).StatusCode);
    }
    var definitions = await client.GetFromJsonAsync<JsonElement>("/api/monitoring/definitions");
    Assert.Equal(13, definitions.GetArrayLength());
  }

  [Fact]
  public async Task RejectsOverlappingPeriodAndCrossPoktanLand() {
    using var client = _factory.CreateClient(); client.AuthenticateAsSuperAdmin();
    var valid = With(Common(false), "inspections", Row(new JsonObject { ["petaniId"] = TestIds.PetaniA, ["lahanId"] = TestIds.LahanA, ["observedOn"] = "2027-04-25" }, ("markerCount", 4), ("condition", "Good")));
    valid["periodStart"] = "2027-01-01"; valid["periodEnd"] = "2027-12-31";
    Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/monitoring/land-boundaries", valid)).StatusCode);
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/monitoring/land-boundaries", valid)).StatusCode);
    var invalid = With(Common(false), "inspections", Row(new JsonObject { ["petaniId"] = TestIds.PetaniB, ["lahanId"] = TestIds.LahanB, ["observedOn"] = "2026-04-25" }, ("condition", "Good")));
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/monitoring/turnera", invalid)).StatusCode);
  }

  private static JsonObject Common(bool semi) => new() { ["poktanId"] = TestIds.PoktanA1, ["periodStart"] = "2026-01-01", ["periodEnd"] = semi ? "2026-06-30" : "2026-12-31", ["summary"] = "Integration test" };
  private static JsonObject With(JsonObject source, string key, JsonNode? value) { var result = (JsonObject)source.DeepClone(); result[key] = value?.DeepClone(); return result; }
  private static JsonArray Row(JsonObject source, params (string Key, object Value)[] values) { var row = (JsonObject)source.DeepClone(); foreach (var (key, value) in values) row[key] = value is JsonNode node ? node.DeepClone() : JsonValue.Create(value); return new JsonArray(row); }
}
