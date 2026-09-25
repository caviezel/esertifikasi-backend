using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.IntegrationTests.Fixtures;

namespace Esertifikasi.Api.IntegrationTests.Authorization;

public sealed class AuthorizationTests : IClassFixture<EsertifikasiWebApplicationFactory>, IAsyncLifetime {
  private readonly EsertifikasiWebApplicationFactory _factory;

  public AuthorizationTests(EsertifikasiWebApplicationFactory factory) {
    _factory = factory;
  }

  public Task InitializeAsync() => _factory.SeedAsync();

  public Task DisposeAsync() => Task.CompletedTask;

  [Fact]
  public async Task Anonymous_CannotAccessProtectedEndpoint() {
    using var client = _factory.CreateClient();

    var response = await client.GetAsync("/api/associations");

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task AssociationAdmin_CanReadOwnPetani_ButNotOtherAssociation() {
    using var client = _factory.CreateClient();
    client.AuthenticateAs(TestIds.AssociationAdminA, AppRoles.AssociationAdmin);

    var ownResponse = await client.GetAsync($"/api/petani/{TestIds.PetaniA}");
    var otherResponse = await client.GetAsync($"/api/petani/{TestIds.PetaniB}");

    Assert.Equal(HttpStatusCode.OK, ownResponse.StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, otherResponse.StatusCode);
  }

  [Fact]
  public async Task PoktanAdmin_CannotReadPetaniFromAnotherPoktan() {
    using var client = _factory.CreateClient();
    client.AuthenticateAs(TestIds.PoktanAdminA1, AppRoles.PoktanAdmin);

    var ownResponse = await client.GetAsync($"/api/petani/{TestIds.PetaniA}");
    var otherResponse = await client.GetAsync($"/api/petani/{TestIds.PetaniB}");

    Assert.Equal(HttpStatusCode.OK, ownResponse.StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, otherResponse.StatusCode);
  }

  [Fact]
  public async Task Member_CanReadOwnProfileAndDocument_ButNotAnotherMembersDocument() {
    using var client = _factory.CreateClient();
    client.AuthenticateAs(TestIds.MemberA, AppRoles.MemberTaniBaik);

    var profileResponse = await client.GetAsync("/api/petani/me");
    var ownDocumentResponse = await client.GetAsync($"/api/documents/{TestIds.DocumentA}");
    var otherDocumentResponse = await client.GetAsync($"/api/documents/{TestIds.DocumentB}");

    Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
    Assert.Equal(HttpStatusCode.OK, ownDocumentResponse.StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, otherDocumentResponse.StatusCode);
  }

  [Fact]
  public async Task Member_CannotUploadOrDeleteOwnDocument() {
    using var client = _factory.CreateClient();
    client.AuthenticateAs(TestIds.MemberA, AppRoles.MemberTaniBaik);
    using var upload = new MultipartFormDataContent();
    upload.Add(new StringContent(TestIds.DocumentTypePetani.ToString()), "documentTypeId");
    upload.Add(new ByteArrayContent(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }), "file", "ktp.pdf");

    var uploadResponse = await client.PostAsync($"/api/petani/{TestIds.PetaniA}/documents", upload);
    var deleteResponse = await client.DeleteAsync($"/api/documents/{TestIds.DocumentA}");

    Assert.Equal(HttpStatusCode.Forbidden, uploadResponse.StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
  }

  [Fact]
  public async Task InvalidRequest_ReturnsIndonesianValidationProblem() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();

    var response = await client.PostAsJsonAsync("/api/associations", new { nama = "", noTelp = "telepon-tidak-valid" });
    var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("Validasi permintaan gagal", json.RootElement.GetProperty("title").GetString());
    Assert.Contains("Nilai untuk field", json.RootElement.GetProperty("errors").GetProperty("Nama")[0].GetString());
  }

  [Fact]
  public async Task CreatePoktan_ReturnsCreatedResponseWithAssociationName() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();

    var response = await client.PostAsJsonAsync("/api/poktan", new {
      associationId = TestIds.AssociationA,
      nama = $"Poktan {Guid.NewGuid():N}",
      negara = "Indonesia",
      provinceId = TestIds.Province,
      regencyId = TestIds.Regency
    });
    var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    Assert.Equal("Association A", json.RootElement.GetProperty("associationNama").GetString());
  }

  [Fact]
  public async Task PetaniRequest_RequiresNik() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();

    var response = await client.PostAsJsonAsync("/api/petani", new {
      poktanId = TestIds.PoktanA1,
      nama = "Petani Tanpa NIK"
    });
    var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("Nik", out _));
  }

  [Fact]
  public async Task PetaniAndLahan_RejectNonPositiveDesaId() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();

    var petaniResponse = await client.PostAsJsonAsync("/api/petani", new {
      poktanId = TestIds.PoktanA1,
      nama = "Petani Wilayah Tidak Valid",
      nik = "9876543210987654",
      desaId = 0
    });
    var lahanResponse = await client.PostAsJsonAsync("/api/lahan", new {
      petaniId = TestIds.PetaniA,
      desaId = -1
    });

    Assert.Equal(HttpStatusCode.BadRequest, petaniResponse.StatusCode);
    Assert.Equal(HttpStatusCode.BadRequest, lahanResponse.StatusCode);
  }

  [Fact]
  public async Task LahanRequest_RequiresSpreadsheetRegistrationFields() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();

    var response = await client.PostAsJsonAsync("/api/lahan", new { petaniId = TestIds.PetaniA });
    var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var errors = json.GetProperty("errors");
    Assert.True(errors.TryGetProperty("DesaId", out _));
    Assert.True(errors.TryGetProperty("MetodeBuka", out _));
    Assert.True(errors.TryGetProperty("PerolehanTanahGarapan", out _));
    Assert.True(errors.TryGetProperty("PeruntukanTanah", out _));
    Assert.True(errors.TryGetProperty("TanamanAwal", out _));
  }
}
