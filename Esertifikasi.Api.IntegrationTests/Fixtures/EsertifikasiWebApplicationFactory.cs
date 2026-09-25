using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Services.Documents;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Esertifikasi.Api.IntegrationTests.Fixtures;

public sealed class EsertifikasiWebApplicationFactory : WebApplicationFactory<Program> {
  private readonly string _databaseName = $"esertifikasi-tests-{Guid.NewGuid():N}";
  private readonly SemaphoreSlim _seedLock = new(1, 1);
  private bool _seeded;

  protected override void ConfigureWebHost(IWebHostBuilder builder) {
    builder.UseEnvironment("IntegrationTest");
    builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Database=integration-tests");
    builder.UseSetting("Jwt:Key", "integration-test-signing-key-at-least-32-characters");

    builder.ConfigureTestServices(services => {
      services.RemoveAll<DbContextOptions<AppDbContext>>();
      services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));
      services.RemoveAll<IFileStorage>();
      services.AddSingleton<IFileStorage, InMemoryFileStorage>();
      services.AddAuthentication(options => {
        options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
        options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
        options.DefaultForbidScheme = TestAuthenticationHandler.SchemeName;
      }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });
    });
  }

  public async Task SeedAsync() {
    await _seedLock.WaitAsync();
    try {
      if (_seeded) return;
      using var scope = Services.CreateScope();
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      await db.Database.EnsureDeletedAsync();
      await db.Database.EnsureCreatedAsync();

      var roles = AppRoles.All.ToDictionary(name => name, name => new IdentityRole<Guid> {
        Id = Guid.NewGuid(), Name = name, NormalizedName = name.ToUpperInvariant()
      });
      db.Roles.AddRange(roles.Values);

      var users = new[] {
        User(TestIds.SuperAdmin, "superadmin@test.local"),
        User(TestIds.AssociationAdminA, "association-a@test.local"),
        User(TestIds.PoktanAdminA1, "poktan-a1@test.local"),
        User(TestIds.MemberA, "member-a@test.local"),
        User(TestIds.MemberB, "member-b@test.local")
      };
      db.Users.AddRange(users);
      db.UserRoles.AddRange(
          new IdentityUserRole<Guid> { UserId = TestIds.SuperAdmin, RoleId = roles[AppRoles.SuperAdmin].Id },
          new IdentityUserRole<Guid> { UserId = TestIds.AssociationAdminA, RoleId = roles[AppRoles.AssociationAdmin].Id },
          new IdentityUserRole<Guid> { UserId = TestIds.PoktanAdminA1, RoleId = roles[AppRoles.PoktanAdmin].Id },
          new IdentityUserRole<Guid> { UserId = TestIds.MemberA, RoleId = roles[AppRoles.MemberTaniBaik].Id },
          new IdentityUserRole<Guid> { UserId = TestIds.MemberB, RoleId = roles[AppRoles.MemberTaniBaik].Id });
      var regionNow = DateTimeOffset.UtcNow;
      db.Provinces.Add(new Province { Id = TestIds.Province, Code = "98", Name = "PROVINSI FIXTURE", SourceUpdatedAt = regionNow, SyncedAt = regionNow });
      db.Regencies.Add(new Regency { Id = TestIds.Regency, Code = "98.01", ProvinceId = TestIds.Province, Name = "KABUPATEN FIXTURE", SourceUpdatedAt = regionNow, SyncedAt = regionNow });
      db.Districts.Add(new District { Id = TestIds.District, Code = "98.01.01", RegencyId = TestIds.Regency, Name = "KECAMATAN FIXTURE", SourceUpdatedAt = regionNow, SyncedAt = regionNow });
      db.Villages.Add(new Village { Id = TestIds.Village, Code = "98.01.01.0001", DistrictId = TestIds.District, Name = "DESA FIXTURE", SourceUpdatedAt = regionNow, SyncedAt = regionNow });
      db.Associations.AddRange(
          new Association { Id = TestIds.AssociationA, Nama = "Association A" },
          new Association { Id = TestIds.AssociationB, Nama = "Association B" });
      db.Poktan.AddRange(
          new Poktan { Id = TestIds.PoktanA1, AssociationId = TestIds.AssociationA, Nama = "Poktan A1" },
          new Poktan { Id = TestIds.PoktanADelete, AssociationId = TestIds.AssociationA, Nama = "Poktan Hapus" },
          new Poktan { Id = TestIds.PoktanB1, AssociationId = TestIds.AssociationB, Nama = "Poktan B1" });
      db.Petani.AddRange(
          new Petani { Id = TestIds.PetaniA, PoktanId = TestIds.PoktanA1, ApplicationUserId = TestIds.MemberA, Nama = "Petani A", Nik = "1111111111111111" },
          new Petani { Id = TestIds.PetaniDelete, PoktanId = TestIds.PoktanADelete, Nama = "Petani Akan Dihapus", Nik = "2222222222222222" },
          new Petani { Id = TestIds.PetaniB, PoktanId = TestIds.PoktanB1, ApplicationUserId = TestIds.MemberB, Nama = "Petani B", Nik = "3333333333333333" });
      db.Lahan.AddRange(
          new Lahan { Id = TestIds.LahanA, PetaniId = TestIds.PetaniA, NoLegalitas = "LAHAN-A", LuasLegalitas = 10.25m },
          new Lahan { Id = TestIds.LahanB, PetaniId = TestIds.PetaniB, NoLegalitas = "LAHAN-B", LuasLegalitas = 20.50m });
      db.AssociationAdminAssignments.Add(new AssociationAdminAssignment { UserId = TestIds.AssociationAdminA, AssociationId = TestIds.AssociationA });
      db.PoktanAdminAssignments.Add(new PoktanAdminAssignment { UserId = TestIds.PoktanAdminA1, PoktanId = TestIds.PoktanA1 });
      db.DocumentTypes.Add(new DocumentType {
        Id = TestIds.DocumentTypePetani,
        Code = "KTP",
        Nama = "Kartu Tanda Penduduk",
        OwnerType = DocumentOwnerType.Petani,
        IsRequired = true
      });
      db.DocumentTypes.Add(new DocumentType {
        Id = TestIds.DocumentTypeLahan,
        Code = "SERTIFIKAT_TANAH_SKT",
        Nama = "Sertifikat Tanah / SKT",
        OwnerType = DocumentOwnerType.Lahan,
        IsRequired = true
      });
      db.DocumentTypes.Add(new DocumentType {
        Id = TestIds.DocumentTypeAuditReport,
        Code = "LAPORAN_AUDIT_EKSTERNAL",
        Nama = "Laporan Audit Eksternal",
        OwnerType = DocumentOwnerType.CertificationCycle,
        IsRequired = false
      });
      db.DocumentTypes.Add(new DocumentType {
        Id = TestIds.DocumentTypeAuditClosureReport,
        Code = "LAPORAN_PENUTUPAN_TEMUAN_EKSTERNAL",
        Nama = "Laporan Penutupan Temuan Audit Eksternal",
        OwnerType = DocumentOwnerType.CertificationCycle,
        IsRequired = false
      });
      db.Documents.AddRange(
          Document(TestIds.DocumentA, TestIds.PetaniA, TestIds.MemberA),
          Document(TestIds.DocumentB, TestIds.PetaniB, TestIds.MemberB));
      db.DocumentVersions.AddRange(
          Version(TestIds.DocumentA, TestIds.MemberA, "test/document-a.pdf"),
          Version(TestIds.DocumentB, TestIds.MemberB, "test/document-b.pdf"));
      await db.SaveChangesAsync();
      _seeded = true;
    }
    finally {
      _seedLock.Release();
    }
  }

  private static ApplicationUser User(Guid id, string email) {
    return new ApplicationUser {
      Id = id,
      UserName = email,
      NormalizedUserName = email.ToUpperInvariant(),
      Email = email,
      NormalizedEmail = email.ToUpperInvariant(),
      EmailConfirmed = true,
      Status = AccountStatus.Active,
      SecurityStamp = Guid.NewGuid().ToString("N"),
      ConcurrencyStamp = Guid.NewGuid().ToString()
    };
  }

  private static DocumentRecord Document(Guid id, Guid petaniId, Guid userId) {
    return new DocumentRecord {
      Id = id,
      DocumentTypeId = TestIds.DocumentTypePetani,
      PetaniId = petaniId,
      UploadedByUserId = userId,
      Status = DocumentStatus.Verified
    };
  }

  private static DocumentVersion Version(Guid documentId, Guid userId, string storageKey) {
    return new DocumentVersion {
      Id = Guid.NewGuid(),
      DocumentId = documentId,
      VersionNumber = 1,
      StorageKey = storageKey,
      OriginalFileName = "ktp.pdf",
      ContentType = "application/pdf",
      FileExtension = ".pdf",
      FileSize = 10,
      Sha256Hash = new string('A', 64),
      UploadedByUserId = userId
    };
  }
}
