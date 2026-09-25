using Esertifikasi.Api.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api.Security;

public static class IdentitySeeder {
  public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration) {
    using var scope = services.CreateScope();
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

    foreach (var role in AppRoles.All) {
      if (await roles.RoleExistsAsync(role)) {
        continue;
      }

      var roleResult = await roles.CreateAsync(new IdentityRole<Guid>(role));
      EnsureSucceeded(roleResult, $"Role '{role}' tidak dapat dibuat");
    }

    await SeedAssociationDocumentTypesAsync(scope.ServiceProvider);
    await SeedPetaniDocumentTypesAsync(scope.ServiceProvider);
    await SeedLahanDocumentTypesAsync(scope.ServiceProvider);
    await SeedCertificationCycleDocumentTypesAsync(scope.ServiceProvider);
    await SeedExternalAuditDocumentTypesAsync(scope.ServiceProvider);

    if (!configuration.GetValue("BootstrapAdmin:Enabled", false)) {
      return;
    }

    var email = configuration["BootstrapAdmin:Email"];
    var password = configuration["BootstrapAdmin:Password"];

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) {
      throw new InvalidOperationException(
          "BootstrapAdmin:Email dan BootstrapAdmin:Password wajib dikonfigurasi ketika bootstrap administrator diaktifkan.");
    }

    email = email.Trim();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var user = await users.FindByEmailAsync(email);

    if (user is null) {
      user = new ApplicationUser {
        Id = Guid.NewGuid(),
        UserName = email,
        Email = email,
        EmailConfirmed = true,
        Status = AccountStatus.Active
      };

      var result = await users.CreateAsync(user, password);
      EnsureSucceeded(result, "Administrator awal tidak dapat dibuat");
    }
    else if (user.Status != AccountStatus.Active || !user.EmailConfirmed) {
      user.Status = AccountStatus.Active;
      user.EmailConfirmed = true;
      var updateResult = await users.UpdateAsync(user);
      EnsureSucceeded(updateResult, "Administrator awal tidak dapat diaktifkan");
    }

    if (!await users.IsInRoleAsync(user, AppRoles.SuperAdmin)) {
      var roleResult = await users.AddToRoleAsync(user, AppRoles.SuperAdmin);
      EnsureSucceeded(roleResult, "Role SuperAdmin tidak dapat diberikan kepada administrator awal");
    }
  }

  private static async Task SeedPetaniDocumentTypesAsync(IServiceProvider services) {
    var db = services.GetRequiredService<AppDbContext>();
    var definitions = new[] {
      (Guid.Parse("52000000-0000-0000-0000-000000000001"), "KTP", "KTP"),
      (Guid.Parse("52000000-0000-0000-0000-000000000002"), "KARTU_KELUARGA", "Kartu Keluarga"),
      (Guid.Parse("52000000-0000-0000-0000-000000000003"), "SURAT_KETERANGAN_TANAH", "Surat Keterangan Tanah")
    };
    var existing = (await db.DocumentTypes.Where(x => x.OwnerType == DocumentOwnerType.Petani).ToListAsync())
        .ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
    foreach (var definition in definitions) {
      if (existing.TryGetValue(definition.Item2, out var existingType)) {
        existingType.IsActive = true;
        existingType.IsRequired = true;
        continue;
      }
      var type = new DocumentType {
        Id = definition.Item1, Code = definition.Item2, Nama = definition.Item3,
        OwnerType = DocumentOwnerType.Petani, IsRequired = true,
        AllowedExtensions = ".pdf,.jpg,.jpeg,.png", MaximumFileSize = 10 * 1024 * 1024, IsActive = true
      };
      db.DocumentTypes.Add(type);
      existing.Add(type.Code, type);
    }
    await db.SaveChangesAsync();

    var cycles = await db.CertificationCycles.Select(x => x.Id).ToListAsync();
    var existingRequirements = await db.CycleDocumentRequirements
        .Where(x => x.OwnerType == DocumentOwnerType.CertificationParticipant)
        .Select(x => new { x.CertificationCycleId, x.DocumentTypeId }).ToListAsync();
    var requirementKeys = existingRequirements.Select(x => (x.CertificationCycleId, x.DocumentTypeId)).ToHashSet();
    foreach (var cycleId in cycles) {
      foreach (var type in existing.Values.Where(x => x.IsActive)) {
        if (!requirementKeys.Add((cycleId, type.Id))) continue;
        db.CycleDocumentRequirements.Add(new CycleDocumentRequirement {
          Id = Guid.NewGuid(), CertificationCycleId = cycleId, DocumentTypeId = type.Id,
          OwnerType = DocumentOwnerType.CertificationParticipant, IsRequired = type.IsRequired
        });
      }
    }
    await db.SaveChangesAsync();
  }

  private static async Task SeedAssociationDocumentTypesAsync(IServiceProvider services) {
    var db = services.GetRequiredService<AppDbContext>();
    var definitions = new[] {
      (Guid.Parse("51000000-0000-0000-0000-000000000001"), "AKTA_PENDIRIAN", "Akta Pendirian"),
      (Guid.Parse("51000000-0000-0000-0000-000000000002"), "SK_KEMENKUMHAM", "SK Kemenkumham"),
      (Guid.Parse("51000000-0000-0000-0000-000000000003"), "NPWP_ORGANISASI", "NPWP Organisasi"),
      (Guid.Parse("51000000-0000-0000-0000-000000000004"), "SURAT_KETERANGAN_DOMISILI", "Surat Keterangan Domisili"),
      (Guid.Parse("51000000-0000-0000-0000-000000000005"), "STRUKTUR_ORGANISASI", "Struktur Organisasi"),
      (Guid.Parse("51000000-0000-0000-0000-000000000006"), "AD_ART", "AD/ART"),
      (Guid.Parse("51000000-0000-0000-0000-000000000007"), "PROFIL_ORGANISASI", "Profil Organisasi"),
      (Guid.Parse("51000000-0000-0000-0000-000000000008"), "DAFTAR_ANGGOTA", "Daftar Anggota"),
      (Guid.Parse("51000000-0000-0000-0000-000000000009"), "PETA_WILAYAH_KERJA", "Peta Wilayah Kerja"),
      (Guid.Parse("51000000-0000-0000-0000-000000000010"), "LOGO_KOP_ORGANISASI", "Logo/KOP Organisasi")
    };
    var existing = (await db.DocumentTypes.Where(x => x.OwnerType == DocumentOwnerType.Association).ToListAsync())
        .ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
    foreach (var definition in definitions) {
      if (existing.TryGetValue(definition.Item2, out var existingType)) {
        existingType.IsActive = true;
        existingType.IsRequired = true;
        continue;
      }
      var type = new DocumentType {
        Id = definition.Item1, Code = definition.Item2, Nama = definition.Item3,
        OwnerType = DocumentOwnerType.Association, IsRequired = true,
        AllowedExtensions = ".pdf,.jpg,.jpeg,.png", MaximumFileSize = 10 * 1024 * 1024, IsActive = true
      };
      db.DocumentTypes.Add(type);
      existing.Add(type.Code, type);
    }
    await db.SaveChangesAsync();

    var cycles = await db.CertificationCycles.Select(x => x.Id).ToListAsync();
    var existingRequirements = await db.CycleDocumentRequirements
        .Where(x => x.OwnerType == DocumentOwnerType.Association)
        .Select(x => new { x.CertificationCycleId, x.DocumentTypeId }).ToListAsync();
    var requirementKeys = existingRequirements.Select(x => (x.CertificationCycleId, x.DocumentTypeId)).ToHashSet();
    foreach (var cycleId in cycles) {
      foreach (var type in existing.Values.Where(x => x.IsActive)) {
        if (!requirementKeys.Add((cycleId, type.Id))) continue;
        db.CycleDocumentRequirements.Add(new CycleDocumentRequirement {
          Id = Guid.NewGuid(), CertificationCycleId = cycleId, DocumentTypeId = type.Id,
          OwnerType = DocumentOwnerType.Association, IsRequired = type.IsRequired
        });
      }
    }
    await db.SaveChangesAsync();
  }

  private static async Task SeedLahanDocumentTypesAsync(IServiceProvider services) {
    var db = services.GetRequiredService<AppDbContext>();
    var definitions = new[] {
      (Guid.Parse("53000000-0000-0000-0000-000000000001"), "SERTIFIKAT_TANAH_SKT", "Sertifikat Tanah / SKT"),
      (Guid.Parse("53000000-0000-0000-0000-000000000002"), "SPPT_PBB", "SPPT PBB"),
      (Guid.Parse("53000000-0000-0000-0000-000000000003"), "SURAT_KETERANGAN_DESA", "Surat Keterangan Desa"),
      (Guid.Parse("53000000-0000-0000-0000-000000000004"), "FOTO_LAHAN_UTARA", "Foto Lahan (Utara)"),
      (Guid.Parse("53000000-0000-0000-0000-000000000005"), "FOTO_LAHAN_SELATAN", "Foto Lahan (Selatan)"),
      (Guid.Parse("53000000-0000-0000-0000-000000000006"), "FOTO_LAHAN_TIMUR", "Foto Lahan (Timur)"),
      (Guid.Parse("53000000-0000-0000-0000-000000000007"), "FOTO_LAHAN_BARAT", "Foto Lahan (Barat)"),
      (Guid.Parse("53000000-0000-0000-0000-000000000008"), "PETA_SKETSA_LAHAN", "Peta Sketsa Lahan")
    };
    var existing = (await db.DocumentTypes.Where(x => x.OwnerType == DocumentOwnerType.Lahan).ToListAsync())
        .ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
    foreach (var definition in definitions) {
      if (existing.TryGetValue(definition.Item2, out var existingType)) {
        existingType.IsActive = true;
        existingType.IsRequired = true;
        continue;
      }
      var type = new DocumentType {
        Id = definition.Item1, Code = definition.Item2, Nama = definition.Item3,
        OwnerType = DocumentOwnerType.Lahan, IsRequired = true,
        AllowedExtensions = ".pdf,.jpg,.jpeg,.png", MaximumFileSize = 10 * 1024 * 1024, IsActive = true
      };
      db.DocumentTypes.Add(type);
      existing.Add(type.Code, type);
    }
    await db.SaveChangesAsync();

    var cycles = await db.CertificationCycles.Select(x => x.Id).ToListAsync();
    var existingRequirements = await db.CycleDocumentRequirements
        .Where(x => x.OwnerType == DocumentOwnerType.CertificationParticipantLahan)
        .Select(x => new { x.CertificationCycleId, x.DocumentTypeId }).ToListAsync();
    var requirementKeys = existingRequirements.Select(x => (x.CertificationCycleId, x.DocumentTypeId)).ToHashSet();
    foreach (var cycleId in cycles) {
      foreach (var type in existing.Values.Where(x => x.IsActive)) {
        if (!requirementKeys.Add((cycleId, type.Id))) continue;
        db.CycleDocumentRequirements.Add(new CycleDocumentRequirement {
          Id = Guid.NewGuid(), CertificationCycleId = cycleId, DocumentTypeId = type.Id,
          OwnerType = DocumentOwnerType.CertificationParticipantLahan, IsRequired = type.IsRequired
        });
      }
    }
    await db.SaveChangesAsync();
  }

  private static async Task SeedCertificationCycleDocumentTypesAsync(IServiceProvider services) {
    var db = services.GetRequiredService<AppDbContext>();
    const string code = "CERTIFICATE";
    var type = await db.DocumentTypes.SingleOrDefaultAsync(
        x => x.OwnerType == DocumentOwnerType.CertificationCycle && x.Code == code);

    if (type is null) {
      type = new DocumentType {
        Id = Guid.Parse("54000000-0000-0000-0000-000000000001"),
        Code = code,
        Nama = "Sertifikat",
        OwnerType = DocumentOwnerType.CertificationCycle,
        IsRequired = true,
        AllowedExtensions = ".pdf",
        MaximumFileSize = 10 * 1024 * 1024,
        IsActive = true
      };
      db.DocumentTypes.Add(type);
    }
    else {
      type.IsActive = true;
      type.IsRequired = true;
    }

    await db.SaveChangesAsync();

    var cycleIds = await db.CertificationCycles.Select(x => x.Id).ToListAsync();
    var existingCycleIds = (await db.CycleDocumentRequirements
        .Where(x => x.OwnerType == DocumentOwnerType.CertificationCycle
            && x.DocumentTypeId == type.Id)
        .Select(x => x.CertificationCycleId)
        .ToListAsync()).ToHashSet();

    foreach (var cycleId in cycleIds.Where(existingCycleIds.Add)) {
      db.CycleDocumentRequirements.Add(new CycleDocumentRequirement {
        Id = Guid.NewGuid(),
        CertificationCycleId = cycleId,
        DocumentTypeId = type.Id,
        OwnerType = DocumentOwnerType.CertificationCycle,
        IsRequired = true
      });
    }

    await db.SaveChangesAsync();
  }

  private static async Task SeedExternalAuditDocumentTypesAsync(IServiceProvider services) {
    var db = services.GetRequiredService<AppDbContext>();
    var definitions = new[] {
      (Guid.Parse("55000000-0000-0000-0000-000000000001"), "LAPORAN_AUDIT_EKSTERNAL", "Laporan Audit Eksternal"),
      (Guid.Parse("55000000-0000-0000-0000-000000000002"), "LAPORAN_PENUTUPAN_TEMUAN_EKSTERNAL", "Laporan Penutupan Temuan Audit Eksternal")
    };
    var existing = (await db.DocumentTypes.Where(x => x.OwnerType == DocumentOwnerType.CertificationCycle
        && (x.Code == definitions[0].Item2 || x.Code == definitions[1].Item2)).ToListAsync())
        .ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
    foreach (var definition in definitions) {
      if (existing.TryGetValue(definition.Item2, out var existingType)) {
        existingType.IsActive = true;
        continue;
      }
      db.DocumentTypes.Add(new DocumentType {
        Id = definition.Item1, Code = definition.Item2, Nama = definition.Item3,
        OwnerType = DocumentOwnerType.CertificationCycle, IsRequired = false,
        AllowedExtensions = ".pdf", MaximumFileSize = 10 * 1024 * 1024, IsActive = true
      });
    }
    await db.SaveChangesAsync();
  }

  private static void EnsureSucceeded(IdentityResult result, string message) {
    if (result.Succeeded) {
      return;
    }

    throw new InvalidOperationException(
        $"{message}: {string.Join("; ", result.Errors.Select(x => x.Description))}");
  }
}
