using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.IntegrationTests.Fixtures;
using Esertifikasi.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Esertifikasi.Api.IntegrationTests.Authorization;

public sealed class ScopedSummaryTests : IClassFixture<EsertifikasiWebApplicationFactory>, IAsyncLifetime {
  private static readonly Guid EmptyAssociationId = Guid.Parse("70000000-0000-0000-0000-000000000001");
  private static readonly Guid EmptyPoktanId = Guid.Parse("70000000-0000-0000-0000-000000000002");
  private static readonly Guid EmptyPetaniId = Guid.Parse("70000000-0000-0000-0000-000000000003");
  private static readonly Guid DeletedAssociationId = Guid.Parse("70000000-0000-0000-0000-000000000004");
  private static readonly Guid DeletedPoktanId = Guid.Parse("70000000-0000-0000-0000-000000000005");
  private static readonly Guid DeletedPetaniId = Guid.Parse("70000000-0000-0000-0000-000000000006");
  private static readonly Guid MissingId = Guid.Parse("70000000-0000-0000-0000-000000000099");
  private readonly EsertifikasiWebApplicationFactory _factory;

  public ScopedSummaryTests(EsertifikasiWebApplicationFactory factory) {
    _factory = factory;
  }

  public async Task InitializeAsync() {
    await _factory.SeedAsync();

    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (await db.Associations.IgnoreQueryFilters().AnyAsync(x => x.Id == EmptyAssociationId)) {
      return;
    }

    db.Associations.AddRange(
        new Association { Id = EmptyAssociationId, Nama = "Association Kosong" },
        new Association { Id = DeletedAssociationId, Nama = "Association Terhapus", IsDeleted = true });
    db.Poktan.AddRange(
        new Poktan { Id = EmptyPoktanId, AssociationId = TestIds.AssociationA, Nama = "Poktan Kosong" },
        new Poktan { Id = DeletedPoktanId, AssociationId = TestIds.AssociationA, Nama = "Poktan Terhapus", IsDeleted = true });
    db.Petani.AddRange(
        new Petani { Id = EmptyPetaniId, PoktanId = TestIds.PoktanA1, Nama = "Petani Kosong", Nik = "4444444444444444" },
        new Petani { Id = DeletedPetaniId, PoktanId = TestIds.PoktanA1, Nama = "Petani Terhapus", Nik = "5555555555555555", IsDeleted = true },
        new Petani { Id = Guid.NewGuid(), PoktanId = DeletedPoktanId, Nama = "Petani Dalam Poktan Terhapus", Nik = "6666666666666666" });
    db.Lahan.AddRange(
        new Lahan { Id = Guid.NewGuid(), PetaniId = TestIds.PetaniA, NoLegalitas = "LAHAN-TERHAPUS", LuasLegalitas = 100m, IsDeleted = true },
        new Lahan { Id = Guid.NewGuid(), PetaniId = DeletedPetaniId, NoLegalitas = "LAHAN-PETANI-TERHAPUS", LuasLegalitas = 200m },
        new Lahan {
          Id = Guid.NewGuid(),
          PetaniId = db.Petani.Local.Single(x => x.PoktanId == DeletedPoktanId).Id,
          NoLegalitas = "LAHAN-POKTAN-TERHAPUS",
          LuasLegalitas = 300m
        });

    var mappedLahan = await db.Lahan.SingleAsync(x => x.Id == TestIds.LahanA);
    mappedLahan.BoundaryGeoJson = "{\"type\":\"MultiPolygon\",\"coordinates\":[[[[0,0],[0,1],[1,1],[1,0],[0,0]]]]}";

    await db.SaveChangesAsync();
  }

  public Task DisposeAsync() => Task.CompletedTask;

  [Fact]
  public async Task AssociationSummaryCountsOnlyActiveDescendants() {
    using var client = _factory.CreateClient();
    client.AuthenticateAs(TestIds.AssociationAdminA, AppRoles.AssociationAdmin);

    var summary = await GetAsync<AssociationSummaryResponse>(
        client,
        $"/api/associations/{TestIds.AssociationA}/summary");

    Assert.Equal(new AssociationSummaryResponse(3, 3, 1, 10.25m), summary);
  }

  [Fact]
  public async Task PoktanSummaryCountsOnlyActiveDescendants() {
    using var client = _factory.CreateClient();
    client.AuthenticateAs(TestIds.PoktanAdminA1, AppRoles.PoktanAdmin);

    var summary = await GetAsync<PoktanSummaryResponse>(
        client,
        $"/api/poktan/{TestIds.PoktanA1}/summary");

    Assert.Equal(new PoktanSummaryResponse(2, 1, 10.25m), summary);
  }

  [Fact]
  public async Task MemberCanReadOnlyOwnPetaniSummary() {
    using var client = _factory.CreateClient();
    client.AuthenticateAs(TestIds.MemberA, AppRoles.MemberTaniBaik);

    var ownSummary = await GetAsync<PetaniSummaryResponse>(
        client,
        $"/api/petani/{TestIds.PetaniA}/summary");
    var otherResponse = await client.GetAsync($"/api/petani/{TestIds.PetaniB}/summary");
    var associationResponse = await client.GetAsync($"/api/associations/{TestIds.AssociationA}/summary");
    var poktanResponse = await client.GetAsync($"/api/poktan/{TestIds.PoktanA1}/summary");

    Assert.Equal(new PetaniSummaryResponse(1, 10.25m), ownSummary);
    Assert.Equal(HttpStatusCode.Forbidden, otherResponse.StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, associationResponse.StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, poktanResponse.StatusCode);
  }

  [Fact]
  public async Task LahanSummaryReturnsReadinessAndDocumentCounts() {
    using var client = _factory.CreateClient();
    client.AuthenticateAs(TestIds.MemberA, AppRoles.MemberTaniBaik);

    var response = await client.GetAsync($"/api/lahan/{TestIds.LahanA}/summary");
    var otherResponse = await client.GetAsync($"/api/lahan/{TestIds.LahanB}/summary");
    var summary = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.True(summary.GetProperty("boundaryAvailable").GetBoolean());
    Assert.Equal("NotAssessed", summary.GetProperty("baselineStatus").GetString());
    Assert.False(summary.GetProperty("isCertified").GetBoolean());
    Assert.Equal(1, summary.GetProperty("requiredDocuments").GetInt32());
    Assert.Equal(0, summary.GetProperty("uploadedDocuments").GetInt32());
    Assert.Equal(0, summary.GetProperty("verifiedDocuments").GetInt32());
    Assert.Equal("Incomplete", summary.GetProperty("documentsStatus").GetString());
    Assert.Equal(HttpStatusCode.NotFound, otherResponse.StatusCode);
  }

  [Fact]
  public async Task LahanByPetaniReturnsSummaryForEveryRow() {
    var duplicateDocumentIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      db.Documents.AddRange(
          new DocumentRecord {
            Id = duplicateDocumentIds[0], DocumentTypeId = TestIds.DocumentTypeLahan,
            LahanId = TestIds.LahanA, UploadedByUserId = TestIds.SuperAdmin,
            Status = DocumentStatus.Verified
          },
          new DocumentRecord {
            Id = duplicateDocumentIds[1], DocumentTypeId = TestIds.DocumentTypeLahan,
            LahanId = TestIds.LahanA, UploadedByUserId = TestIds.SuperAdmin,
            Status = DocumentStatus.Verified
          });
      await db.SaveChangesAsync();
    }
    try {
      using var client = _factory.CreateClient();
      client.AuthenticateAs(TestIds.MemberA, AppRoles.MemberTaniBaik);

      var items = await GetAsync<List<PetaniLahanListItem>>(
          client, $"/api/lahan/by-petani/{TestIds.PetaniA}");

      var item = Assert.Single(items);
      Assert.Equal(TestIds.LahanA, item.Id);
      Assert.True(item.BoundaryAvailable);
      Assert.Equal(BaselineSummaryStatus.NotAssessed, item.BaselineStatus);
      Assert.False(item.IsCertified);
      Assert.Equal(1, item.RequiredDocuments);
      Assert.Equal(1, item.UploadedDocuments);
      Assert.Equal(1, item.VerifiedDocuments);
      Assert.Equal(DocumentCompletionStatus.Verified, item.DocumentsStatus);
    }
    finally {
      using var scope = _factory.Services.CreateScope();
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      var duplicates = await db.Documents.Where(x => duplicateDocumentIds.Contains(x.Id)).ToListAsync();
      db.Documents.RemoveRange(duplicates);
      await db.SaveChangesAsync();
    }
  }

  [Fact]
  public async Task EmptyResourcesReturnZeroTotals() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();

    var association = await GetAsync<AssociationSummaryResponse>(client, $"/api/associations/{EmptyAssociationId}/summary");
    var poktan = await GetAsync<PoktanSummaryResponse>(client, $"/api/poktan/{EmptyPoktanId}/summary");
    var petani = await GetAsync<PetaniSummaryResponse>(client, $"/api/petani/{EmptyPetaniId}/summary");

    Assert.Equal(new AssociationSummaryResponse(0, 0, 0, 0m), association);
    Assert.Equal(new PoktanSummaryResponse(0, 0, 0m), poktan);
    Assert.Equal(new PetaniSummaryResponse(0, 0m), petani);
  }

  [Fact]
  public async Task AdminsCannotReadResourcesOutsideTheirScope() {
    using var associationClient = _factory.CreateClient();
    associationClient.AuthenticateAs(TestIds.AssociationAdminA, AppRoles.AssociationAdmin);
    using var poktanClient = _factory.CreateClient();
    poktanClient.AuthenticateAs(TestIds.PoktanAdminA1, AppRoles.PoktanAdmin);

    var associationResponse = await associationClient.GetAsync($"/api/associations/{TestIds.AssociationB}/summary");
    var poktanResponse = await poktanClient.GetAsync($"/api/poktan/{TestIds.PoktanB1}/summary");
    var petaniResponse = await poktanClient.GetAsync($"/api/petani/{TestIds.PetaniB}/summary");

    Assert.Equal(HttpStatusCode.Forbidden, associationResponse.StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, poktanResponse.StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, petaniResponse.StatusCode);
  }

  [Fact]
  public async Task SuperAdminReceivesNotFoundForMissingResources() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();

    var association = await client.GetAsync($"/api/associations/{MissingId}/summary");
    var poktan = await client.GetAsync($"/api/poktan/{MissingId}/summary");
    var petani = await client.GetAsync($"/api/petani/{MissingId}/summary");

    Assert.Equal(HttpStatusCode.NotFound, association.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, poktan.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, petani.StatusCode);
  }

  [Fact]
  public async Task SoftDeletedParentsReturnNotFoundForSuperAdmin() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();

    var association = await client.GetAsync($"/api/associations/{DeletedAssociationId}/summary");
    var poktan = await client.GetAsync($"/api/poktan/{DeletedPoktanId}/summary");
    var petani = await client.GetAsync($"/api/petani/{DeletedPetaniId}/summary");

    Assert.Equal(HttpStatusCode.NotFound, association.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, poktan.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, petani.StatusCode);
  }

  [Fact]
  public async Task CertificationDatabaseLahan_ReturnsFlatPagedRows() {
    using var client = _factory.CreateClient();
    client.AuthenticateAs(TestIds.AssociationAdminA, AppRoles.AssociationAdmin);

    var result = await GetAsync<CertificationDatabaseLahanPage>(client,
        $"/api/associations/{TestIds.AssociationA}/certification-database/lahan?search=LAHAN-A");

    var item = Assert.Single(result.Items);
    Assert.Equal(TestIds.LahanA, item.LahanId);
    Assert.Equal(TestIds.PetaniA, item.PetaniId);
    Assert.Equal("Petani A", item.PetaniName);
    Assert.Equal("Poktan A1", item.PoktanName);
    Assert.True(item.BoundaryAvailable);
    Assert.Equal(BaselineSummaryStatus.NotAssessed, item.BaselineStatus);
    Assert.False(item.IsCertified);
    Assert.Equal(1, item.RequiredDocuments);
    Assert.Equal(0, item.UploadedDocuments);
    Assert.Equal(0, item.VerifiedDocuments);
    Assert.Equal(DocumentCompletionStatus.Incomplete, item.DocumentsStatus);
    Assert.Equal(1, result.TotalCount);
  }

  [Fact]
  public async Task CertificationDatabaseLahanMap_ReturnsGeoJsonAndUnavailableCount() {
    using var client = _factory.CreateClient();
    client.AuthenticateAs(TestIds.AssociationAdminA, AppRoles.AssociationAdmin);

    var result = await GetAsync<CertificationDatabaseLahanMapResponse>(client,
        $"/api/associations/{TestIds.AssociationA}/certification-database/lahan/map");

    Assert.Equal("FeatureCollection", result.Type);
    var feature = Assert.Single(result.Features);
    Assert.Equal(TestIds.LahanA, feature.Id);
    Assert.Equal("MultiPolygon", feature.Geometry.GetProperty("type").GetString());
    Assert.Equal(0, result.BoundaryUnavailableCount);
  }

  private static async Task<T> GetAsync<T>(HttpClient client, string path) {
    var response = await client.GetAsync(path);
    response.EnsureSuccessStatusCode();

    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    options.Converters.Add(new JsonStringEnumConverter());
    return (await response.Content.ReadFromJsonAsync<T>(options))!;
  }
}
