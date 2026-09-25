using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Esertifikasi.Api.IntegrationTests.Certification;

public sealed class ParticipantMasterDocumentAttachmentTests : IClassFixture<EsertifikasiWebApplicationFactory>, IAsyncLifetime {
  private readonly EsertifikasiWebApplicationFactory _factory;

  public ParticipantMasterDocumentAttachmentTests(EsertifikasiWebApplicationFactory factory) => _factory = factory;
  public Task InitializeAsync() => _factory.SeedAsync();
  public Task DisposeAsync() => Task.CompletedTask;

  [Fact]
  public async Task PetaniDocumentVersion_CanBeAttachedToParticipantAsCycleDraft() {
    var cycleId = Guid.NewGuid();
    var participantId = Guid.NewGuid();
    Guid sourceVersionId;
    string sourceStorageKey;
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      var source = await db.DocumentVersions.SingleAsync(x => x.DocumentId == TestIds.DocumentA);
      sourceVersionId = source.Id;
      sourceStorageKey = source.StorageKey;
      db.CertificationCycles.Add(new CertificationCycle {
        Id = cycleId, AssociationId = TestIds.AssociationA, Type = CertificationCycleType.Recertification,
        SequenceNumber = 9001, Status = CertificationCycleStatus.Active, CurrentPhase = CertificationPhase.Preparation,
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow), IsCurrent = true, CreatedByUserId = TestIds.SuperAdmin
      });
      db.CertificationParticipants.Add(new CertificationParticipant {
        Id = participantId, CertificationCycleId = cycleId, PetaniId = TestIds.PetaniA,
        PoktanIdSnapshot = TestIds.PoktanA1, EntryPath = ParticipantEntryPath.New,
        Status = ParticipationStatus.Included, StartingStep = CertificationStep.Disclosure
      });
      var disclosureId = Guid.NewGuid();
      db.Disclosures.Add(new Disclosure {
        Id = disclosureId, CertificationCycleId = cycleId, VersionNumber = 1,
        Status = DisclosureStatus.Completed, CreatedByUserId = TestIds.SuperAdmin,
        SubmittedAt = DateTimeOffset.UtcNow, CompletedAt = DateTimeOffset.UtcNow
      });
      db.Set<DisclosureParticipant>().Add(new DisclosureParticipant {
        DisclosureId = disclosureId, CertificationParticipantId = participantId
      });
      await db.SaveChangesAsync();
    }

    using var client = _factory.CreateClient();
    client.AuthenticateAs(TestIds.AssociationAdminA, AppRoles.AssociationAdmin);
    var response = await client.PostAsJsonAsync($"/api/certification/participants/{participantId}/documents/attach",
        new { documentVersionId = sourceVersionId });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    var documentId = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    using var verificationScope = _factory.Services.CreateScope();
    var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var attached = await verificationDb.Documents.Include(x => x.Versions).SingleAsync(x => x.Id == documentId);
    Assert.Equal(participantId, attached.CertificationParticipantId);
    Assert.Equal(TestIds.DocumentTypePetani, attached.DocumentTypeId);
    Assert.Equal(DocumentStatus.Draft, attached.Status);
    Assert.Equal(sourceStorageKey, Assert.Single(attached.Versions).StorageKey);
  }
}
