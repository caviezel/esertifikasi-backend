using System.Net;
using System.Text.Json;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.IntegrationTests.Fixtures;

namespace Esertifikasi.Api.IntegrationTests.Authorization;

public sealed class LahanDocumentTests : IClassFixture<EsertifikasiWebApplicationFactory>, IAsyncLifetime {
  private readonly EsertifikasiWebApplicationFactory _factory;

  public LahanDocumentTests(EsertifikasiWebApplicationFactory factory) => _factory = factory;
  public Task InitializeAsync() => _factory.SeedAsync();
  public Task DisposeAsync() => Task.CompletedTask;

  [Fact]
  public async Task AssociationAdmin_CanLoadRequirementsAndUploadLahanDocument() {
    using var client = _factory.CreateClient();
    client.AuthenticateAs(TestIds.AssociationAdminA, AppRoles.AssociationAdmin);

    var requirements = await client.GetAsync($"/api/lahan/{TestIds.LahanA}/documents/requirements");
    using var upload = new MultipartFormDataContent();
    upload.Add(new StringContent(TestIds.DocumentTypeLahan.ToString()), "documentTypeId");
    upload.Add(new ByteArrayContent(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 }), "file", "sertifikat.pdf");
    var uploaded = await client.PostAsync($"/api/lahan/{TestIds.LahanA}/documents", upload);

    Assert.Equal(HttpStatusCode.OK, requirements.StatusCode);
    var requirementJson = JsonDocument.Parse(await requirements.Content.ReadAsStringAsync());
    Assert.Contains(requirementJson.RootElement.EnumerateArray(), x => x.GetProperty("id").GetGuid() == TestIds.DocumentTypeLahan);
    Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
  }
}
