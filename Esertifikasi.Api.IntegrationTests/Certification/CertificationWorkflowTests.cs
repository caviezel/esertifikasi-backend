using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Esertifikasi.Api.IntegrationTests.Certification;

public sealed class CertificationWorkflowTests : IClassFixture<EsertifikasiWebApplicationFactory>, IAsyncLifetime {
  private readonly EsertifikasiWebApplicationFactory _factory;

  public CertificationWorkflowTests(EsertifikasiWebApplicationFactory factory) => _factory = factory;
  public Task InitializeAsync() => _factory.SeedAsync();
  public Task DisposeAsync() => Task.CompletedTask;

  [Fact]
  public async Task AssociationCreation_AtomicallyCreatesInitialCurrentCycleAndSteps() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();

    var response = await client.PostAsJsonAsync("/api/associations", AssociationPayload($"Workflow {Guid.NewGuid():N}"));
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    var associationId = body.RootElement.GetProperty("id").GetGuid();
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var cycle = await db.CertificationCycles.Include(x => x.StepProgress).Include(x => x.Transitions)
        .SingleAsync(x => x.AssociationId == associationId);
    Assert.Equal(CertificationCycleType.InitialCertification, cycle.Type);
    Assert.Equal(0, cycle.SequenceNumber);
    Assert.True(cycle.IsCurrent);
    Assert.Equal(Enum.GetValues<CertificationStep>().Length, cycle.StepProgress.Count);
    Assert.Single(cycle.Transitions);
  }

  [Fact]
  public async Task CurrentCertificationCycle_ReturnsNormalizedSidebarState() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();
    var association = await client.PostAsJsonAsync(
        "/api/associations", AssociationPayload($"Sidebar {Guid.NewGuid():N}"));
    var associationId = JsonDocument.Parse(await association.Content.ReadAsStringAsync())
        .RootElement.GetProperty("id").GetGuid();

    var response = await client.GetAsync(
        $"/api/associations/{associationId}/certification-cycles/current");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    Assert.Equal(associationId, body.GetProperty("cycle").GetProperty("associationId").GetGuid());
    Assert.Equal("InitialCertification", body.GetProperty("cycle").GetProperty("type").GetString());
    Assert.Equal("Disclosure", body.GetProperty("cycle").GetProperty("currentPhase").GetString());
    Assert.Equal(0m, body.GetProperty("progress").GetProperty("percentage").GetDecimal());
    Assert.True(body.GetProperty("permissions").GetProperty("canManageCycle").GetBoolean());

    var pages = body.GetProperty("pages").EnumerateArray().ToList();
    Assert.DoesNotContain(pages, x => x.GetProperty("key").GetString() == "participants-and-land");
    Assert.DoesNotContain(pages, x => x.GetProperty("key").GetString() == "land-mapping");
    var disclosure = Assert.Single(pages, x => x.GetProperty("key").GetString() == "disclosure");
    Assert.True(disclosure.GetProperty("isAvailable").GetBoolean());
    Assert.True(disclosure.GetProperty("isCurrent").GetBoolean());
    Assert.True(disclosure.GetProperty("canEdit").GetBoolean());

    var verification = await client.GetAsync(
        $"/api/certification-cycles/{body.GetProperty("cycle").GetProperty("id").GetGuid()}/document-verification");
    Assert.Equal(HttpStatusCode.OK, verification.StatusCode);
    Assert.True(JsonDocument.Parse(await verification.Content.ReadAsStringAsync()).RootElement
        .TryGetProperty("summary", out _));
  }

  [Fact]
  public async Task BaselineReadiness_ReturnsEmptySummaryWhenAssociationHasNoLahan() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();
    var association = await client.PostAsJsonAsync(
        "/api/associations", AssociationPayload($"Empty baseline {Guid.NewGuid():N}"));
    var associationId = JsonDocument.Parse(await association.Content.ReadAsStringAsync())
        .RootElement.GetProperty("id").GetGuid();

    var response = await client.GetAsync($"/api/associations/{associationId}/baseline-readiness");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    Assert.Equal(0, body.GetProperty("totalLahan").GetInt32());
    Assert.Empty(body.GetProperty("lahan").EnumerateArray());
  }

  [Fact]
  public async Task BaselineReadiness_ReturnsNotAssessedWhenLahanHasNoAssessment() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();
    var association = await client.PostAsJsonAsync(
        "/api/associations", AssociationPayload($"Unassessed baseline {Guid.NewGuid():N}"));
    var associationId = JsonDocument.Parse(await association.Content.ReadAsStringAsync())
        .RootElement.GetProperty("id").GetGuid();
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      var poktanId = Guid.NewGuid();
      var petaniId = Guid.NewGuid();
      db.Poktan.Add(new Poktan { Id = poktanId, AssociationId = associationId, Nama = "Empty baseline Poktan" });
      db.Petani.Add(new Petani { Id = petaniId, PoktanId = poktanId, Nama = "Empty baseline Petani" });
      db.Lahan.Add(new Lahan { Id = Guid.NewGuid(), PetaniId = petaniId, NoLegalitas = "NO-BASELINE" });
      await db.SaveChangesAsync();
    }

    var response = await client.GetAsync($"/api/associations/{associationId}/baseline-readiness");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    Assert.Equal(1, body.GetProperty("totalLahan").GetInt32());
    Assert.Equal("NotAssessed", body.GetProperty("lahan")[0].GetProperty("summaryStatus").GetString());
  }

  [Fact]
  public async Task HistoricalCycle_MutationReturnsStructuredConflict() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();
    var association = await client.PostAsJsonAsync(
        "/api/associations", AssociationPayload($"Historical {Guid.NewGuid():N}"));
    var associationId = JsonDocument.Parse(await association.Content.ReadAsStringAsync())
        .RootElement.GetProperty("id").GetGuid();
    Guid cycleId;
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      var cycle = await db.CertificationCycles.SingleAsync(x => x.AssociationId == associationId);
      cycle.IsCurrent = false;
      cycleId = cycle.Id;
      await db.SaveChangesAsync();
    }

    var response = await client.PostAsJsonAsync($"/api/certification-cycles/{cycleId}/transition",
        new { targetPhase = "Preparation" });

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    Assert.Equal("CERTIFICATION_CYCLE_READ_ONLY", body.GetProperty("code").GetString());
  }

  [Fact]
  public async Task Readiness_ReportsBlockersAndTransitionCannotBypassThem() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();
    var association = await client.PostAsJsonAsync("/api/associations", AssociationPayload($"Readiness {Guid.NewGuid():N}"));
    var associationId = JsonDocument.Parse(await association.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    Guid cycleId;
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      cycleId = await db.CertificationCycles.Where(x => x.AssociationId == associationId).Select(x => x.Id).SingleAsync();
    }

    var readiness = await client.GetAsync($"/api/certification-cycles/{cycleId}/readiness");
    var transition = await client.PostAsJsonAsync($"/api/certification-cycles/{cycleId}/transition", new {
      targetPhase = CertificationPhase.Preparation
    });

    Assert.Equal(HttpStatusCode.OK, readiness.StatusCode);
    Assert.Equal(HttpStatusCode.Conflict, transition.StatusCode);
    var readinessJson = JsonDocument.Parse(await readiness.Content.ReadAsStringAsync());
    var transitionJson = JsonDocument.Parse(await transition.Content.ReadAsStringAsync());
    Assert.False(readinessJson.RootElement.GetProperty("canTransition").GetBoolean());
    Assert.Contains(readinessJson.RootElement.GetProperty("blockers").EnumerateArray(),
        x => x.GetProperty("code").GetString() == "DISCLOSURE_INCOMPLETE");
    Assert.Contains(transitionJson.RootElement.GetProperty("blockers").EnumerateArray(),
        x => x.GetProperty("code").GetString() == "DISCLOSURE_INCOMPLETE");
  }

  [Fact]
  public async Task MasterPetaniAndLahanDocuments_SatisfyCycleReadinessAndVerification() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();
    var cycleId = Guid.NewGuid();
    var participantId = Guid.NewGuid();
    var participantLahanId = Guid.NewGuid();
    var disclosureId = Guid.NewGuid();
    var lahanDocumentId = Guid.NewGuid();

    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      var cycle = new CertificationCycle {
        Id = cycleId, AssociationId = TestIds.AssociationA, Type = CertificationCycleType.InitialCertification,
        Status = CertificationCycleStatus.Active, CurrentPhase = CertificationPhase.Preparation,
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow), IsCurrent = true, CreatedByUserId = TestIds.SuperAdmin
      };
      cycle.DocumentRequirements.Add(new CycleDocumentRequirement {
        Id = Guid.NewGuid(), DocumentTypeId = TestIds.DocumentTypePetani,
        OwnerType = DocumentOwnerType.CertificationParticipant, IsRequired = true, RequiredCount = 1
      });
      cycle.DocumentRequirements.Add(new CycleDocumentRequirement {
        Id = Guid.NewGuid(), DocumentTypeId = TestIds.DocumentTypeLahan,
        OwnerType = DocumentOwnerType.CertificationParticipantLahan, IsRequired = true, RequiredCount = 1
      });
      foreach (var step in Enum.GetValues<CertificationStep>()) cycle.StepProgress.Add(new CycleStepProgress {
        Id = Guid.NewGuid(), Step = step,
        RequiredTarget = step is CertificationStep.Training or CertificationStep.Monitoring ? 0 : null
      });
      var participant = new CertificationParticipant {
        Id = participantId, CertificationCycle = cycle, PetaniId = TestIds.PetaniA,
        PoktanIdSnapshot = TestIds.PoktanA1, Status = ParticipationStatus.Included
      };
      var participantLahan = new CertificationParticipantLahan {
        Id = participantLahanId, CertificationParticipant = participant, LahanId = TestIds.LahanA,
        Status = LahanParticipationStatus.Included
      };
      var disclosure = new Disclosure {
        Id = disclosureId, CertificationCycle = cycle, VersionNumber = 1, Status = DisclosureStatus.Completed,
        CreatedByUserId = TestIds.SuperAdmin, CompletedAt = DateTimeOffset.UtcNow
      };
      disclosure.Participants.Add(new DisclosureParticipant { CertificationParticipant = participant });
      disclosure.Lahan.Add(new DisclosureLahan { CertificationParticipantLahan = participantLahan });
      db.AddRange(cycle, participant, participantLahan, disclosure);
      db.Documents.Add(new DocumentRecord {
        Id = lahanDocumentId, DocumentTypeId = TestIds.DocumentTypeLahan, LahanId = TestIds.LahanA,
        UploadedByUserId = TestIds.SuperAdmin, Status = DocumentStatus.Verified
      });
      db.DocumentVersions.Add(new DocumentVersion {
        Id = Guid.NewGuid(), DocumentId = lahanDocumentId, VersionNumber = 1, StorageKey = "test/master-lahan.pdf",
        OriginalFileName = "master-lahan.pdf", ContentType = "application/pdf", FileExtension = ".pdf",
        FileSize = 10, Sha256Hash = new string('B', 64), UploadedByUserId = TestIds.SuperAdmin
      });
      await db.SaveChangesAsync();
    }

    var readiness = await client.GetAsync($"/api/certification-cycles/{cycleId}/readiness");
    var summary = await client.GetAsync($"/api/certification-cycles/{cycleId}/document-readiness");
    var verification = await client.GetAsync($"/api/certification-cycles/{cycleId}/document-verification");

    Assert.Equal(HttpStatusCode.OK, readiness.StatusCode);
    Assert.True(JsonDocument.Parse(await readiness.Content.ReadAsStringAsync()).RootElement
        .GetProperty("canTransition").GetBoolean());
    var summaryJson = JsonDocument.Parse(await summary.Content.ReadAsStringAsync()).RootElement;
    Assert.Equal(1, summaryJson.GetProperty("participants")[0].GetProperty("verified").GetInt32());
    Assert.Equal(1, summaryJson.GetProperty("lahan")[0].GetProperty("verified").GetInt32());
    var verificationJson = JsonDocument.Parse(await verification.Content.ReadAsStringAsync()).RootElement;
    Assert.Equal(2, verificationJson.GetProperty("summary").GetProperty("verified").GetInt32());
    Assert.Equal(0, verificationJson.GetProperty("summary").GetProperty("missing").GetInt32());

    var associationTypeId = Guid.NewGuid();
    var associationDocumentId = Guid.NewGuid();
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      db.DocumentTypes.Add(new DocumentType {
        Id = associationTypeId, Code = $"ASSOC_{Guid.NewGuid():N}", Nama = "Master Association",
        OwnerType = DocumentOwnerType.Association, IsRequired = true
      });
      db.CycleDocumentRequirements.Add(new CycleDocumentRequirement {
        Id = Guid.NewGuid(), CertificationCycleId = cycleId, DocumentTypeId = associationTypeId,
        OwnerType = DocumentOwnerType.Association, IsRequired = true, RequiredCount = 1
      });
      db.Documents.Add(new DocumentRecord {
        Id = associationDocumentId, DocumentTypeId = associationTypeId, AssociationId = TestIds.AssociationA,
        UploadedByUserId = TestIds.SuperAdmin, Status = DocumentStatus.Verified
      });
      db.DocumentVersions.Add(new DocumentVersion {
        Id = Guid.NewGuid(), DocumentId = associationDocumentId, VersionNumber = 1,
        StorageKey = "test/master-association.pdf", OriginalFileName = "master-association.pdf",
        ContentType = "application/pdf", FileExtension = ".pdf", FileSize = 10,
        Sha256Hash = new string('C', 64), UploadedByUserId = TestIds.SuperAdmin
      });
      await db.SaveChangesAsync();
    }

    var readinessWithAssociation = await client.GetAsync($"/api/certification-cycles/{cycleId}/readiness");
    var progress = await client.GetAsync($"/api/certification-cycles/{cycleId}/progress");
    var verificationWithAssociation = await client.GetAsync($"/api/certification-cycles/{cycleId}/document-verification");
    Assert.True(JsonDocument.Parse(await readinessWithAssociation.Content.ReadAsStringAsync()).RootElement
        .GetProperty("canTransition").GetBoolean());
    Assert.Equal(HttpStatusCode.OK, progress.StatusCode);
    var documentStep = JsonDocument.Parse(await progress.Content.ReadAsStringAsync()).RootElement
        .GetProperty("steps").EnumerateArray().Single(x =>
            x.GetProperty("step").GetString() == nameof(CertificationStep.DatabaseAndDocumentManagement));
    Assert.Equal(3, documentStep.GetProperty("applicable").GetInt32());
    Assert.Equal(3, documentStep.GetProperty("completed").GetInt32());
    Assert.Equal(100m, documentStep.GetProperty("percentage").GetDecimal());
    Assert.Equal(3, JsonDocument.Parse(await verificationWithAssociation.Content.ReadAsStringAsync()).RootElement
        .GetProperty("summary").GetProperty("verified").GetInt32());
  }

  [Fact]
  public async Task CertificationEnums_AcceptAndReturnStringValues() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();
    var association = await client.PostAsJsonAsync("/api/associations", AssociationPayload($"Enum contract {Guid.NewGuid():N}"));
    var associationId = JsonDocument.Parse(await association.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

    Guid cycleId;
    var petaniId = Guid.NewGuid();
    var lahanId = Guid.NewGuid();
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      var poktanId = Guid.NewGuid();
      db.Poktan.Add(new Poktan { Id = poktanId, AssociationId = associationId, Nama = "Enum contract Poktan" });
      db.Petani.Add(new Petani { Id = petaniId, PoktanId = poktanId, Nama = "Enum contract Petani", Nik = DateTime.UtcNow.Ticks.ToString()[^16..] });
      db.Lahan.Add(new Lahan { Id = lahanId, PetaniId = petaniId, NoLegalitas = $"ENUM-{Guid.NewGuid():N}" });
      await db.SaveChangesAsync();
      cycleId = await db.CertificationCycles.Where(x => x.AssociationId == associationId).Select(x => x.Id).SingleAsync();
    }

    var cycleResponse = await client.GetAsync($"/api/certification-cycles/{cycleId}");
    Assert.Equal(HttpStatusCode.OK, cycleResponse.StatusCode);
    var cycleJson = JsonDocument.Parse(await cycleResponse.Content.ReadAsStringAsync()).RootElement;
    Assert.Equal("InitialCertification", cycleJson.GetProperty("type").GetString());
    Assert.Equal("Active", cycleJson.GetProperty("status").GetString());
    Assert.Equal("Disclosure", cycleJson.GetProperty("currentPhase").GetString());

    var enrollment = await client.PostAsJsonAsync($"/api/certification-cycles/{cycleId}/participants", new {
      petaniId,
      entryPath = "New",
      lahanIds = new[] { lahanId }
    });
    Assert.Equal(HttpStatusCode.Created, enrollment.StatusCode);
    var participantId = JsonDocument.Parse(await enrollment.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    await client.PostAsJsonAsync($"/api/certification-cycles/participants/{participantId}/status", new { status = "Included" });

    Guid participantLahanId;
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      participantLahanId = await db.CertificationParticipantLahan
          .Where(x => x.CertificationParticipantId == participantId).Select(x => x.Id).SingleAsync();
    }
    await client.PostAsJsonAsync($"/api/certification-cycles/participant-lahan/{participantLahanId}/status", new { status = "Included" });

    var disclosure = await client.PostAsJsonAsync($"/api/certification/cycles/{cycleId}/disclosures", new { });
    Assert.Equal(HttpStatusCode.Created, disclosure.StatusCode);
  }

  [Fact]
  public async Task ApprovedSecondDisclosure_AllowsLateLahanPreparationAndBlocksCompletionUntilReady() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();
    var association = await client.PostAsJsonAsync("/api/associations", AssociationPayload($"Late scope {Guid.NewGuid():N}"));
    var associationId = JsonDocument.Parse(await association.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

    var petaniId = Guid.NewGuid();
    var lahanId = Guid.NewGuid();
    Guid cycleId;
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      var poktanId = Guid.NewGuid();
      db.Poktan.Add(new Poktan { Id = poktanId, AssociationId = associationId, Nama = "Late scope Poktan" });
      db.Petani.Add(new Petani { Id = petaniId, PoktanId = poktanId, Nama = "Late scope Petani", Nik = DateTime.UtcNow.Ticks.ToString()[^16..] });
      db.Lahan.Add(new Lahan { Id = lahanId, PetaniId = petaniId, NoLegalitas = $"LATE-{Guid.NewGuid():N}",
        BoundaryGeoJson = "{\"type\":\"MultiPolygon\",\"coordinates\":[[[[0,0],[0,1],[1,1],[1,0],[0,0]]]]}" });
      var cycle = await db.CertificationCycles.SingleAsync(x => x.AssociationId == associationId);
      cycle.CurrentPhase = CertificationPhase.Preparation;
      db.Disclosures.Add(new Disclosure {
        Id = Guid.NewGuid(), CertificationCycleId = cycle.Id, VersionNumber = 1,
        Status = DisclosureStatus.Completed, CreatedByUserId = TestIds.SuperAdmin,
        SubmittedAt = DateTimeOffset.UtcNow, CompletedAt = DateTimeOffset.UtcNow
      });
      await db.SaveChangesAsync();
      cycleId = cycle.Id;
    }

    var second = await client.PostAsJsonAsync($"/api/certification/cycles/{cycleId}/disclosures", new { notes = "Add late scope" });
    Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    var disclosureId = JsonDocument.Parse(await second.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/certification/disclosures/{disclosureId}/review",
        new { approve = true, notes = "Approved" })).StatusCode);

    var enrollment = await client.PostAsJsonAsync($"/api/certification-cycles/{cycleId}/participants", new {
      petaniId, entryPath = "New", lahanIds = new[] { lahanId }
    });
    Assert.Equal(HttpStatusCode.Created, enrollment.StatusCode);
    var participantId = JsonDocument.Parse(await enrollment.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    Guid participantLahanId;
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      participantLahanId = await db.CertificationParticipantLahan.Where(x => x.CertificationParticipantId == participantId)
          .Select(x => x.Id).SingleAsync();
    }

    var firstBaseline = await client.PutAsJsonAsync($"/api/lahan/{lahanId}/baseline", new {
      type = "Nkt", status = "Completed", result = "OK"
    });
    Assert.Equal(HttpStatusCode.OK, firstBaseline.StatusCode);
    Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/certification/disclosures/{disclosureId}/complete", null)).StatusCode);

    foreach (var type in Enum.GetNames<BaselineAssessmentType>()) {
      var baseline = await client.PutAsJsonAsync($"/api/lahan/{lahanId}/baseline", new {
        type, status = "Completed", result = "OK"
      });
      Assert.Equal(HttpStatusCode.OK, baseline.StatusCode);
    }

    var baselineSummary = await client.GetAsync($"/api/associations/{associationId}/baseline-readiness");
    Assert.Equal(HttpStatusCode.OK, baselineSummary.StatusCode);
    var baselineSummaryJson = JsonDocument.Parse(await baselineSummary.Content.ReadAsStringAsync()).RootElement;
    Assert.Equal(1, baselineSummaryJson.GetProperty("readyLahan").GetInt32());
    Assert.Equal("Complete", baselineSummaryJson.GetProperty("lahan")[0].GetProperty("summaryStatus").GetString());

    Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
        $"/api/certification-cycles/participants/{participantId}/status", new { status = "Included" })).StatusCode);
    Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(
        $"/api/certification/disclosures/{disclosureId}/lahan/{participantLahanId}", new { status = "Included" })).StatusCode);

    Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/certification/disclosures/{disclosureId}/complete", null)).StatusCode);
    var disclosureDetail = await client.GetAsync($"/api/certification/disclosures/{disclosureId}");
    Assert.Equal(HttpStatusCode.OK, disclosureDetail.StatusCode);
    var disclosureJson = JsonDocument.Parse(await disclosureDetail.Content.ReadAsStringAsync()).RootElement;
    Assert.True(disclosureJson.GetProperty("isSnapshotFinal").GetBoolean());
    Assert.Single(disclosureJson.GetProperty("participants").EnumerateArray());
    Assert.Single(disclosureJson.GetProperty("lahan").EnumerateArray());

    var database = await client.GetAsync($"/api/associations/{associationId}/certification-database?search=Late%20scope");
    Assert.Equal(HttpStatusCode.OK, database.StatusCode);
    var databaseJson = JsonDocument.Parse(await database.Content.ReadAsStringAsync()).RootElement;
    Assert.Equal(1, databaseJson.GetProperty("totalCount").GetInt32());
    Assert.Equal(lahanId, databaseJson.GetProperty("items")[0].GetProperty("lahan")[0].GetProperty("lahanId").GetGuid());

    Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(
        $"/api/certification-cycles/participant-lahan/{participantLahanId}/status",
        new { status = "Excluded", reason = "Attempted bypass" })).StatusCode);
    Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(
        $"/api/certification-cycles/participants/{participantId}/status",
        new { status = "Excluded", reason = "Attempted bypass" })).StatusCode);
    var readiness = await client.GetAsync($"/api/certification-cycles/{cycleId}/readiness");
    var readinessJson = JsonDocument.Parse(await readiness.Content.ReadAsStringAsync()).RootElement;
    Assert.DoesNotContain(readinessJson.GetProperty("blockers").EnumerateArray(),
        x => x.GetProperty("code").GetString() == "DISCLOSURE_INCOMPLETE");

    var training = await client.PostAsJsonAsync($"/api/certification/cycles/{cycleId}/training", new {
      title = "Preparation lifecycle", scheduledAt = DateTimeOffset.UtcNow, petaniIds = new[] { petaniId }
    });
    Assert.Equal(HttpStatusCode.Created, training.StatusCode);
    var trainingId = JsonDocument.Parse(await training.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/certification/training/{trainingId}/complete", null)).StatusCode);
    Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(
        $"/api/certification/training/{trainingId}/attendance/{petaniId}", new { status = "Attended" })).StatusCode);
    Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/certification/training/{trainingId}/complete", null)).StatusCode);
    Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(
        $"/api/certification/training/{trainingId}/attendance/{petaniId}", new { status = "Absent" })).StatusCode);
    Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/certification/training/{trainingId}/reopen",
        new { reason = "Correct attendance" })).StatusCode);

    var month = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
    var monitoring = await client.PostAsJsonAsync("/api/certification/monitoring", new {
      petaniId, lahanId, category = "Budidaya", monitoringMonth = month, status = "InProgress"
    });
    Assert.Equal(HttpStatusCode.Created, monitoring.StatusCode);
    var monitoringId = JsonDocument.Parse(await monitoring.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/certification/monitoring", new {
      petaniId, lahanId, category = "Budidaya", monitoringMonth = month, status = "Completed"
    })).StatusCode);
    Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/certification/monitoring/{monitoringId}", new {
      lahanId, category = "Budidaya", monitoringMonth = month, status = "Completed", notes = "Final"
    })).StatusCode);
    Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/certification/monitoring/{monitoringId}")).StatusCode);

    Assert.Equal(HttpStatusCode.OK,
        (await client.GetAsync($"/api/certification-cycles/{cycleId}/document-readiness")).StatusCode);
  }

  [Fact]
  public async Task DisclosureWorkspace_CanCreateDraftAndBulkIncludeThenExcludeLahan() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();
    var association = await client.PostAsJsonAsync(
        "/api/associations", AssociationPayload($"Disclosure workspace {Guid.NewGuid():N}"));
    var associationId = JsonDocument.Parse(await association.Content.ReadAsStringAsync())
        .RootElement.GetProperty("id").GetGuid();
    var poktanId = Guid.NewGuid();
    var petaniId = Guid.NewGuid();
    var lahanId = Guid.NewGuid();
    Guid cycleId;
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      cycleId = await db.CertificationCycles.Where(x => x.AssociationId == associationId)
          .Select(x => x.Id).SingleAsync();
      db.Poktan.Add(new Poktan { Id = poktanId, AssociationId = associationId, Nama = "Workspace Poktan" });
      db.Petani.Add(new Petani { Id = petaniId, PoktanId = poktanId, Nama = "Workspace Petani" });
      db.Lahan.Add(new Lahan {
        Id = lahanId, PetaniId = petaniId, NoLegalitas = "WORKSPACE-001",
        BoundaryGeoJson = "{\"type\":\"Polygon\",\"coordinates\":[]}"
      });
      db.BaselineAssessments.AddRange(Enum.GetValues<BaselineAssessmentType>().Select(type =>
          new BaselineAssessment { Id = Guid.NewGuid(), LahanId = lahanId, Type = type, Status = ProgressStatus.Completed }));
      await db.SaveChangesAsync();
    }

    var initial = await client.GetAsync($"/api/certification/cycles/{cycleId}/disclosure-workspace?page=1&pageSize=20");
    Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
    var initialJson = JsonDocument.Parse(await initial.Content.ReadAsStringAsync()).RootElement;
    Assert.Equal(JsonValueKind.Null, initialJson.GetProperty("disclosure").GetProperty("id").ValueKind);
    Assert.Equal(1, initialJson.GetProperty("summary").GetProperty("totalLahan").GetInt32());
    Assert.True(initialJson.GetProperty("items")[0].GetProperty("eligible").GetBoolean());

    var include = await client.PostAsJsonAsync(
        $"/api/certification/cycles/{cycleId}/disclosure-workspace/lahan/include", new { lahanIds = new[] { lahanId } });
    Assert.Equal(HttpStatusCode.OK, include.StatusCode);
    var includeJson = JsonDocument.Parse(await include.Content.ReadAsStringAsync()).RootElement;
    var disclosureId = includeJson.GetProperty("disclosure").GetProperty("id").GetGuid();
    Assert.Equal("Included", includeJson.GetProperty("affectedRows")[0].GetProperty("lahanStatus").GetString());
    Assert.Equal(1, includeJson.GetProperty("summary").GetProperty("selectedLahan").GetInt32());

    var exclude = await client.PostAsJsonAsync($"/api/certification/disclosures/{disclosureId}/lahan/exclude",
        new { lahanIds = new[] { lahanId }, reason = "Not selected" });
    Assert.Equal(HttpStatusCode.OK, exclude.StatusCode);
    var excludeJson = JsonDocument.Parse(await exclude.Content.ReadAsStringAsync()).RootElement;
    Assert.Equal("Excluded", excludeJson.GetProperty("affectedRows")[0].GetProperty("lahanStatus").GetString());
    Assert.Equal(0, excludeJson.GetProperty("summary").GetProperty("selectedLahan").GetInt32());
  }

  [Fact]
  public async Task AssociationAdmin_CanEnrollOwnParticipant_AndMemberCanReadOwnParticipation() {
    Guid cycleId;
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      var existing = await db.CertificationCycles.SingleOrDefaultAsync(x => x.AssociationId == TestIds.AssociationA && x.IsCurrent);
      if (existing is null) {
        existing = new CertificationCycle {
          Id = Guid.NewGuid(), AssociationId = TestIds.AssociationA, Type = CertificationCycleType.InitialCertification,
          SequenceNumber = 0, Status = CertificationCycleStatus.Active, CurrentPhase = CertificationPhase.Disclosure,
          StartDate = DateOnly.FromDateTime(DateTime.UtcNow), IsCurrent = true, CreatedByUserId = TestIds.SuperAdmin
        };
        foreach (var step in Enum.GetValues<CertificationStep>()) existing.StepProgress.Add(new CycleStepProgress { Id = Guid.NewGuid(), Step = step });
        db.CertificationCycles.Add(existing);
        await db.SaveChangesAsync();
      }
      cycleId = existing.Id;
    }
    using var admin = _factory.CreateClient();
    admin.AuthenticateAs(TestIds.AssociationAdminA, AppRoles.AssociationAdmin);
    var enrollment = await admin.PostAsJsonAsync($"/api/certification-cycles/{cycleId}/participants", new {
      petaniId = TestIds.PetaniA, entryPath = ParticipantEntryPath.New, lahanIds = new[] { TestIds.LahanA }
    });
    Assert.True(enrollment.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict);

    using var member = _factory.CreateClient();
    member.AuthenticateAs(TestIds.MemberA, AppRoles.MemberTaniBaik);
    var participants = await member.GetAsync($"/api/certification-cycles/{cycleId}/participants");
    Assert.Equal(HttpStatusCode.OK, participants.StatusCode);
    Assert.Contains(TestIds.PetaniA.ToString(), await participants.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
  }

  private static object AssociationPayload(string nama) => new {
    nama, levelOrganisasi = "Provinsi", jenisOrganisasi = "Koperasi", ketuaOrganisasi = "Ketua",
    bendahara = "Bendahara", sekretarisOrganisasi = "Sekretaris", bidang = "Sertifikasi",
    noTelp = "08123456789", provinceId = TestIds.Province
  };
}
