using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.IntegrationTests.Fixtures;
namespace Esertifikasi.Api.IntegrationTests.Operations;
public sealed class MonitoringObservationTests(EsertifikasiWebApplicationFactory factory) : IClassFixture<EsertifikasiWebApplicationFactory>, IAsyncLifetime {
  public Task InitializeAsync() => factory.SeedAsync(); public Task DisposeAsync() => Task.CompletedTask;
  [Fact]
  public async Task MultiTypeObservationsHaveIndependentHistoryAndRespectFinalization() {
    using var client = factory.CreateClient(); client.AuthenticateAs(TestIds.AssociationAdminA, AppRoles.AssociationAdmin);
    var created = await client.PostAsJsonAsync("/api/monitoring/weeds", new { poktanId = TestIds.PoktanA1, periodStart = "2040-01-01", periodEnd = "2040-12-31", inspections = new object[0] }); Assert.Equal(HttpStatusCode.Created, created.StatusCode); var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(); var route = $"/api/monitoring/submissions/{id}";
    object Request(string date) => new { rows = new[] { new { petaniId = TestIds.PetaniA, lahanId = TestIds.LahanA, observedOn = date, weedType = "Pakis" }, new { petaniId = TestIds.PetaniA, lahanId = TestIds.LahanA, observedOn = date, weedType = "Rumput" } } };
    var response = await client.PostAsJsonAsync(route + "/observations", Request("2040-04-01")); Assert.Equal(HttpStatusCode.OK, response.StatusCode); var observation = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("observationId").GetGuid();
    response = await client.PutAsJsonAsync(route + $"/observations/{observation}", Request("2040-05-01")); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var detail = await client.GetFromJsonAsync<JsonElement>(route); Assert.Equal(2, detail.GetProperty("detail").GetProperty("inspections").GetArrayLength());
    var table = await client.GetFromJsonAsync<JsonElement>($"/api/monitoring/table?associationId={TestIds.AssociationA}&type=Weed&year=2040"); Assert.Equal(1, table.GetProperty("items")[0].GetProperty("observationCount").GetInt32());
    Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync(route + "/finalize", null)).StatusCode); Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(route + "/observations", Request("2040-06-01"))).StatusCode);
    client.AuthenticateAs(TestIds.MemberB, AppRoles.MemberTaniBaik); Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(route)).StatusCode);
  }
  [Fact]
  public async Task ConfidentialComplaintsAreHiddenFromPoktanAdminAndVisibleToComplainant() {
    using var client = factory.CreateClient(); client.AuthenticateAs(TestIds.AssociationAdminA, AppRoles.AssociationAdmin);
    var response = await client.PostAsJsonAsync("/api/monitoring/member-complaints", new { poktanId = TestIds.PoktanA1, periodStart = "2041-01-01", periodEnd = "2041-12-31", complaints = new[] { new { petaniId = TestIds.PetaniA, receivedOn = "2041-02-01", complaintType = "Bibit", description = "Private complaint", isConfidential = true } } }); Assert.Equal(HttpStatusCode.Created, response.StatusCode); var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    client.AuthenticateAs(TestIds.PoktanAdminA1, AppRoles.PoktanAdmin); Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/monitoring/submissions/{id}")).StatusCode);
    var route = $"/api/monitoring/complaints?associationId={TestIds.AssociationA}&year=2041";
    var list = await client.GetFromJsonAsync<JsonElement>(route); Assert.Equal(0, list.GetProperty("totalCount").GetInt32());
    client.AuthenticateAs(TestIds.MemberA, AppRoles.MemberTaniBaik); list = await client.GetFromJsonAsync<JsonElement>(route); Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
  }
  [Fact]
  public async Task ImportRequiresExplicitDuplicateDecisionAndSavesNothingOnInvalidRow() {
    using var client = factory.CreateClient(); client.AuthenticateAs(TestIds.AssociationAdminA, AppRoles.AssociationAdmin);
    object Row(string choice, decimal area) => new { nik = "1111111111111111", landLegalNumber = "LAHAN-A", duplicateChoice = choice, data = new { activity = "Spraying", activityDate = "2042-02-01", treatedAreaHa = area, materialName = "Pesticide", unitPrice = 10000, doseLitersPerHa = 1 } };
    var route = $"/api/field-logs/import?associationId={TestIds.AssociationA}";
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(route, new { rows = new[] { Row("", 1), Row("append", 100) } })).StatusCode);
    var list = await client.GetFromJsonAsync<JsonElement>($"/api/field-logs?associationId={TestIds.AssociationA}&activity=Spraying&year=2042"); Assert.Equal(0, list.GetProperty("totalCount").GetInt32());
    Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(route, new { rows = new[] { Row("", 1) } })).StatusCode);
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(route, new { rows = new[] { Row("", 1) } })).StatusCode);
    Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(route, new { rows = new[] { Row("skip", 1) } })).StatusCode);
    list = await client.GetFromJsonAsync<JsonElement>($"/api/field-logs?associationId={TestIds.AssociationA}&activity=Spraying&year=2042"); Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
  }
}
