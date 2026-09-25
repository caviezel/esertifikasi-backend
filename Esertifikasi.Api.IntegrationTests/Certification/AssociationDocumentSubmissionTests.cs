using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Esertifikasi.Api.IntegrationTests.Certification;

public sealed class AssociationDocumentSubmissionTests : IClassFixture<EsertifikasiWebApplicationFactory>, IAsyncLifetime {
  private readonly EsertifikasiWebApplicationFactory _factory;

  public AssociationDocumentSubmissionTests(EsertifikasiWebApplicationFactory factory) => _factory = factory;
  public Task InitializeAsync() => _factory.SeedAsync();
  public Task DisposeAsync() => Task.CompletedTask;

  [Fact]
  public async Task AssociationDocumentVersion_CanBeAttachedSubmittedAndVerifiedForCycle() {
    var typeId = Guid.NewGuid();
    var documentId = Guid.NewGuid();
    var versionId = Guid.NewGuid();
    Guid cycleId;
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      var cycle = await db.CertificationCycles.SingleOrDefaultAsync(x => x.AssociationId == TestIds.AssociationA && x.IsCurrent);
      if (cycle is null) {
        cycle = new CertificationCycle {
          Id = Guid.NewGuid(), AssociationId = TestIds.AssociationA, Type = CertificationCycleType.InitialCertification,
          SequenceNumber = 0, Status = CertificationCycleStatus.Active, CurrentPhase = CertificationPhase.Disclosure,
          StartDate = DateOnly.FromDateTime(DateTime.UtcNow), IsCurrent = true, CreatedByUserId = TestIds.SuperAdmin
        };
        db.CertificationCycles.Add(cycle);
      }
      cycleId = cycle.Id;
      db.DocumentTypes.Add(new DocumentType {
        Id = typeId, Code = $"ASSOCIATION_TEST_{typeId:N}", Nama = "Association Test",
        OwnerType = DocumentOwnerType.Association, IsRequired = true
      });
      db.Documents.Add(new DocumentRecord {
        Id = documentId, DocumentTypeId = typeId, AssociationId = TestIds.AssociationA,
        UploadedByUserId = TestIds.AssociationAdminA
      });
      db.DocumentVersions.Add(new DocumentVersion {
        Id = versionId, DocumentId = documentId, VersionNumber = 1, StorageKey = "association/test.pdf",
        OriginalFileName = "akta.pdf", ContentType = "application/pdf", FileExtension = ".pdf",
        FileSize = 5, Sha256Hash = new string('A', 64), UploadedByUserId = TestIds.AssociationAdminA
      });
      db.CycleDocumentRequirements.Add(new CycleDocumentRequirement {
        Id = Guid.NewGuid(), CertificationCycleId = cycleId, DocumentTypeId = typeId,
        OwnerType = DocumentOwnerType.Association, IsRequired = true
      });
      await db.SaveChangesAsync();
    }

    using var associationAdmin = _factory.CreateClient();
    associationAdmin.AuthenticateAs(TestIds.AssociationAdminA, AppRoles.AssociationAdmin);
    var attach = await associationAdmin.PostAsJsonAsync($"/api/certification-cycles/{cycleId}/association-documents",
        new { documentVersionId = versionId });
    Assert.Equal(HttpStatusCode.Created, attach.StatusCode);
    var submissionId = JsonDocument.Parse(await attach.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

    var submit = await associationAdmin.PostAsync($"/api/certification-cycles/{cycleId}/association-documents/submit", null);
    Assert.Equal(HttpStatusCode.NoContent, submit.StatusCode);

    using var superAdmin = _factory.CreateClient();
    superAdmin.AuthenticateAsSuperAdmin();
    var verify = await superAdmin.PostAsync($"/api/certification-cycles/{cycleId}/association-documents/{submissionId}/verify", null);
    var status = await superAdmin.GetAsync($"/api/certification-cycles/{cycleId}/association-documents");
    var deletePinnedDocument = await associationAdmin.DeleteAsync($"/api/documents/{documentId}");

    Assert.Equal(HttpStatusCode.NoContent, verify.StatusCode);
    Assert.Equal(HttpStatusCode.OK, status.StatusCode);
    Assert.Equal(HttpStatusCode.Conflict, deletePinnedDocument.StatusCode);
    var json = JsonDocument.Parse(await status.Content.ReadAsStringAsync());
    Assert.True(json.RootElement.GetProperty("isSubmittedComplete").GetBoolean());
    Assert.True(json.RootElement.GetProperty("isVerifiedComplete").GetBoolean());
    Assert.Equal(versionId, json.RootElement.GetProperty("requirements")[0].GetProperty("submission").GetProperty("documentVersionId").GetGuid());
  }
}
