using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Esertifikasi.Api.IntegrationTests.Certification;

public sealed class AuditFindingWorkflowTests : IClassFixture<EsertifikasiWebApplicationFactory>, IAsyncLifetime {
  private readonly EsertifikasiWebApplicationFactory _factory;

  public AuditFindingWorkflowTests(EsertifikasiWebApplicationFactory factory) => _factory = factory;
  public Task InitializeAsync() => _factory.SeedAsync();
  public Task DisposeAsync() => Task.CompletedTask;

  [Fact]
  public async Task IcsAuditor_CanCreateFindingForOwnPoktan_ButNotAnotherPoktan() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();
    var (_, auditId, poktanX, poktanY) = await SetupPerformedInternalAuditAsync(client);

    var icsAuditorId = Guid.NewGuid();
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      db.IcsAuditorAssignments.Add(new IcsAuditorAssignment { UserId = icsAuditorId, PoktanId = poktanX });
      await db.SaveChangesAsync();
    }

    using var icsClient = _factory.CreateClient();
    icsClient.AuthenticateAs(icsAuditorId, AppRoles.IcsAuditor);

    var ownFinding = await icsClient.PostAsJsonAsync($"/api/certification/audits/{auditId}/findings", new {
      code = "F-1", description = "Temuan poktan sendiri", poktanId = poktanX, severity = FindingSeverity.Minor
    });
    Assert.Equal(HttpStatusCode.Created, ownFinding.StatusCode);

    var otherFinding = await icsClient.PostAsJsonAsync($"/api/certification/audits/{auditId}/findings", new {
      code = "F-2", description = "Temuan poktan lain", poktanId = poktanY, severity = FindingSeverity.Minor
    });
    Assert.Equal(HttpStatusCode.Forbidden, otherFinding.StatusCode);
  }

  [Fact]
  public async Task IcsAuditor_CanViewCurrentCycle_WithManageInternalAuditPermission() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();
    var (associationId, _, poktanX, _) = await SetupPerformedInternalAuditAsync(client);

    var icsAuditorId = Guid.NewGuid();
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      db.IcsAuditorAssignments.Add(new IcsAuditorAssignment { UserId = icsAuditorId, PoktanId = poktanX });
      await db.SaveChangesAsync();
    }

    using var icsClient = _factory.CreateClient();
    icsClient.AuthenticateAs(icsAuditorId, AppRoles.IcsAuditor);

    var response = await icsClient.GetAsync($"/api/associations/{associationId}/current-certification-cycle");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
    Assert.True(json.GetProperty("permissions").GetProperty("canManageInternalAudit").GetBoolean());
    Assert.False(json.GetProperty("permissions").GetProperty("canManageCycle").GetBoolean());

    using var unrelatedClient = _factory.CreateClient();
    unrelatedClient.AuthenticateAs(Guid.NewGuid(), AppRoles.IcsAuditor);
    var forbidden = await unrelatedClient.GetAsync($"/api/associations/{associationId}/current-certification-cycle");
    Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
  }

  [Fact]
  public async Task IcsAuditor_CanSubmitCorrectiveAction_ButCannotClose() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();
    var (associationId, auditId, poktanX, _) = await SetupPerformedInternalAuditAsync(client);

    var icsAuditorId = Guid.NewGuid();
    var associationAdminId = Guid.NewGuid();
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      db.IcsAuditorAssignments.Add(new IcsAuditorAssignment { UserId = icsAuditorId, PoktanId = poktanX });
      db.AssociationAdminAssignments.Add(new AssociationAdminAssignment { UserId = associationAdminId, AssociationId = associationId });
      await db.SaveChangesAsync();
    }

    using var icsClient = _factory.CreateClient();
    icsClient.AuthenticateAs(icsAuditorId, AppRoles.IcsAuditor);

    var createResponse = await icsClient.PostAsJsonAsync($"/api/certification/audits/{auditId}/findings", new {
      code = "F-1", description = "Temuan", poktanId = poktanX, severity = FindingSeverity.Major
    });
    Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
    var findingId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

    var submitResponse = await icsClient.PutAsJsonAsync($"/api/certification/findings/{findingId}", new {
      penyebabAnalisis = "Root cause", corrections = "Perbaikan langsung", correctiveAction = "Tindakan perbaikan", submitForReview = true
    });
    Assert.Equal(HttpStatusCode.NoContent, submitResponse.StatusCode);

    var icsCloseAttempt = await icsClient.PostAsJsonAsync($"/api/certification/findings/{findingId}/close", new {
      correctiveAction = "Tindakan perbaikan", closureEvidence = "Bukti"
    });
    Assert.Equal(HttpStatusCode.Forbidden, icsCloseAttempt.StatusCode);

    using var adminClient = _factory.CreateClient();
    adminClient.AuthenticateAs(associationAdminId, AppRoles.AssociationAdmin);
    var closeResponse = await adminClient.PostAsJsonAsync($"/api/certification/findings/{findingId}/close", new {
      correctiveAction = "Tindakan perbaikan", closureEvidence = "Bukti"
    });
    Assert.Equal(HttpStatusCode.NoContent, closeResponse.StatusCode);

    using var scope2 = _factory.Services.CreateScope();
    var verifyDb = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
    var finding = await verifyDb.AuditFindings.SingleAsync(x => x.Id == findingId);
    Assert.Equal(FindingStatus.Closed, finding.Status);
  }

  [Fact]
  public async Task PoktanAdmin_CanCloseSubmittedFindingForOwnPoktan() {
    using var client = _factory.CreateClient(); client.AuthenticateAsSuperAdmin();
    var (_, auditId, poktanX, _) = await SetupPerformedInternalAuditAsync(client);
    var poktanAdminId = Guid.NewGuid();
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      db.PoktanAdminAssignments.Add(new PoktanAdminAssignment { UserId = poktanAdminId, PoktanId = poktanX });
      await db.SaveChangesAsync();
    }
    var create = await client.PostAsJsonAsync($"/api/certification/audits/{auditId}/findings", new { code = "PA-1", description = "Temuan", poktanId = poktanX, severity = FindingSeverity.Minor });
    var findingId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    await client.PutAsJsonAsync($"/api/certification/findings/{findingId}", new { correctiveAction = "Sudah diperbaiki", submitForReview = true });
    using var admin = _factory.CreateClient(); admin.AuthenticateAs(poktanAdminId, AppRoles.PoktanAdmin);
    Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/certification/findings/{findingId}/close", new { correctiveAction = "Sudah diperbaiki", closureEvidence = "Diverifikasi" })).StatusCode);
  }

  [Fact]
  public async Task InternalAudit_TemplatePreviewAndConfirmedImport_Work() {
    using var client = _factory.CreateClient(); client.AuthenticateAsSuperAdmin();
    var (_, auditId, poktanX, _) = await SetupPerformedInternalAuditAsync(client);
    var template = await client.GetAsync($"/api/certification/audits/{auditId}/findings/template");
    Assert.Equal(HttpStatusCode.OK, template.StatusCode);
    using var form = new MultipartFormDataContent();
    form.Add(new ByteArrayContent(await template.Content.ReadAsByteArrayAsync()), "file", "template.xlsx");
    var preview = await client.PostAsync($"/api/certification/audits/{auditId}/findings/import/preview", form);
    Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
    Assert.True((await preview.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isValid").GetBoolean());
    var import = await client.PostAsJsonAsync($"/api/certification/audits/{auditId}/findings/import", new { rows = new[] { new { code = "BULK-1", poktanId = poktanX, severity = "Major", description = "Temuan bulk", dueDate = "2026-12-31", penyebabAnalisis = "Analisis", corrections = "Koreksi", correctiveAction = "Tindakan" } } });
    Assert.Equal(HttpStatusCode.OK, import.StatusCode);
    Assert.Equal(1, (await import.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("imported").GetInt32());
  }

  [Fact]
  public async Task ExternalAudit_UploadReportThenClosureReport_Succeeds() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();
    var auditId = await SetupPerformedExternalAuditAsync(client);

    var reportUpload = await UploadAsync(client, $"/api/certification/audits/{auditId}/report", TestIds.DocumentTypeAuditReport, true);
    Assert.Equal(HttpStatusCode.Created, reportUpload.StatusCode);

    var closureUpload = await UploadAsync(client, $"/api/certification/audits/{auditId}/closure-report", TestIds.DocumentTypeAuditClosureReport);
    Assert.Equal(HttpStatusCode.Created, closureUpload.StatusCode);
  }

  [Fact]
  public async Task ExternalAudit_UploadClosureReportBeforeReport_ReturnsConflict() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();
    var auditId = await SetupPerformedExternalAuditAsync(client);

    var closureUpload = await UploadAsync(client, $"/api/certification/audits/{auditId}/closure-report", TestIds.DocumentTypeAuditClosureReport);

    Assert.Equal(HttpStatusCode.Conflict, closureUpload.StatusCode);
  }

  [Fact]
  public async Task ExternalAudit_UploadWithWrongDocumentType_ReturnsBadRequest() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();
    var auditId = await SetupPerformedExternalAuditAsync(client);

    var response = await UploadAsync(client, $"/api/certification/audits/{auditId}/report", TestIds.DocumentTypeAuditClosureReport);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task ExternalAudit_ReportWithoutFindings_ClosesAuditAutomatically() {
    using var client = _factory.CreateClient(); client.AuthenticateAsSuperAdmin();
    var auditId = await SetupPerformedExternalAuditAsync(client);
    Assert.Equal(HttpStatusCode.Created, (await UploadAsync(client, $"/api/certification/audits/{auditId}/report", TestIds.DocumentTypeAuditReport, false)).StatusCode);
    using var scope = _factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var audit = await db.CertificationAudits.SingleAsync(x => x.Id == auditId);
    Assert.Equal(AuditStatus.Closed, audit.Status); Assert.NotNull(audit.ClosedAt);
  }

  [Fact]
  public async Task ExternalAudit_WithFindings_ClosesWhenClosureReportIsUploaded() {
    using var client = _factory.CreateClient(); client.AuthenticateAsSuperAdmin();
    var auditId = await SetupPerformedExternalAuditAsync(client);
    Assert.Equal(HttpStatusCode.Created, (await UploadAsync(client, $"/api/certification/audits/{auditId}/report", TestIds.DocumentTypeAuditReport, true)).StatusCode);
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      Assert.Equal(AuditStatus.FindingsOpen, (await db.CertificationAudits.SingleAsync(x => x.Id == auditId)).Status);
    }
    Assert.Equal(HttpStatusCode.Created, (await UploadAsync(client, $"/api/certification/audits/{auditId}/closure-report", TestIds.DocumentTypeAuditClosureReport)).StatusCode);
    using var verifyScope = _factory.Services.CreateScope(); var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
    Assert.Equal(AuditStatus.Closed, (await verifyDb.CertificationAudits.SingleAsync(x => x.Id == auditId)).Status);
  }

  private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string path, Guid documentTypeId, bool? hasFindings = null) {
    using var form = new MultipartFormDataContent();
    form.Add(new StringContent(documentTypeId.ToString()), "documentTypeId");
    if (hasFindings is not null) form.Add(new StringContent(hasFindings.Value.ToString()), "hasFindings");
    form.Add(new ByteArrayContent(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }), "file", "laporan.pdf");
    return await client.PostAsync(path, form);
  }

  private async Task<(Guid AssociationId, Guid AuditId, Guid PoktanX, Guid PoktanY)> SetupPerformedInternalAuditAsync(HttpClient client) {
    var associationId = await CreateAssociationAsync(client);
    Guid cycleId, poktanX = Guid.NewGuid(), poktanY = Guid.NewGuid();
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      cycleId = await db.CertificationCycles.Where(x => x.AssociationId == associationId).Select(x => x.Id).SingleAsync();
      db.Poktan.AddRange(
          new Poktan { Id = poktanX, AssociationId = associationId, Nama = "Poktan X" },
          new Poktan { Id = poktanY, AssociationId = associationId, Nama = "Poktan Y" });
      var cycle = await db.CertificationCycles.SingleAsync(x => x.Id == cycleId);
      cycle.CurrentPhase = CertificationPhase.InternalAudit;
      await db.SaveChangesAsync();
    }

    var createAudit = await client.PostAsJsonAsync($"/api/certification/cycles/{cycleId}/audits", new {
      type = AuditType.Internal, scheduledDate = DateOnly.FromDateTime(DateTime.UtcNow)
    });
    Assert.Equal(HttpStatusCode.Created, createAudit.StatusCode);
    var auditId = (await createAudit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

    var perform = await client.PostAsJsonAsync($"/api/certification/audits/{auditId}/perform", new {
      performedDate = DateOnly.FromDateTime(DateTime.UtcNow), passed = true, notes = "Test"
    });
    Assert.Equal(HttpStatusCode.NoContent, perform.StatusCode);

    return (associationId, auditId, poktanX, poktanY);
  }

  private async Task<Guid> SetupPerformedExternalAuditAsync(HttpClient client) {
    var associationId = await CreateAssociationAsync(client);
    Guid cycleId;
    using (var scope = _factory.Services.CreateScope()) {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      cycleId = await db.CertificationCycles.Where(x => x.AssociationId == associationId).Select(x => x.Id).SingleAsync();
      var cycle = await db.CertificationCycles.SingleAsync(x => x.Id == cycleId);
      cycle.CurrentPhase = CertificationPhase.ExternalAudit;
      db.CertificationAudits.Add(new CertificationAudit {
        Id = Guid.NewGuid(), CertificationCycleId = cycleId, Type = AuditType.Internal, Status = AuditStatus.Closed,
        ScheduledDate = DateOnly.FromDateTime(DateTime.UtcNow), PerformedDate = DateOnly.FromDateTime(DateTime.UtcNow),
        ClosedAt = DateTimeOffset.UtcNow, CreatedByUserId = TestIds.SuperAdmin
      });
      await db.SaveChangesAsync();
    }

    var createAudit = await client.PostAsJsonAsync($"/api/certification/cycles/{cycleId}/audits", new {
      type = AuditType.External, scheduledDate = DateOnly.FromDateTime(DateTime.UtcNow)
    });
    Assert.Equal(HttpStatusCode.Created, createAudit.StatusCode);
    var auditId = (await createAudit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

    return auditId;
  }

  private static async Task<Guid> CreateAssociationAsync(HttpClient client) {
    var response = await client.PostAsJsonAsync("/api/associations", new {
      nama = $"Audit Test {Guid.NewGuid():N}", levelOrganisasi = "Provinsi", jenisOrganisasi = "Koperasi",
      ketuaOrganisasi = "Ketua", bendahara = "Bendahara", sekretarisOrganisasi = "Sekretaris", bidang = "Sertifikasi",
      noTelp = "08123456789", provinceId = TestIds.Province
    });
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
  }
}
