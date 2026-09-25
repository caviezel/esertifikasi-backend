using System.Net;
using System.Text.Json;
using Esertifikasi.Api.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Esertifikasi.Api.IntegrationTests.Authorization;

public sealed class SoftDeleteTests : IClassFixture<EsertifikasiWebApplicationFactory>, IAsyncLifetime {
  private readonly EsertifikasiWebApplicationFactory _factory;

  public SoftDeleteTests(EsertifikasiWebApplicationFactory factory) {
    _factory = factory;
  }

  public Task InitializeAsync() => _factory.SeedAsync();

  public Task DisposeAsync() => Task.CompletedTask;

  [Fact]
  public async Task DeletingPoktan_HidesChildrenWithoutPhysicallyRemovingThem() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();

    var deleteResponse = await client.DeleteAsync($"/api/poktan/{TestIds.PoktanADelete}");
    var listResponse = await client.GetAsync($"/api/petani?poktanId={TestIds.PoktanADelete}");
    var json = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());

    Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
    Assert.Equal(0, json.RootElement.GetProperty("totalCount").GetInt32());

    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var physicalRowExists = await db.Petani.IgnoreQueryFilters().AnyAsync(x => x.Id == TestIds.PetaniDelete);
    Assert.True(physicalRowExists);
  }
}
