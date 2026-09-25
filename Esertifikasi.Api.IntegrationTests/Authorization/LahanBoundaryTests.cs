using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Esertifikasi.Api.IntegrationTests.Fixtures;

namespace Esertifikasi.Api.IntegrationTests.Authorization;

public sealed class LahanBoundaryTests : IClassFixture<EsertifikasiWebApplicationFactory>, IAsyncLifetime {
  private readonly EsertifikasiWebApplicationFactory _factory;

  public LahanBoundaryTests(EsertifikasiWebApplicationFactory factory) => _factory = factory;
  public Task InitializeAsync() => _factory.SeedAsync();
  public Task DisposeAsync() => Task.CompletedTask;

  [Fact]
  public async Task Create_AcceptsAndReturnsValidMultiPolygon() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();
    using var boundary = JsonDocument.Parse(
        "{\"type\":\"MultiPolygon\",\"coordinates\":[[[[119.4,-5.1],[119.5,-5.1],[119.5,-5.2],[119.4,-5.1]]]]}");

    var create = await client.PostAsJsonAsync("/api/lahan", new {
      petaniId = TestIds.PetaniA,
      noLegalitas = $"BOUNDARY-{Guid.NewGuid():N}",
      desaId = TestIds.Village, rtRw = "001/002", alamat = "Jalan Kebun",
      metodeBuka = "Manual", dibukaOleh = "Petani", tahunDibuka = 2020,
      tutupanLahan = "Kelapa Sawit", perolehanTanahGarapan = "Warisan",
      peruntukanTanah = "Perkebunan", tanamanAwal = "Kelapa Sawit",
      jenisTanah = "Mineral", polaTanam = "Monokultur", mitraPengelola = "Ada",
      statusHgu = false, statusGambutFeg = false, statusGambutKhg = false,
      luasGeometri = 1000m, luasTerverifikasi = 990m, selisihLuas = 10m,
      boundary = boundary.RootElement
    });

    Assert.Equal(HttpStatusCode.Created, create.StatusCode);
    var id = JsonDocument.Parse(await create.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    var detail = await client.GetAsync($"/api/lahan/{id}");
    var json = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
    Assert.Equal("MultiPolygon", json.RootElement.GetProperty("boundary").GetProperty("type").GetString());
    Assert.Equal("Mineral", json.RootElement.GetProperty("jenisTanah").GetString());
    Assert.Equal("Ada", json.RootElement.GetProperty("mitraPengelola").GetString());
    Assert.Equal(10m, json.RootElement.GetProperty("selisihLuas").GetDecimal());
  }

  [Fact]
  public async Task Create_RejectsUnclosedMultiPolygonRing() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();
    using var boundary = JsonDocument.Parse(
        "{\"type\":\"MultiPolygon\",\"coordinates\":[[[[119.4,-5.1],[119.5,-5.1],[119.5,-5.2],[119.4,-5.2]]]]}");

    var response = await client.PostAsJsonAsync("/api/lahan", new {
      petaniId = TestIds.PetaniA,
      desaId = TestIds.Village, rtRw = "001/002", alamat = "Jalan Kebun",
      metodeBuka = "Manual", dibukaOleh = "Petani", tahunDibuka = 2020,
      tutupanLahan = "Kelapa Sawit", perolehanTanahGarapan = "Warisan",
      peruntukanTanah = "Perkebunan", tanamanAwal = "Kelapa Sawit",
      boundary = boundary.RootElement
    });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("Boundary", out _));
  }
}
