using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Esertifikasi.Api.IntegrationTests.Infrastructure;

public sealed class PostgreSqlModelTests {
  [Fact]
  public void ModelCanGeneratePostgreSqlCreateScript() {
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql("Host=localhost;Database=model_validation;Username=test;Password=test")
        .ConfigureWarnings(warnings => warnings.Throw(
            CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning))
        .Options;

    using var db = new AppDbContext(options);

    var script = db.Database.GenerateCreateScript();

    Assert.Contains("CK_Document_ExactlyOneOwner", script);
    Assert.Contains("FK_MemberRegistration_Petani_ExistingPetaniId", script);
    Assert.Contains("FK_MemberRegistration_AspNetUsers_ReviewedByUserId", script);
    Assert.Contains("IX_DocumentType_OwnerType_Code", script);
    Assert.Contains("CertificationCycle", script);
    Assert.Contains("CK_CertificationCycle_Sequence", script);
    Assert.Contains("CertificationParticipantLahan", script);
    Assert.Contains("CertificateParticipant", script);
    Assert.Contains("CertificationCycleId", script);
    Assert.Contains("CREATE TABLE esertifikasi.\"Province\"", script);
    Assert.Contains("CREATE TABLE esertifikasi.\"Village\"", script);
    Assert.Contains("CREATE TABLE esertifikasi.\"RegionDatasetImport\"", script);
    Assert.Contains("CREATE TABLE esertifikasi.\"AssociationDocumentSubmission\"", script);
    Assert.Contains("\"DatasetSha256\" character varying(64) NOT NULL", script);
    Assert.Contains("\"AssociationId\" uuid", script);
    Assert.Contains("\"BoundaryGeoJson\" jsonb", script);
    Assert.Contains("FK_Petani_Village_DesaId", script);
    Assert.Contains("FK_Lahan_Village_DesaId", script);
    Assert.Contains("WHERE \"IsActive\" = true", script);
    Assert.Contains("ON DELETE RESTRICT", script);
  }
}
