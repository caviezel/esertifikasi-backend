using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.IntegrationTests.Fixtures;
using Esertifikasi.Api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Esertifikasi.Api.IntegrationTests.Infrastructure;

public sealed class DocumentTypeSeedTests : IClassFixture<EsertifikasiWebApplicationFactory>, IAsyncLifetime {
  private readonly EsertifikasiWebApplicationFactory _factory;

  public DocumentTypeSeedTests(EsertifikasiWebApplicationFactory factory) {
    _factory = factory;
  }

  public Task InitializeAsync() => _factory.SeedAsync();

  public Task DisposeAsync() => Task.CompletedTask;

  [Fact]
  public async Task IdentitySeeder_CreatesCertificateDocumentTypeIdempotently() {
    var configuration = _factory.Services.GetRequiredService<IConfiguration>();

    await IdentitySeeder.SeedAsync(_factory.Services, configuration);
    await IdentitySeeder.SeedAsync(_factory.Services, configuration);

    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var types = await db.DocumentTypes
        .Where(x => x.OwnerType == DocumentOwnerType.CertificationCycle && x.Code == "CERTIFICATE")
        .ToListAsync();

    var type = Assert.Single(types);
    Assert.Equal("Sertifikat", type.Nama);
    Assert.True(type.IsActive);
    Assert.True(type.IsRequired);
    Assert.Equal(".pdf", type.AllowedExtensions);
    Assert.Equal(10 * 1024 * 1024, type.MaximumFileSize);

    var cycleCount = await db.CertificationCycles.CountAsync();
    var requirementCount = await db.CycleDocumentRequirements.CountAsync(x =>
        x.DocumentTypeId == type.Id && x.OwnerType == DocumentOwnerType.CertificationCycle);
    Assert.Equal(cycleCount, requirementCount);
  }
}
