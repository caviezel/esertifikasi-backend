using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.IntegrationTests.Fixtures;
namespace Esertifikasi.Api.IntegrationTests.Operations;
public sealed class OperationsWorkflowTests(EsertifikasiWebApplicationFactory factory) : IClassFixture<EsertifikasiWebApplicationFactory>, IAsyncLifetime {
  public Task InitializeAsync() => factory.SeedAsync();
  public Task DisposeAsync() => Task.CompletedTask;
  [Fact]
  public async Task AssociationTrainingUsesDailyAttendanceAndDoesNotRequireCycle() {
    using var client = factory.CreateClient(); client.AuthenticateAs(TestIds.AssociationAdminA, AppRoles.AssociationAdmin);
    var days = new[] { "2020-01-02", "2020-01-04" };
    var request = new { title = "Ongoing association training", startDate = "2020-01-02", endDate = "2020-01-04", days, petaniIds = new[] { TestIds.PetaniA }, topicIds = new[] { Guid.Parse("b6000000-0000-0000-0000-000000000001") } };
    var created = await client.PostAsJsonAsync($"/api/associations/{TestIds.AssociationA}/training", request);
    Assert.Equal(HttpStatusCode.Created, created.StatusCode); var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(); var route = $"/api/associations/{TestIds.AssociationA}/training/{id}";
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(route + "/complete", null)).StatusCode);
    Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(route + $"/attendance/{TestIds.PetaniA}/{days[0]}", new { present = true })).StatusCode);
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(route + "/complete", null)).StatusCode);
    Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(route + $"/attendance/{TestIds.PetaniA}/{days[1]}", new { present = false })).StatusCode);
    Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync(route + "/complete", null)).StatusCode);
    var detail = await client.GetFromJsonAsync<JsonElement>(route); Assert.False(detail.GetProperty("participants")[0].GetProperty("completed").GetBoolean());
    Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(route + $"/attendance/{TestIds.PetaniA}/{days[1]}", new { present = true })).StatusCode);
    Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(route + "/reopen", new { reason = "Correct attendance" })).StatusCode);
    Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(route + $"/attendance/{TestIds.PetaniA}/{days[1]}", new { present = true })).StatusCode);
    detail = await client.GetFromJsonAsync<JsonElement>(route); Assert.True(detail.GetProperty("participants")[0].GetProperty("completed").GetBoolean());
    client.AuthenticateAs(TestIds.MemberB, AppRoles.MemberTaniBaik); Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(route)).StatusCode);
  }
  [Fact]
  public async Task FieldLogsComputeCostsLockFinalizedRecordsAndKeepSequenceGaps() {
    using var client = factory.CreateClient(); client.AuthenticateAs(TestIds.AssociationAdminA, AppRoles.AssociationAdmin);
    object Request(string date) => new { lahanId = TestIds.LahanA, activity = "Fertilizing", activityDate = date, materialName = "Urea", treeCount = 100, dosePerTreeKg = 2, unitPrice = 5000, workerCount = 2, wagePerPerson = 100000 };
    var first = await client.PostAsJsonAsync("/api/field-logs", Request("2031-01-03")); Assert.Equal(HttpStatusCode.Created, first.StatusCode); var row = await first.Content.ReadFromJsonAsync<JsonElement>(); var id = row.GetProperty("id").GetGuid();
    Assert.Equal(1000000m, row.GetProperty("materialCost").GetDecimal()); Assert.Equal(1200000m, row.GetProperty("totalCost").GetDecimal()); Assert.Equal(1, row.GetProperty("sequenceNumber").GetInt32());
    Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/field-logs/{id}/finalize", null)).StatusCode);
    Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/field-logs/{id}", Request("2031-01-03"))).StatusCode);
    var totals = await client.GetFromJsonAsync<JsonElement>($"/api/field-logs/totals?lahanId={TestIds.LahanA}&activity=Fertilizing&year=2031"); Assert.Equal(1200000m, totals.GetProperty("overall").GetProperty("totalCost").GetDecimal());
    Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/field-logs/{id}/reopen", new { reason = "Duplicate" })).StatusCode);
    Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/field-logs/{id}")).StatusCode);
    row = await (await client.PostAsJsonAsync("/api/field-logs", Request("2031-01-03"))).Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(2, row.GetProperty("sequenceNumber").GetInt32());
    row = await (await client.PostAsJsonAsync("/api/field-logs", Request("2032-01-03"))).Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(1, row.GetProperty("sequenceNumber").GetInt32());
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/field-logs", new { lahanId = TestIds.LahanA, activity = "Spraying", activityDate = "2031-02-01", treatedAreaHa = 11 })).StatusCode);
    client.AuthenticateAs(TestIds.MemberA, AppRoles.MemberTaniBaik); Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/field-logs", Request("2031-01-03"))).StatusCode);
  }
  [Fact]
  public async Task RotationsRejectOverlapAndHarvestOutsideDates() {
    using var client = factory.CreateClient(); client.AuthenticateAsSuperAdmin(); var rotation = new { lahanId = TestIds.LahanA, name = "Rotation", startDate = "2030-01-01", endDate = "2030-06-30" };
    var response = await client.PostAsJsonAsync("/api/field-logs/rotations", rotation); Assert.Equal(HttpStatusCode.Created, response.StatusCode); var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/field-logs/rotations", rotation)).StatusCode);
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/field-logs", new { lahanId = TestIds.LahanA, activity = "Harvest", activityDate = "2030-07-01", rotationId = id })).StatusCode);
  }
}
