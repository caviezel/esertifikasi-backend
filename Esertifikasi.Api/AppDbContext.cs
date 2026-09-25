using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Esertifikasi.Api;

public sealed class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid> {
  public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {
  }

  protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
    // Monitoring has several required, independently queryable detail tables beneath
    // soft-deletable master data. Every application query applies scope explicitly.
    optionsBuilder.ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
  }

  public DbSet<Association> Associations => Set<Association>();
  public DbSet<Poktan> Poktan => Set<Poktan>();
  public DbSet<Petani> Petani => Set<Petani>();
  public DbSet<Lahan> Lahan => Set<Lahan>();
  public DbSet<AssociationAdminAssignment> AssociationAdminAssignments => Set<AssociationAdminAssignment>();
  public DbSet<PoktanAdminAssignment> PoktanAdminAssignments => Set<PoktanAdminAssignment>();
  public DbSet<IcsAuditorAssignment> IcsAuditorAssignments => Set<IcsAuditorAssignment>();
  public DbSet<MemberRegistration> MemberRegistrations => Set<MemberRegistration>();
  public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
  public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
  public DbSet<DocumentRecord> Documents => Set<DocumentRecord>();
  public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
  public DbSet<Province> Provinces => Set<Province>();
  public DbSet<Regency> Regencies => Set<Regency>();
  public DbSet<District> Districts => Set<District>();
  public DbSet<Village> Villages => Set<Village>();
  public DbSet<RegionDatasetImport> RegionDatasetImports => Set<RegionDatasetImport>();
  public DbSet<CertificationCycle> CertificationCycles => Set<CertificationCycle>();
  public DbSet<CertificationParticipant> CertificationParticipants => Set<CertificationParticipant>();
  public DbSet<CertificationParticipantLahan> CertificationParticipantLahan => Set<CertificationParticipantLahan>();
  public DbSet<ParticipantStepProgress> ParticipantStepProgress => Set<ParticipantStepProgress>();
  public DbSet<CycleStepProgress> CycleStepProgress => Set<CycleStepProgress>();
  public DbSet<WorkflowTransition> WorkflowTransitions => Set<WorkflowTransition>();
  public DbSet<Disclosure> Disclosures => Set<Disclosure>();
  public DbSet<TrainingSession> TrainingSessions => Set<TrainingSession>();
  public DbSet<TrainingAttendance> TrainingAttendance => Set<TrainingAttendance>();
  public DbSet<MonitoringRecord> MonitoringRecords => Set<MonitoringRecord>();
  public DbSet<MonitoringSubmission> MonitoringSubmissions => Set<MonitoringSubmission>();
  public DbSet<MonitoringDefinition> MonitoringDefinitions => Set<MonitoringDefinition>();
  public DbSet<MonitoringFollowUp> MonitoringFollowUps => Set<MonitoringFollowUp>();
  public DbSet<MonitoringAttachment> MonitoringAttachments => Set<MonitoringAttachment>();
  public DbSet<MonitoringReferenceItem> MonitoringReferenceItems => Set<MonitoringReferenceItem>();
  public DbSet<BaselineAssessment> BaselineAssessments => Set<BaselineAssessment>();
  public DbSet<LandMappingRecord> LandMappingRecords => Set<LandMappingRecord>();
  public DbSet<CertificationAudit> CertificationAudits => Set<CertificationAudit>();
  public DbSet<AuditFinding> AuditFindings => Set<AuditFinding>();
  public DbSet<Certificate> Certificates => Set<Certificate>();
  public DbSet<CertificateParticipant> CertificateParticipants => Set<CertificateParticipant>();
  public DbSet<CertificateLahan> CertificateLahan => Set<CertificateLahan>();
  public DbSet<CycleDocumentRequirement> CycleDocumentRequirements => Set<CycleDocumentRequirement>();
  public DbSet<AssociationDocumentSubmission> AssociationDocumentSubmissions => Set<AssociationDocumentSubmission>();

  protected override void OnModelCreating(ModelBuilder modelBuilder) {
    base.OnModelCreating(modelBuilder);
    modelBuilder.HasDefaultSchema(DatabaseConstants.Schema);

    modelBuilder.Entity<Association>(entity => {
      entity.ToTable("Association");
      entity.HasQueryFilter(x => !x.IsDeleted);
      entity.HasKey(x => x.Id);
      entity.Property(x => x.Nama).HasMaxLength(255).IsRequired();
      entity.Property(x => x.JenisOrganisasi).HasConversion<string>().HasMaxLength(30);
      entity.Property(x => x.KetuaOrganisasi).HasMaxLength(255).IsRequired();
      entity.Property(x => x.Bendahara).HasMaxLength(255).IsRequired();
      entity.Property(x => x.SekretarisOrganisasi).HasMaxLength(255).IsRequired();
      entity.Property(x => x.Bidang).HasMaxLength(255).IsRequired();
      entity.Property(x => x.NoTelp).HasMaxLength(30);
      entity.HasOne(x => x.Province).WithMany().HasForeignKey(x => x.ProvinceId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.Regency).WithMany().HasForeignKey(x => x.RegencyId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.District).WithMany().HasForeignKey(x => x.DistrictId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.Desa).WithMany().HasForeignKey(x => x.DesaId).OnDelete(DeleteBehavior.Restrict);
      entity.HasIndex(x => x.Nama).HasFilter("\"IsDeleted\" = false");
      entity.HasMany(x => x.Poktan).WithOne(x => x.Association)
          .HasForeignKey(x => x.AssociationId).OnDelete(DeleteBehavior.Restrict);
    });

    modelBuilder.Entity<Poktan>(entity => {
      entity.ToTable("Poktan");
      entity.HasQueryFilter(x => !x.IsDeleted && !x.Association.IsDeleted);
      entity.Property(x => x.Nama).HasMaxLength(255).IsRequired();
      entity.Property(x => x.NomorKeanggotaanRspo).HasMaxLength(100);
      entity.Property(x => x.Negara).HasMaxLength(100).IsRequired();
      entity.Property(x => x.BatasMaksimumLuasSawit).HasPrecision(18, 2);
      entity.Property(x => x.LuasAreaSawit).HasPrecision(18, 2);
      entity.HasOne(x => x.Province).WithMany().HasForeignKey(x => x.ProvinceId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.Regency).WithMany().HasForeignKey(x => x.RegencyId).OnDelete(DeleteBehavior.Restrict);
      entity.HasIndex(x => new { x.AssociationId, x.Nama }).IsUnique().HasFilter("\"IsDeleted\" = false");
      entity.HasMany(x => x.Petani).WithOne(x => x.Poktan)
          .HasForeignKey(x => x.PoktanId).OnDelete(DeleteBehavior.Restrict);
    });

    modelBuilder.Entity<Petani>(entity => {
      entity.ToTable("Petani");
      entity.HasQueryFilter(x => !x.IsDeleted && !x.Poktan.IsDeleted && !x.Poktan.Association.IsDeleted);
      entity.HasKey(x => x.Id);
      entity.Property(x => x.Nama).HasMaxLength(255).IsRequired();
      entity.Property(x => x.Nik).HasMaxLength(16);
      entity.Property(x => x.TempatLahir).HasMaxLength(255);
      entity.Property(x => x.JenisKelamin).HasConversion<string>().HasMaxLength(30);
      entity.HasIndex(x => x.DesaId);
      entity.HasOne(x => x.Desa).WithMany(x => x.Petani).HasForeignKey(x => x.DesaId).OnDelete(DeleteBehavior.Restrict);
      entity.Property(x => x.RtRw).HasMaxLength(20);
      entity.Property(x => x.Alamat).HasMaxLength(1000);
      entity.Property(x => x.StatusPerkawinan).HasConversion<string>().HasMaxLength(50);
      entity.Property(x => x.Pekerjaan).HasMaxLength(255);
      entity.Property(x => x.Kewarganegaraan).HasConversion<string>().HasMaxLength(100);
      entity.Property(x => x.Suku).HasMaxLength(100);
      entity.Property(x => x.NoKk).HasMaxLength(16);
      entity.Property(x => x.Email).HasMaxLength(255);
      entity.Property(x => x.NoTelepon).HasMaxLength(30);
      entity.Property(x => x.NamaKepalaKeluarga).HasMaxLength(255);
      entity.Property(x => x.PekerjaanSampingan).HasMaxLength(255);
      entity.Property(x => x.PendidikanTerakhir).HasConversion<string>().HasMaxLength(100);
      entity.HasIndex(x => x.Nik).IsUnique().HasFilter("\"Nik\" IS NOT NULL AND \"IsDeleted\" = false");
      entity.HasIndex(x => x.ApplicationUserId).IsUnique();
      entity.HasIndex(x => new { x.PoktanId, x.IsDeleted });
      entity.HasOne(x => x.ApplicationUser).WithOne(x => x.Petani)
          .HasForeignKey<Petani>(x => x.ApplicationUserId).OnDelete(DeleteBehavior.SetNull);
      entity.HasMany(x => x.Lahan).WithOne(x => x.Petani)
          .HasForeignKey(x => x.PetaniId).OnDelete(DeleteBehavior.Restrict);
    });

    modelBuilder.Entity<Lahan>(entity => {
      entity.ToTable("Lahan");
      entity.HasQueryFilter(x => !x.IsDeleted && !x.Petani.IsDeleted && !x.Petani.Poktan.IsDeleted && !x.Petani.Poktan.Association.IsDeleted);
      entity.HasKey(x => x.Id);
      entity.Property(x => x.BoundaryGeoJson).HasColumnType("jsonb");
      entity.Property(x => x.NoLegalitas).HasMaxLength(255);
      entity.Property(x => x.NoSppl).HasMaxLength(255);
      entity.Property(x => x.NoStdb).HasMaxLength(255);
      entity.Property(x => x.JenisKepemilikan).HasMaxLength(100);
      entity.Property(x => x.StatusKepemilikan).HasMaxLength(100);
      entity.Property(x => x.KeteranganSertifikat).HasMaxLength(1000);
      entity.Property(x => x.KemanaMenjualPanen).HasMaxLength(255);
      entity.Property(x => x.NamaPembeli).HasMaxLength(255);
      entity.Property(x => x.NamaPabrik).HasMaxLength(255);
      entity.Property(x => x.Komoditas).HasMaxLength(100);
      entity.Property(x => x.BatasUtara).HasMaxLength(255);
      entity.Property(x => x.BatasTimur).HasMaxLength(255);
      entity.Property(x => x.BatasBarat).HasMaxLength(255);
      entity.Property(x => x.BatasSelatan).HasMaxLength(255);
      entity.Property(x => x.JenisTanah).HasConversion<string>().HasMaxLength(100);
      entity.Property(x => x.TanamanLain).HasMaxLength(255);
      entity.Property(x => x.TempatBeliPupuk).HasMaxLength(255);
      entity.Property(x => x.PolaTanam).HasConversion<string>().HasMaxLength(100);
      entity.Property(x => x.MitraPengelola).HasConversion<string>().HasMaxLength(255);
      entity.Property(x => x.MitraPengelolaLainnya).HasConversion<string>().HasMaxLength(255);
      entity.Property(x => x.AsalBibit).HasMaxLength(255);
      entity.Property(x => x.JenisBibit).HasMaxLength(255);
      entity.Property(x => x.BisnisLain).HasMaxLength(255);
      entity.HasIndex(x => x.DesaId);
      entity.HasOne(x => x.Desa).WithMany(x => x.Lahan).HasForeignKey(x => x.DesaId).OnDelete(DeleteBehavior.Restrict);
      entity.Property(x => x.RtRw).HasMaxLength(20);
      entity.Property(x => x.Alamat).HasMaxLength(1000);
      entity.Property(x => x.NoPetaLahan).HasMaxLength(255);
      entity.Property(x => x.MetodeBuka).HasMaxLength(255);
      entity.Property(x => x.DibukaOleh).HasMaxLength(255);
      entity.Property(x => x.TutupanLahan).HasMaxLength(255);
      entity.Property(x => x.TutupanLahanLainnya).HasMaxLength(255);
      entity.Property(x => x.PerolehanTanahGarapan).HasConversion<string>().HasMaxLength(255);
      entity.Property(x => x.PeruntukanTanah).HasConversion<string>().HasMaxLength(255);
      entity.Property(x => x.TanamanAwal).HasMaxLength(255);
      entity.Property(x => x.LuasLegalitas).HasPrecision(18, 2);
      entity.Property(x => x.LuasTertanam).HasPrecision(18, 2);
      entity.Property(x => x.LuasProduktif).HasPrecision(18, 2);
      entity.Property(x => x.ProduksiRataRata).HasPrecision(18, 2);
      entity.Property(x => x.BeratTbs).HasPrecision(18, 2);
      entity.Property(x => x.LuasGeometri).HasPrecision(18, 2);
      entity.Property(x => x.LuasTerverifikasi).HasPrecision(18, 2);
      entity.Property(x => x.SelisihLuas).HasPrecision(18, 2);
      entity.HasIndex(x => x.NoLegalitas);
      entity.HasIndex(x => new { x.PetaniId, x.IsDeleted });
    });

    modelBuilder.Entity<AssociationAdminAssignment>(entity => {
      entity.ToTable("AssociationAdminAssignment");
      entity.HasQueryFilter(x => !x.Association.IsDeleted);
      entity.HasKey(x => new { x.UserId, x.AssociationId });
      entity.HasOne(x => x.User).WithMany(x => x.AssociationAssignments).HasForeignKey(x => x.UserId);
      entity.HasOne(x => x.Association).WithMany(x => x.AdminAssignments).HasForeignKey(x => x.AssociationId);
    });
    modelBuilder.Entity<PoktanAdminAssignment>(entity => {
      entity.ToTable("PoktanAdminAssignment");
      entity.HasQueryFilter(x => !x.Poktan.IsDeleted && !x.Poktan.Association.IsDeleted);
      entity.HasKey(x => new { x.UserId, x.PoktanId });
      entity.HasOne(x => x.User).WithMany(x => x.PoktanAssignments).HasForeignKey(x => x.UserId);
      entity.HasOne(x => x.Poktan).WithMany(x => x.AdminAssignments).HasForeignKey(x => x.PoktanId);
    });
    modelBuilder.Entity<IcsAuditorAssignment>(entity => {
      entity.ToTable("IcsAuditorAssignment");
      entity.HasQueryFilter(x => !x.Poktan.IsDeleted && !x.Poktan.Association.IsDeleted);
      entity.HasKey(x => new { x.UserId, x.PoktanId });
      entity.HasOne(x => x.User).WithMany(x => x.IcsAuditorAssignments).HasForeignKey(x => x.UserId);
      entity.HasOne(x => x.Poktan).WithMany(x => x.IcsAuditorAssignments).HasForeignKey(x => x.PoktanId);
    });
    modelBuilder.Entity<AuditFinding>(entity => {
      entity.HasOne(x => x.Poktan).WithMany().HasForeignKey(x => x.PoktanId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.Entity<MemberRegistration>(entity => {
      entity.ToTable("MemberRegistration");
      entity.HasQueryFilter(x =>
          !x.Association.IsDeleted &&
          !x.Poktan.IsDeleted &&
          !x.Poktan.Association.IsDeleted);
      entity.Property(x => x.Provider).HasMaxLength(50).IsRequired();
      entity.Property(x => x.ProviderSubjectId).HasMaxLength(255).IsRequired();
      entity.Property(x => x.Nama).HasMaxLength(255).IsRequired();
      entity.Property(x => x.Nik).HasMaxLength(16);
      entity.HasIndex(x => new { x.Provider, x.ProviderSubjectId }).IsUnique();
      entity.HasIndex(x => new { x.AssociationId, x.Status });
      entity.HasIndex(x => new { x.PoktanId, x.Status });
      entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
      entity.HasOne(x => x.Association).WithMany().HasForeignKey(x => x.AssociationId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.Poktan).WithMany().HasForeignKey(x => x.PoktanId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.ExistingPetani).WithMany().HasForeignKey(x => x.ExistingPetaniId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.ReviewedByUser).WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.Entity<RefreshToken>(entity => {
      entity.ToTable("RefreshToken");
      entity.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
      entity.HasIndex(x => x.TokenHash).IsUnique();
      entity.HasIndex(x => new { x.UserId, x.ExpiresAt });
      entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
    });
    modelBuilder.Entity<DocumentType>(entity => {
      entity.ToTable("DocumentType");
      entity.Property(x => x.Code).HasMaxLength(100).IsRequired();
      entity.Property(x => x.Nama).HasMaxLength(255).IsRequired();
      entity.Property(x => x.AllowedExtensions).HasMaxLength(255).IsRequired();
      entity.HasIndex(x => new { x.OwnerType, x.Code }).IsUnique().HasFilter("\"IsActive\" = true");
    });
    modelBuilder.Entity<DocumentRecord>(entity => {
      entity.ToTable("Document", table => table.HasCheckConstraint(
          "CK_Document_ExactlyOneOwner",
          "(CASE WHEN \"PetaniId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"LahanId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"AssociationId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"CertificationCycleId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"CertificationParticipantId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"CertificationParticipantLahanId\" IS NULL THEN 0 ELSE 1 END) = 1"));
      entity.HasQueryFilter(x => !x.IsDeleted);
      entity.Property(x => x.Catatan).HasMaxLength(2000);
      entity.Property(x => x.RejectionReason).HasMaxLength(2000);
      entity.HasOne(x => x.DocumentType).WithMany(x => x.Documents).HasForeignKey(x => x.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.Petani).WithMany(x => x.Documents).HasForeignKey(x => x.PetaniId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.Lahan).WithMany(x => x.Documents).HasForeignKey(x => x.LahanId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.Association).WithMany(x => x.Documents).HasForeignKey(x => x.AssociationId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.CertificationCycle).WithMany(x => x.Documents).HasForeignKey(x => x.CertificationCycleId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.CertificationParticipant).WithMany().HasForeignKey(x => x.CertificationParticipantId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.CertificationParticipantLahan).WithMany(x => x.Documents).HasForeignKey(x => x.CertificationParticipantLahanId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.ReviewedByUser).WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
      entity.HasIndex(x => new { x.PetaniId, x.DocumentTypeId }).IsUnique().HasFilter("\"IsDeleted\" = false AND \"PetaniId\" IS NOT NULL");
      entity.HasIndex(x => new { x.LahanId, x.DocumentTypeId }).IsUnique().HasFilter("\"IsDeleted\" = false AND \"LahanId\" IS NOT NULL");
      entity.HasIndex(x => new { x.AssociationId, x.DocumentTypeId }).IsUnique().HasFilter("\"IsDeleted\" = false AND \"AssociationId\" IS NOT NULL");
      entity.HasIndex(x => new { x.CertificationCycleId, x.DocumentTypeId }).IsUnique().HasFilter("\"IsDeleted\" = false AND \"CertificationCycleId\" IS NOT NULL");
      entity.HasIndex(x => new { x.CertificationParticipantId, x.DocumentTypeId }).IsUnique().HasFilter("\"IsDeleted\" = false AND \"CertificationParticipantId\" IS NOT NULL");
      entity.HasIndex(x => new { x.CertificationParticipantLahanId, x.DocumentTypeId }).IsUnique().HasFilter("\"IsDeleted\" = false AND \"CertificationParticipantLahanId\" IS NOT NULL");
      entity.HasIndex(x => new { x.Status, x.IsDeleted });
    });
    modelBuilder.Entity<DocumentVersion>(entity => {
      entity.ToTable("DocumentVersion");
      entity.HasQueryFilter(x => !x.Document.IsDeleted);
      entity.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
      entity.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
      entity.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
      entity.Property(x => x.FileExtension).HasMaxLength(20).IsRequired();
      entity.Property(x => x.Sha256Hash).HasMaxLength(64).IsRequired();
      entity.HasIndex(x => new { x.DocumentId, x.VersionNumber }).IsUnique();
      entity.HasOne(x => x.Document).WithMany(x => x.Versions).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
      entity.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.ConfigureAdministrativeRegionModel();
    modelBuilder.ConfigureCertificationModel();
    modelBuilder.ConfigureMonitoringModel();
  }
}
