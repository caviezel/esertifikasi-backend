using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Esertifikasi.Api.IntegrationTests.Certification;

public sealed class EndToEndWorkflowTests : IClassFixture<EsertifikasiWebApplicationFactory>, IAsyncLifetime {
  private readonly EsertifikasiWebApplicationFactory _factory;

  public EndToEndWorkflowTests(EsertifikasiWebApplicationFactory factory) => _factory = factory;
  public Task InitializeAsync() => _factory.SeedAsync();
  public Task DisposeAsync() => Task.CompletedTask;

  private async Task TransitionCycleAsync(Guid cycleId, CertificationPhase target) {
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var cycle = await db.CertificationCycles.SingleAsync(x => x.Id == cycleId);
    var prev = cycle.CurrentPhase;
    cycle.CurrentPhase = target;
    if (target == CertificationPhase.Completed) { cycle.Status = CertificationCycleStatus.Completed; cycle.CompletedDate = DateOnly.FromDateTime(DateTime.UtcNow); }
    db.WorkflowTransitions.Add(new WorkflowTransition { Id = Guid.NewGuid(), CertificationCycleId = cycleId, FromPhase = prev, ToPhase = target, UserId = TestIds.SuperAdmin, ChangedAt = DateTimeOffset.UtcNow });
    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task FullWorkflow_DisclosureThroughCompleted_TransitionsAllPhasesAndCreatesNextCycle() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();
    var assocResp = await client.PostAsJsonAsync("/api/associations", AssociationPayload($"E2E {Guid.NewGuid():N}"));
    Assert.Equal(HttpStatusCode.Created, assocResp.StatusCode);
    var associationId = JsonDocument.Parse(await assocResp.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

    Guid petaniId = Guid.NewGuid(), lahanId = Guid.NewGuid(), cycleId;
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      var poktanId = Guid.NewGuid();
      db.Poktan.Add(new Poktan { Id = poktanId, AssociationId = associationId, Nama = "E2E Poktan" });
      db.Petani.Add(new Petani { Id = petaniId, PoktanId = poktanId, Nama = "E2E Petani", Nik = DateTime.UtcNow.Ticks.ToString()[^16..] });
      db.Lahan.Add(new Lahan { Id = lahanId, PetaniId = petaniId, NoLegalitas = "E2E-LAHAN", LuasLegalitas = 5.0m,
        BoundaryGeoJson = ValidMultiPolygon });
      await db.SaveChangesAsync();
      cycleId = await db.CertificationCycles.Where(x => x.AssociationId == associationId).Select(x => x.Id).SingleAsync();
    }

    // Configure training/monitoring targets via API
    await client.PutAsJsonAsync($"/api/certification-cycles/{cycleId}/step-config/{CertificationStep.Training}", new { requiredTarget = 1 });
    await client.PutAsJsonAsync($"/api/certification-cycles/{cycleId}/step-config/{CertificationStep.Monitoring}", new { requiredTarget = 1 });

    // --- Disclosure: assess new Lahan, then enroll and select its scope ---
    foreach (var type in Enum.GetValues<BaselineAssessmentType>()) {
      var blResp = await client.PutAsJsonAsync($"/api/lahan/{lahanId}/baseline", new {
        type, status = ProgressStatusCompleted, result = "OK"
      });
      Assert.Equal(HttpStatusCode.OK, blResp.StatusCode);
    }
    var enrollResp = await client.PostAsJsonAsync($"/api/certification-cycles/{cycleId}/participants", new {
      petaniId = petaniId, entryPath = ParticipantEntryPath.New, lahanIds = new[] { lahanId }
    });
    Assert.Equal(HttpStatusCode.Created, enrollResp.StatusCode);
    var participantId = JsonDocument.Parse(await enrollResp.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

    await client.PostAsJsonAsync($"/api/certification-cycles/participants/{participantId}/status", new { status = ParticipationStatus.Included });

    Guid participantLahanId;
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      participantLahanId = await db.CertificationParticipantLahan.Where(x => x.CertificationParticipantId == participantId).Select(x => x.Id).SingleAsync();
    }
    await client.PostAsJsonAsync($"/api/certification-cycles/participant-lahan/{participantLahanId}/status", new { status = LahanParticipationStatus.Included });
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      db.Documents.AddRange(
        new DocumentRecord {
          Id = Guid.NewGuid(), DocumentTypeId = TestIds.DocumentTypePetani,
          CertificationParticipantId = participantId, UploadedByUserId = TestIds.SuperAdmin,
          Status = DocumentStatus.Verified
        },
        new DocumentRecord {
          Id = Guid.NewGuid(), DocumentTypeId = TestIds.DocumentTypeLahan,
          CertificationParticipantLahanId = participantLahanId, UploadedByUserId = TestIds.SuperAdmin,
          Status = DocumentStatus.Verified
        });
      await db.SaveChangesAsync();
    }

    var discResp = await client.PostAsJsonAsync($"/api/certification/cycles/{cycleId}/disclosures", new { notes = "E2E" });
    Assert.Equal(HttpStatusCode.Created, discResp.StatusCode);
    var disclosureId = JsonDocument.Parse(await discResp.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/certification/disclosures/{disclosureId}/complete", null)).StatusCode);

    // --- Preparation phase ---
    await TransitionCycleAsync(cycleId, CertificationPhase.Preparation);
    var trainResp = await client.PostAsJsonAsync($"/api/certification/cycles/{cycleId}/training", new {
      title = "E2E Training", scheduledAt = DateTimeOffset.UtcNow, petaniIds = new[] { petaniId }
    });
    Assert.Equal(HttpStatusCode.Created, trainResp.StatusCode);
    var sessionId = JsonDocument.Parse(await trainResp.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    await client.PutAsJsonAsync($"/api/certification/training/{sessionId}/attendance/{petaniId}", new { status = AttendanceStatus.Attended });
    Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/certification/training/{sessionId}/complete", null)).StatusCode);
    var monResp = await client.PostAsJsonAsync("/api/certification/monitoring", new {
      petaniId, category = MonitoringCategory.Budidaya,
      monitoringMonth = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1), status = ProgressStatusCompleted
    });
    Assert.Equal(HttpStatusCode.Created, monResp.StatusCode);

    // --- InternalAudit phase ---
    await TransitionCycleAsync(cycleId, CertificationPhase.InternalAudit);
    var intAudit = await client.PostAsJsonAsync($"/api/certification/cycles/{cycleId}/audits", new {
      type = AuditType.Internal, scheduledDate = DateOnly.FromDateTime(DateTime.UtcNow)
    });
    Assert.Equal(HttpStatusCode.Created, intAudit.StatusCode);
    var intAuditId = JsonDocument.Parse(await intAudit.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    await client.PostAsJsonAsync($"/api/certification/audits/{intAuditId}/perform", new {
      performedDate = DateOnly.FromDateTime(DateTime.UtcNow), passed = true, notes = "E2E"
    });
    Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/certification/audits/{intAuditId}/close", null)).StatusCode);

    // --- ExternalAudit phase ---
    await TransitionCycleAsync(cycleId, CertificationPhase.ExternalAudit);
    var extAudit = await client.PostAsJsonAsync($"/api/certification/cycles/{cycleId}/audits", new {
      type = AuditType.External, scheduledDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)
    });
    Assert.Equal(HttpStatusCode.Created, extAudit.StatusCode);
    var extAuditId = JsonDocument.Parse(await extAudit.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    using (var auditReport = new MultipartFormDataContent()) {
      auditReport.Add(new StringContent(TestIds.DocumentTypeAuditReport.ToString()), "documentTypeId");
      auditReport.Add(new StringContent("false"), "hasFindings");
      auditReport.Add(new ByteArrayContent(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }), "file", "audit-eksternal.pdf");
      Assert.Equal(HttpStatusCode.Created,
          (await client.PostAsync($"/api/certification/audits/{extAuditId}/report", auditReport)).StatusCode);
    }

    // --- CertificateIssuance phase ---
    await TransitionCycleAsync(cycleId, CertificationPhase.CertificateIssuance);
    Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(
        $"/api/certification/participants/{participantId}/certificate-eligibility",
        new { status = CertificateEligibilityStatus.Qualified })).StatusCode);
    Guid certDocId;
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      certDocId = Guid.NewGuid();
      db.Documents.Add(new DocumentRecord {
        Id = certDocId, DocumentTypeId = TestIds.DocumentTypeLahan, CertificationCycleId = cycleId,
        UploadedByUserId = TestIds.SuperAdmin, Status = DocumentStatus.Verified
      });
      await db.SaveChangesAsync();
    }
    var certResp = await client.PostAsJsonAsync($"/api/certification/cycles/{cycleId}/certificates", new {
      documentId = certDocId, number = $"E2E-{Guid.NewGuid():N}", certificationBody = "E2E Body",
      issuedDate = DateOnly.FromDateTime(DateTime.UtcNow)
    });
    var certBody2 = await certResp.Content.ReadAsStringAsync(); Assert.True(certResp.StatusCode == HttpStatusCode.Created, certBody2);
    var nextCycleId = JsonDocument.Parse(await certResp.Content.ReadAsStringAsync()).RootElement.GetProperty("nextCycleId").GetGuid();

    // Verify cycle completed and next cycle created
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      var completed = await db.CertificationCycles.SingleAsync(x => x.Id == cycleId);
      Assert.Equal(CertificationPhase.Completed, completed.CurrentPhase);
      Assert.False(completed.IsCurrent);
      var next = await db.CertificationCycles.SingleAsync(x => x.Id == nextCycleId);
      Assert.True(next.IsCurrent);
      Assert.Equal(1, next.SequenceNumber);
    }

    // Verify computed progress returns 100%
    var progressResp = await client.GetAsync($"/api/certification-cycles/{cycleId}/progress");
    Assert.Equal(HttpStatusCode.OK, progressResp.StatusCode);
    var progressJson = JsonDocument.Parse(await progressResp.Content.ReadAsStringAsync());
    Assert.Equal(100m, progressJson.RootElement.GetProperty("percentage").GetDecimal());
  }

  [Fact]
  public async Task CertificateEligibility_ReturnsItemizedMissingDocuments() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();
    var assocResp = await client.PostAsJsonAsync("/api/associations", AssociationPayload($"MissingDocs {Guid.NewGuid():N}"));
    var associationId = JsonDocument.Parse(await assocResp.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

    Guid petaniId = Guid.NewGuid(), lahanId = Guid.NewGuid(), cycleId;
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      var poktanId = Guid.NewGuid();
      db.Poktan.Add(new Poktan { Id = poktanId, AssociationId = associationId, Nama = "MissingDocs Poktan" });
      db.Petani.Add(new Petani { Id = petaniId, PoktanId = poktanId, Nama = "MissingDocs Petani", Nik = DateTime.UtcNow.Ticks.ToString()[^16..] });
      db.Lahan.Add(new Lahan { Id = lahanId, PetaniId = petaniId, NoLegalitas = "MISSING-LAHAN", LuasLegalitas = 3.0m });
      await db.SaveChangesAsync();
      cycleId = await db.CertificationCycles.Where(x => x.AssociationId == associationId).Select(x => x.Id).SingleAsync();
    }

    var enrollResp = await client.PostAsJsonAsync($"/api/certification-cycles/{cycleId}/participants", new {
      petaniId, entryPath = ParticipantEntryPath.New, lahanIds = new[] { lahanId }
    });
    Assert.Equal(HttpStatusCode.Created, enrollResp.StatusCode);
    var participantId = JsonDocument.Parse(await enrollResp.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    await client.PostAsJsonAsync($"/api/certification-cycles/participants/{participantId}/status", new { status = ParticipationStatus.Included });

    Guid participantLahanId;
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      participantLahanId = await db.CertificationParticipantLahan.Where(x => x.CertificationParticipantId == participantId).Select(x => x.Id).SingleAsync();
    }
    await client.PostAsJsonAsync($"/api/certification-cycles/participant-lahan/{participantLahanId}/status", new { status = LahanParticipationStatus.Included });

    // No documents verified yet: expect one missing entry for the participant-owned requirement and one for the lahan-owned requirement.
    var beforeResp = await client.GetAsync($"/api/certification/cycles/{cycleId}/certificate-eligibility");
    Assert.Equal(HttpStatusCode.OK, beforeResp.StatusCode);
    var beforeParticipant = JsonDocument.Parse(await beforeResp.Content.ReadAsStringAsync()).RootElement
        .EnumerateArray().Single(x => x.GetProperty("petaniId").GetGuid() == petaniId);
    var beforeMissing = beforeParticipant.GetProperty("missingDocuments").EnumerateArray().ToList();
    Assert.Equal(2, beforeMissing.Count);
    Assert.Equal(2, beforeParticipant.GetProperty("missingRequiredDocumentCount").GetInt32());

    var petaniEntry = beforeMissing.Single(x => x.GetProperty("ownerType").GetString() == nameof(DocumentOwnerType.CertificationParticipant));
    Assert.Equal(TestIds.DocumentTypePetani, petaniEntry.GetProperty("documentTypeId").GetGuid());
    Assert.Equal("KTP", petaniEntry.GetProperty("documentTypeCode").GetString());
    Assert.Equal("Kartu Tanda Penduduk", petaniEntry.GetProperty("documentTypeName").GetString());
    Assert.Equal(0, petaniEntry.GetProperty("verifiedCount").GetInt32());
    Assert.Equal(1, petaniEntry.GetProperty("requiredCount").GetInt32());
    Assert.Equal(JsonValueKind.Null, petaniEntry.GetProperty("lahanId").ValueKind);
    Assert.Equal(JsonValueKind.Null, petaniEntry.GetProperty("participantLahanId").ValueKind);

    var lahanEntry = beforeMissing.Single(x => x.GetProperty("ownerType").GetString() == nameof(DocumentOwnerType.CertificationParticipantLahan));
    Assert.Equal(lahanId, lahanEntry.GetProperty("lahanId").GetGuid());
    Assert.Equal(participantLahanId, lahanEntry.GetProperty("participantLahanId").GetGuid());
    Assert.Equal(TestIds.DocumentTypeLahan, lahanEntry.GetProperty("documentTypeId").GetGuid());

    // Verify both documents; the itemized list and the legacy count should both drop to zero.
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      db.Documents.AddRange(
          new DocumentRecord { Id = Guid.NewGuid(), DocumentTypeId = TestIds.DocumentTypePetani,
            CertificationParticipantId = participantId, UploadedByUserId = TestIds.SuperAdmin, Status = DocumentStatus.Verified },
          new DocumentRecord { Id = Guid.NewGuid(), DocumentTypeId = TestIds.DocumentTypeLahan,
            CertificationParticipantLahanId = participantLahanId, UploadedByUserId = TestIds.SuperAdmin, Status = DocumentStatus.Verified });
      await db.SaveChangesAsync();
    }

    var afterResp = await client.GetAsync($"/api/certification/cycles/{cycleId}/certificate-eligibility");
    var afterParticipant = JsonDocument.Parse(await afterResp.Content.ReadAsStringAsync()).RootElement
        .EnumerateArray().Single(x => x.GetProperty("petaniId").GetGuid() == petaniId);
    Assert.Empty(afterParticipant.GetProperty("missingDocuments").EnumerateArray());
    Assert.Equal(0, afterParticipant.GetProperty("missingRequiredDocumentCount").GetInt32());
  }

  [Fact]
  public async Task Training_CanRunThroughoutActiveCycle() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();
    var assocResp = await client.PostAsJsonAsync("/api/associations", AssociationPayload($"Guard {Guid.NewGuid():N}"));
    var associationId = JsonDocument.Parse(await assocResp.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    Guid cycleId;
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      cycleId = await db.CertificationCycles.Where(x => x.AssociationId == associationId).Select(x => x.Id).SingleAsync();
    }
    // Training and document work run in parallel throughout the active cycle.
    Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/certification/cycles/{cycleId}/disclosures", new { notes = "allowed" })).StatusCode);
    Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/certification/cycles/{cycleId}/training", new {
      title = "Guard", scheduledAt = DateTimeOffset.UtcNow, petaniIds = Array.Empty<Guid>()
    })).StatusCode);
  }

  [Fact]
  public async Task Training_AllowsAssociationPetaniOutsideCycleScope_AndMonitoringUsesMasterIds() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();
    var assocResp = await client.PostAsJsonAsync("/api/associations", AssociationPayload($"Activities {Guid.NewGuid():N}"));
    var associationId = JsonDocument.Parse(await assocResp.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    Guid cycleId, petaniId = Guid.NewGuid(), lahanId = Guid.NewGuid();
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      var poktanId = Guid.NewGuid();
      db.Poktan.Add(new Poktan { Id = poktanId, AssociationId = associationId, Nama = "Activity Poktan" });
      db.Petani.Add(new Petani { Id = petaniId, PoktanId = poktanId, Nama = "Non participant" });
      db.Lahan.Add(new Lahan { Id = lahanId, PetaniId = petaniId, NoLegalitas = "ACT-1" });
      await db.SaveChangesAsync();
      cycleId = await db.CertificationCycles.Where(x => x.AssociationId == associationId).Select(x => x.Id).SingleAsync();
    }

    var training = await client.PostAsJsonAsync($"/api/certification/cycles/{cycleId}/training", new {
      title = "Open association training", scheduledAt = DateTimeOffset.UtcNow, petaniIds = new[] { petaniId }
    });
    Assert.Equal(HttpStatusCode.Created, training.StatusCode);
    var trainingId = JsonDocument.Parse(await training.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(
        $"/api/certification/training/{trainingId}/attendance/{petaniId}", new { status = AttendanceStatus.Attended })).StatusCode);

    var month = new DateOnly(2026, 9, 1);
    var monitoring = await client.PostAsJsonAsync("/api/certification/monitoring", new {
      petaniId, lahanId, category = MonitoringCategory.Budidaya, monitoringMonth = month, status = ProgressStatusCompleted
    });
    Assert.Equal(HttpStatusCode.Created, monitoring.StatusCode);
    var list = await client.GetAsync($"/api/certification/monitoring?associationId={associationId}&year=2026&month=9");
    Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    var rows = JsonDocument.Parse(await list.Content.ReadAsStringAsync()).RootElement;
    Assert.Contains(rows.EnumerateArray(), x => x.GetProperty("petaniId").GetGuid() == petaniId
        && x.GetProperty("lahanId").GetGuid() == lahanId);
  }

  private const ProgressStatus ProgressStatusCompleted = ProgressStatus.Completed;
  private static object AssociationPayload(string nama) => new {
    nama, levelOrganisasi = "Provinsi", jenisOrganisasi = "Koperasi", ketuaOrganisasi = "Ketua",
    bendahara = "Bendahara", sekretarisOrganisasi = "Sekretaris", bidang = "Sertifikasi",
    noTelp = "08123456789", provinceId = TestIds.Province
  };
  private const string ValidMultiPolygon = "{\"type\":\"MultiPolygon\",\"coordinates\":[[[[0,0],[0,1],[1,1],[1,0],[0,0]]]]}";
}
