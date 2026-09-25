using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Esertifikasi.Api.IntegrationTests.Regions;

public sealed class AdministrativeRegionTests : IClassFixture<EsertifikasiWebApplicationFactory>, IAsyncLifetime {
  private readonly EsertifikasiWebApplicationFactory _factory;

  public AdministrativeRegionTests(EsertifikasiWebApplicationFactory factory) => _factory = factory;
  public Task InitializeAsync() => _factory.SeedAsync();
  public Task DisposeAsync() => Task.CompletedTask;

  [Fact]
  public async Task Petani_CanReferenceLocallyStoredVillage() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();
    var provinceId = 99L;
    var regencyId = 9901L;
    var districtId = 990101L;
    var villageId = 9901019999L;

    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      var now = DateTimeOffset.UtcNow;
      db.Provinces.Add(new Province { Id = provinceId, Code = "99", Name = "PROVINSI TEST", SourceUpdatedAt = now, SyncedAt = now });
      db.Regencies.Add(new Regency { Id = regencyId, Code = "99.01", ProvinceId = provinceId, Name = "KABUPATEN TEST", SourceUpdatedAt = now, SyncedAt = now });
      db.Districts.Add(new District { Id = districtId, Code = "99.01.01", RegencyId = regencyId, Name = "KECAMATAN TEST", SourceUpdatedAt = now, SyncedAt = now });
      db.Villages.Add(new Village { Id = villageId, Code = "99.01.01.9999", DistrictId = districtId, Name = "DESA TEST", SourceUpdatedAt = now, SyncedAt = now });
      await db.SaveChangesAsync();
    }
    var village = await client.GetAsync($"/api/regions/villages/{villageId}");
    var petani = await client.PostAsJsonAsync("/api/petani", new {
      poktanId = TestIds.PoktanA1,
      nama = "Petani Dengan Wilayah",
      nik = "9899997654321098",
      tempatLahir = "Makassar", tanggalLahir = "1990-01-01", jenisKelamin = "LakiLaki",
      desaId = villageId, rtRw = "001/002", alamat = "Jalan Test",
      statusPerkawinan = "Kawin", pekerjaan = "Petani", kewarganegaraan = "Wni", suku = "Bugis",
      noTelepon = "08123456789", noKk = "1234567890123456", namaKepalaKeluarga = "Kepala Keluarga",
      jumlahAnggotaKeluarga = 4, jumlahAnak = 2, pendidikanTerakhir = "Sma"
    });

    Assert.Equal(HttpStatusCode.OK, village.StatusCode);
    Assert.Equal(HttpStatusCode.Created, petani.StatusCode);
    var json = JsonDocument.Parse(await village.Content.ReadAsStringAsync());
    Assert.Equal(provinceId, json.RootElement.GetProperty("provinceId").GetInt64());
  }

  [Fact]
  public async Task Petani_RejectsUnknownVillage() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();

    var response = await client.PostAsJsonAsync("/api/petani", new {
      poktanId = TestIds.PoktanA1,
      nama = "Petani Desa Tidak Ada",
      nik = "9765432109876543",
      desaId = 999999999999L
    });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }
}
