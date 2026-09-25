using System.Net;
using System.Net.Http.Json;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.IntegrationTests.Fixtures;
using Esertifikasi.Api.Models;

namespace Esertifikasi.Api.IntegrationTests.Authorization;

public sealed class DashboardSummaryTests : IClassFixture<EsertifikasiWebApplicationFactory>, IAsyncLifetime {
  private readonly EsertifikasiWebApplicationFactory _factory;

  public DashboardSummaryTests(EsertifikasiWebApplicationFactory factory) {
    _factory = factory;
  }

  public Task InitializeAsync() => _factory.SeedAsync();

  public Task DisposeAsync() => Task.CompletedTask;

  [Fact]
  public async Task AnonymousCannotReadSummary() {
    using var client = _factory.CreateClient();

    var response = await client.GetAsync("/api/dashboard/summary");

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task SuperAdminReceivesSystemTotals() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();

    var summary = await GetSummaryAsync(client);

    Assert.Equal(new DashboardSummaryResponse(2, 3, 3, 2, 30.75m), summary);
  }

  [Fact]
  public async Task AssociationAdminReceivesAssignedAssociationTotals() {
    using var client = _factory.CreateClient();
    client.AuthenticateAs(TestIds.AssociationAdminA, AppRoles.AssociationAdmin);

    var summary = await GetSummaryAsync(client);

    Assert.Equal(new DashboardSummaryResponse(1, 2, 2, 1, 10.25m), summary);
  }

  [Fact]
  public async Task PoktanAdminReceivesAssignedPoktanTotals() {
    using var client = _factory.CreateClient();
    client.AuthenticateAs(TestIds.PoktanAdminA1, AppRoles.PoktanAdmin);

    var summary = await GetSummaryAsync(client);

    Assert.Equal(new DashboardSummaryResponse(1, 1, 1, 1, 10.25m), summary);
  }

  [Fact]
  public async Task MemberReceivesOnlyOwnPetaniAndLahanTotals() {
    using var client = _factory.CreateClient();
    client.AuthenticateAs(TestIds.MemberA, AppRoles.MemberTaniBaik);

    var summary = await GetSummaryAsync(client);

    Assert.Equal(new DashboardSummaryResponse(0, 0, 1, 1, 10.25m), summary);
  }

  private static async Task<DashboardSummaryResponse> GetSummaryAsync(HttpClient client) {
    var response = await client.GetAsync("/api/dashboard/summary");
    response.EnsureSuccessStatusCode();

    return (await response.Content.ReadFromJsonAsync<DashboardSummaryResponse>())!;
  }
}
