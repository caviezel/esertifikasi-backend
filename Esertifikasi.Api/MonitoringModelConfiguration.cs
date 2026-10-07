using Esertifikasi.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Esertifikasi.Api;

internal static class MonitoringModelConfiguration {
  public static void ConfigureMonitoringModel(this ModelBuilder modelBuilder) {
    modelBuilder.Ignore<MonitoringDetailBase>();
    modelBuilder.Ignore<FarmerLandMonitoringRow>();

    modelBuilder.Entity<MonitoringDefinition>(e => {
      e.ToTable("MonitoringDefinition"); e.HasKey(x => x.Type); e.Property(x => x.Name).HasMaxLength(255);
    });
    modelBuilder.Entity<MonitoringSubmission>(e => {
      e.ToTable("MonitoringSubmission", t => t.HasCheckConstraint("CK_MonitoringSubmission_Period", "\"PeriodEnd\" >= \"PeriodStart\""));
      e.Property(x => x.Version).IsConcurrencyToken(); e.HasQueryFilter(x => !x.IsDeleted && !x.Poktan.IsDeleted && !x.Association.IsDeleted);
      e.HasKey(x => x.Id); e.Property(x => x.Summary).HasMaxLength(4000); e.Property(x => x.ReopenReason).HasMaxLength(2000);
      e.Property(x => x.PreparedByName).HasMaxLength(255); e.Property(x => x.AcknowledgedByName).HasMaxLength(255);
      e.HasIndex(x => new { x.PoktanId, x.Type, x.PeriodStart, x.PeriodEnd }).IsUnique().HasFilter("\"IsDeleted\" = false");
      e.HasOne(x => x.Association).WithMany().HasForeignKey(x => x.AssociationId).OnDelete(DeleteBehavior.Restrict);
      e.HasOne(x => x.Poktan).WithMany().HasForeignKey(x => x.PoktanId).OnDelete(DeleteBehavior.Restrict);
      e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
      e.HasOne(x => x.FinalizedByUser).WithMany().HasForeignKey(x => x.FinalizedByUserId).OnDelete(DeleteBehavior.Restrict);
      e.HasOne(x => x.ReopenedByUser).WithMany().HasForeignKey(x => x.ReopenedByUserId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.Entity<MonitoringFollowUp>(e => {
      e.ToTable("MonitoringFollowUp"); e.Property(x => x.Description).HasMaxLength(2000);
      e.HasOne(x => x.MonitoringSubmission).WithMany(x => x.FollowUps).HasForeignKey(x => x.MonitoringSubmissionId).OnDelete(DeleteBehavior.Cascade);
      e.HasOne(x => x.AssignedToUser).WithMany().HasForeignKey(x => x.AssignedToUserId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.Entity<MonitoringAttachment>(e => {
      e.ToTable("MonitoringAttachment"); e.Property(x => x.StorageKey).HasMaxLength(500); e.Property(x => x.OriginalFileName).HasMaxLength(255);
      e.Property(x => x.ContentType).HasMaxLength(100); e.Property(x => x.FileExtension).HasMaxLength(20); e.Property(x => x.Sha256Hash).HasMaxLength(64);
      e.HasOne(x => x.MonitoringSubmission).WithMany(x => x.Attachments).HasForeignKey(x => x.MonitoringSubmissionId).OnDelete(DeleteBehavior.Cascade);
      e.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.Entity<MonitoringReferenceItem>(e => {
      e.ToTable("MonitoringReferenceItem"); e.Property(x => x.Code).HasMaxLength(100); e.Property(x => x.Name).HasMaxLength(255); e.Property(x => x.Description).HasMaxLength(1000);
      e.HasIndex(x => new { x.Kind, x.Code }).IsUnique();
    });

    Detail<LandBoundaryMonitoring>(modelBuilder, MonitoringType.LandBoundaryMarker);
    Detail<TurneraMonitoring>(modelBuilder, MonitoringType.Turnera);
    Detail<ChemicalBufferMonitoring>(modelBuilder, MonitoringType.ChemicalBufferBoundary);
    Detail<WoodyPlantMonitoring>(modelBuilder, MonitoringType.WoodyPlantAndErosionControl);
    Detail<FirstAidKitMonitoring>(modelBuilder, MonitoringType.FirstAidKit);
    Detail<PpeMonitoring>(modelBuilder, MonitoringType.PersonalProtectiveEquipment);
    Detail<HighConservationValueMonitoring>(modelBuilder, MonitoringType.HighConservationValue);
    Detail<FireMonitoring>(modelBuilder, MonitoringType.FireIncident);
    Detail<WorkplaceAccidentMonitoring>(modelBuilder, MonitoringType.WorkplaceAccident);
    Detail<WeedMonitoring>(modelBuilder, MonitoringType.Weed);
    Detail<PlantDiseaseMonitoring>(modelBuilder, MonitoringType.PlantDisease);
    Detail<PestMonitoring>(modelBuilder, MonitoringType.Pest);
    Detail<MemberComplaintMonitoring>(modelBuilder, MonitoringType.MemberComplaint);

    Row<LandBoundaryInspection, LandBoundaryMonitoring>(modelBuilder, "LandBoundaryInspection", x => x.MonitoringSubmissionId, x => x.Inspections);
    Row<TurneraInspection, TurneraMonitoring>(modelBuilder, "TurneraInspection", x => x.MonitoringSubmissionId, x => x.Inspections);
    Row<ChemicalBufferInspection, ChemicalBufferMonitoring>(modelBuilder, "ChemicalBufferInspection", x => x.MonitoringSubmissionId, x => x.Inspections);
    Row<WoodyPlantInspection, WoodyPlantMonitoring>(modelBuilder, "WoodyPlantInspection", x => x.MonitoringSubmissionId, x => x.Inspections);
    Row<PpeInspection, PpeMonitoring>(modelBuilder, "PpeInspection", x => x.MonitoringSubmissionId, x => x.Inspections);
    Row<HcvLocationAssessment, HighConservationValueMonitoring>(modelBuilder, "HcvLocationAssessment", x => x.MonitoringSubmissionId, x => x.Locations);
    Row<ProtectedSpeciesObservation, HighConservationValueMonitoring>(modelBuilder, "ProtectedSpeciesObservation", x => x.MonitoringSubmissionId, x => x.SpeciesObservations);
    Row<FireIncident, FireMonitoring>(modelBuilder, "FireIncident", x => x.MonitoringSubmissionId, x => x.Incidents);
    Row<WorkplaceAccidentIncident, WorkplaceAccidentMonitoring>(modelBuilder, "WorkplaceAccidentIncident", x => x.MonitoringSubmissionId, x => x.Incidents);
    Row<WeedInspection, WeedMonitoring>(modelBuilder, "WeedInspection", x => x.MonitoringSubmissionId, x => x.Inspections);
    Row<PlantDiseaseInspection, PlantDiseaseMonitoring>(modelBuilder, "PlantDiseaseInspection", x => x.MonitoringSubmissionId, x => x.Inspections);
    Row<PestInspection, PestMonitoring>(modelBuilder, "PestInspection", x => x.MonitoringSubmissionId, x => x.Inspections);

    modelBuilder.Entity<FirstAidKitInspection>(e => { e.ToTable("FirstAidKitInspection"); e.HasQueryFilter(x => !x.IsDeleted); e.HasOne<FirstAidLocation>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict); e.Property(x => x.Location).HasMaxLength(500); e.Property(x => x.Notes).HasMaxLength(2000); e.HasOne<FirstAidKitMonitoring>().WithMany(x => x.Inspections).HasForeignKey(x => x.MonitoringSubmissionId).OnDelete(DeleteBehavior.Cascade); });
    modelBuilder.Entity<FirstAidKitItemInspection>(e => { e.ToTable("FirstAidKitItemInspection"); e.Property(x => x.ItemName).HasMaxLength(255); e.HasOne<FirstAidKitInspection>().WithMany(x => x.Items).HasForeignKey(x => x.FirstAidKitInspectionId).OnDelete(DeleteBehavior.Cascade); });
    modelBuilder.Entity<WoodyPlantObservation>(e => { e.ToTable("WoodyPlantObservation"); e.Property(x => x.TreeName).HasMaxLength(255); e.HasOne<WoodyPlantInspection>().WithMany(x => x.Observations).HasForeignKey(x => x.WoodyPlantInspectionId).OnDelete(DeleteBehavior.Cascade); });
    modelBuilder.Entity<PpeItemInspection>(e => { e.ToTable("PpeItemInspection"); e.Property(x => x.ItemName).HasMaxLength(255); e.HasOne<PpeInspection>().WithMany(x => x.Items).HasForeignKey(x => x.PpeInspectionId).OnDelete(DeleteBehavior.Cascade); });
    modelBuilder.Entity<MemberComplaint>(e => { e.ToTable("MemberComplaint"); e.HasQueryFilter(x => !x.IsDeleted); e.HasOne<Lahan>().WithMany().HasForeignKey(x => x.LahanId).OnDelete(DeleteBehavior.Restrict); e.Property(x => x.ComplaintType).HasMaxLength(255); e.Property(x => x.Description).HasMaxLength(4000); e.HasOne<MemberComplaintMonitoring>().WithMany(x => x.Complaints).HasForeignKey(x => x.MonitoringSubmissionId).OnDelete(DeleteBehavior.Cascade); e.HasOne(x => x.Petani).WithMany().HasForeignKey(x => x.PetaniId).OnDelete(DeleteBehavior.Restrict); });
  }

  private static void Detail<T>(ModelBuilder modelBuilder, MonitoringType _) where T : MonitoringDetailBase {
    modelBuilder.Entity<T>(e => { e.ToTable(typeof(T).Name); e.HasKey(x => x.MonitoringSubmissionId); e.HasOne(x => x.MonitoringSubmission).WithOne().HasForeignKey<T>(x => x.MonitoringSubmissionId).OnDelete(DeleteBehavior.Cascade); });
  }

  private static void Row<TRow, TDetail>(ModelBuilder modelBuilder, string table,
      System.Linq.Expressions.Expression<Func<TRow, object?>> foreignKey,
      System.Linq.Expressions.Expression<Func<TDetail, IEnumerable<TRow>?>> navigation)
      where TRow : FarmerLandMonitoringRow where TDetail : MonitoringDetailBase {
    modelBuilder.Entity<TRow>(e => {
      e.ToTable(table); e.HasQueryFilter(x => !x.IsDeleted); e.HasKey(x => x.Id); e.Property(x => x.FarmerNameSnapshot).HasMaxLength(255); e.Property(x => x.NikSnapshot).HasMaxLength(16);
      e.Property(x => x.LandLegalNumberSnapshot).HasMaxLength(255); e.Property(x => x.LandAreaSnapshot).HasPrecision(18, 2); e.Property(x => x.Notes).HasMaxLength(2000); e.Property(x => x.FollowUp).HasMaxLength(2000);
      e.HasOne<TDetail>().WithMany(navigation).HasForeignKey(foreignKey).OnDelete(DeleteBehavior.Cascade);
      e.HasOne(x => x.Petani).WithMany().HasForeignKey(x => x.PetaniId).OnDelete(DeleteBehavior.Restrict);
      e.HasOne(x => x.Lahan).WithMany().HasForeignKey(x => x.LahanId).OnDelete(DeleteBehavior.Restrict);
    });
  }
}
