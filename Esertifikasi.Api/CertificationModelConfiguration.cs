using Esertifikasi.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api;

internal static class CertificationModelConfiguration {
  public static void ConfigureCertificationModel(this ModelBuilder modelBuilder) {
    modelBuilder.Entity<CertificationCycle>(entity => {
      entity.ToTable("CertificationCycle", table => {
        table.HasCheckConstraint("CK_CertificationCycle_Sequence", "\"SequenceNumber\" >= 0");
        table.HasCheckConstraint("CK_CertificationCycle_TypeSequence", "(\"Type\" = 0 AND \"SequenceNumber\" = 0) OR (\"Type\" = 1 AND \"SequenceNumber\" BETWEEN 1 AND 4) OR (\"Type\" = 2 AND \"SequenceNumber\" >= 5)");
      });
      entity.HasQueryFilter(x => !x.Association.IsDeleted);
      entity.HasKey(x => x.Id);
      entity.HasIndex(x => new { x.AssociationId, x.SequenceNumber }).IsUnique();
      entity.HasIndex(x => x.AssociationId).IsUnique().HasFilter("\"IsCurrent\" = true");
      entity.HasOne(x => x.Association).WithMany(x => x.CertificationCycles).HasForeignKey(x => x.AssociationId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.Entity<CertificationParticipant>(entity => {
      entity.ToTable("CertificationParticipant");
      entity.HasQueryFilter(x => !x.CertificationCycle.Association.IsDeleted && !x.Petani.IsDeleted && !x.Petani.Poktan.IsDeleted && !x.Petani.Poktan.Association.IsDeleted);
      entity.HasKey(x => x.Id);
      entity.Property(x => x.StatusReason).HasMaxLength(2000);
      entity.Property(x => x.CertificateEligibilityReason).HasMaxLength(2000);
      entity.HasIndex(x => new { x.CertificationCycleId, x.PetaniId }).IsUnique();
      entity.HasOne(x => x.CertificationCycle).WithMany(x => x.Participants).HasForeignKey(x => x.CertificationCycleId).OnDelete(DeleteBehavior.Cascade);
      entity.HasOne(x => x.Petani).WithMany(x => x.CertificationParticipations).HasForeignKey(x => x.PetaniId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.CertificateEligibilityDecidedByUser).WithMany().HasForeignKey(x => x.CertificateEligibilityDecidedByUserId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.Entity<CertificationParticipantLahan>(entity => {
      entity.ToTable("CertificationParticipantLahan");
      entity.HasQueryFilter(x => !x.CertificationParticipant.CertificationCycle.Association.IsDeleted
          && !x.CertificationParticipant.Petani.IsDeleted && !x.Lahan.IsDeleted);
      entity.HasKey(x => x.Id);
      entity.Property(x => x.StatusReason).HasMaxLength(2000);
      entity.HasIndex(x => new { x.CertificationParticipantId, x.LahanId }).IsUnique();
      entity.HasOne(x => x.CertificationParticipant).WithMany(x => x.Lahan).HasForeignKey(x => x.CertificationParticipantId).OnDelete(DeleteBehavior.Cascade);
      entity.HasOne(x => x.Lahan).WithMany(x => x.CertificationParticipations).HasForeignKey(x => x.LahanId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.Entity<ParticipantStepProgress>(entity => {
      entity.ToTable("ParticipantStepProgress");
      entity.HasQueryFilter(x => !x.CertificationParticipant.CertificationCycle.Association.IsDeleted && !x.CertificationParticipant.Petani.IsDeleted);
      entity.HasKey(x => x.Id);
      entity.Property(x => x.Notes).HasMaxLength(2000);
      entity.HasIndex(x => new { x.CertificationParticipantId, x.Step }).IsUnique();
      entity.HasOne(x => x.CertificationParticipant).WithMany(x => x.StepProgress).HasForeignKey(x => x.CertificationParticipantId).OnDelete(DeleteBehavior.Cascade);
      entity.HasOne(x => x.ResponsibleUser).WithMany().HasForeignKey(x => x.ResponsibleUserId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.Entity<CycleStepProgress>(entity => {
      entity.ToTable("CycleStepProgress");
      entity.HasQueryFilter(x => !x.CertificationCycle.Association.IsDeleted);
      entity.HasKey(x => x.Id);
      entity.Property(x => x.AuditPerformedPercentage).HasPrecision(5, 2);
      entity.HasIndex(x => new { x.CertificationCycleId, x.Step }).IsUnique();
      entity.HasOne(x => x.CertificationCycle).WithMany(x => x.StepProgress).HasForeignKey(x => x.CertificationCycleId).OnDelete(DeleteBehavior.Cascade);
    });
    modelBuilder.Entity<WorkflowTransition>(entity => {
      entity.ToTable("WorkflowTransition");
      entity.HasQueryFilter(x => !x.CertificationCycle.Association.IsDeleted);
      entity.HasKey(x => x.Id);
      entity.Property(x => x.Notes).HasMaxLength(2000);
      entity.HasIndex(x => new { x.CertificationCycleId, x.ChangedAt });
      entity.HasOne(x => x.CertificationCycle).WithMany(x => x.Transitions).HasForeignKey(x => x.CertificationCycleId).OnDelete(DeleteBehavior.Cascade);
      entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.Entity<CycleDocumentRequirement>(entity => {
      entity.ToTable("CycleDocumentRequirement", table => table.HasCheckConstraint("CK_CycleDocumentRequirement_Count", "\"RequiredCount\" > 0"));
      entity.HasQueryFilter(x => !x.CertificationCycle.Association.IsDeleted);
      entity.HasKey(x => x.Id);
      entity.HasIndex(x => new { x.CertificationCycleId, x.DocumentTypeId, x.OwnerType }).IsUnique();
      entity.HasOne(x => x.CertificationCycle).WithMany(x => x.DocumentRequirements).HasForeignKey(x => x.CertificationCycleId).OnDelete(DeleteBehavior.Cascade);
      entity.HasOne(x => x.DocumentType).WithMany().HasForeignKey(x => x.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.Entity<AssociationDocumentSubmission>(entity => {
      entity.ToTable("AssociationDocumentSubmission");
      entity.HasQueryFilter(x => !x.CertificationCycle.Association.IsDeleted && !x.Document.IsDeleted);
      entity.HasKey(x => x.Id);
      entity.Property(x => x.RejectionReason).HasMaxLength(2000);
      entity.HasIndex(x => new { x.CertificationCycleId, x.DocumentTypeId }).IsUnique();
      entity.HasIndex(x => x.DocumentVersionId);
      entity.HasOne(x => x.CertificationCycle).WithMany(x => x.AssociationDocumentSubmissions).HasForeignKey(x => x.CertificationCycleId).OnDelete(DeleteBehavior.Cascade);
      entity.HasOne(x => x.DocumentType).WithMany().HasForeignKey(x => x.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.DocumentVersion).WithMany().HasForeignKey(x => x.DocumentVersionId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.AttachedByUser).WithMany().HasForeignKey(x => x.AttachedByUserId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.SubmittedByUser).WithMany().HasForeignKey(x => x.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.ReviewedByUser).WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.Entity<Disclosure>(entity => {
      entity.ToTable("Disclosure");
      entity.HasQueryFilter(x => !x.CertificationCycle.Association.IsDeleted);
      entity.HasKey(x => x.Id);
      entity.Property(x => x.ReviewNotes).HasMaxLength(2000);
      entity.HasIndex(x => new { x.CertificationCycleId, x.VersionNumber }).IsUnique();
      entity.HasOne(x => x.CertificationCycle).WithMany(x => x.Disclosures).HasForeignKey(x => x.CertificationCycleId).OnDelete(DeleteBehavior.Cascade);
      entity.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.ReviewedByUser).WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.Entity<DisclosureParticipant>(entity => {
      entity.ToTable("DisclosureParticipant");
      entity.HasQueryFilter(x => !x.Disclosure.CertificationCycle.Association.IsDeleted && !x.CertificationParticipant.Petani.IsDeleted);
      entity.HasKey(x => new { x.DisclosureId, x.CertificationParticipantId });
      entity.HasOne(x => x.Disclosure).WithMany(x => x.Participants).HasForeignKey(x => x.DisclosureId).OnDelete(DeleteBehavior.Cascade);
      entity.HasOne(x => x.CertificationParticipant).WithMany().HasForeignKey(x => x.CertificationParticipantId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.Entity<DisclosureLahan>(entity => {
      entity.ToTable("DisclosureLahan");
      entity.HasQueryFilter(x => !x.Disclosure.CertificationCycle.Association.IsDeleted && !x.CertificationParticipantLahan.Lahan.IsDeleted);
      entity.HasKey(x => new { x.DisclosureId, x.CertificationParticipantLahanId });
      entity.HasOne(x => x.Disclosure).WithMany(x => x.Lahan).HasForeignKey(x => x.DisclosureId).OnDelete(DeleteBehavior.Cascade);
      entity.HasOne(x => x.CertificationParticipantLahan).WithMany().HasForeignKey(x => x.CertificationParticipantLahanId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.Entity<TrainingSession>(entity => {
      entity.ToTable("TrainingSession");
      entity.HasQueryFilter(x => !x.CertificationCycle.Association.IsDeleted);
      entity.HasKey(x => x.Id);
      entity.Property(x => x.Title).HasMaxLength(255).IsRequired();
      entity.Property(x => x.Description).HasMaxLength(2000);
      entity.HasOne(x => x.CertificationCycle).WithMany(x => x.TrainingSessions).HasForeignKey(x => x.CertificationCycleId).OnDelete(DeleteBehavior.Cascade);
      entity.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.Entity<TrainingAttendance>(entity => {
      entity.ToTable("TrainingAttendance");
      entity.HasQueryFilter(x => !x.TrainingSession.CertificationCycle.Association.IsDeleted && !x.Petani.IsDeleted);
      entity.HasKey(x => new { x.TrainingSessionId, x.PetaniId });
      entity.Property(x => x.Notes).HasMaxLength(2000);
      entity.HasOne(x => x.TrainingSession).WithMany(x => x.Attendance).HasForeignKey(x => x.TrainingSessionId).OnDelete(DeleteBehavior.Cascade);
      entity.HasOne(x => x.Petani).WithMany(x => x.TrainingAttendance).HasForeignKey(x => x.PetaniId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.Entity<MonitoringRecord>(entity => {
      entity.ToTable("MonitoringRecord");
      entity.HasQueryFilter(x => !x.Petani.IsDeleted && !x.Petani.Poktan.IsDeleted && !x.Petani.Poktan.Association.IsDeleted
          && (x.Lahan == null || !x.Lahan.IsDeleted));
      entity.HasKey(x => x.Id);
      entity.Property(x => x.Notes).HasMaxLength(2000);
      entity.HasIndex(x => new { x.PetaniId, x.LahanId, x.Category, x.MonitoringMonth }).IsUnique().AreNullsDistinct(false);
      entity.HasOne(x => x.Petani).WithMany(x => x.MonitoringRecords).HasForeignKey(x => x.PetaniId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.Lahan).WithMany().HasForeignKey(x => x.LahanId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.ResponsibleUser).WithMany().HasForeignKey(x => x.ResponsibleUserId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.Entity<BaselineAssessment>(entity => {
      entity.ToTable("BaselineAssessment");
      entity.HasQueryFilter(x => !x.Lahan.IsDeleted && !x.Lahan.Petani.IsDeleted
          && !x.Lahan.Petani.Poktan.IsDeleted && !x.Lahan.Petani.Poktan.Association.IsDeleted);
      entity.HasKey(x => x.Id);
      entity.Property(x => x.Result).HasMaxLength(4000);
      entity.Property(x => x.Source).HasMaxLength(1000);
      entity.Property(x => x.Notes).HasMaxLength(2000);
      entity.HasIndex(x => new { x.LahanId, x.Type }).IsUnique();
      entity.HasOne(x => x.Lahan).WithMany(x => x.BaselineAssessments).HasForeignKey(x => x.LahanId).OnDelete(DeleteBehavior.Cascade);
      entity.HasOne(x => x.AssessedByUser).WithMany().HasForeignKey(x => x.AssessedByUserId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.Entity<LandMappingRecord>(entity => {
      entity.ToTable("LandMappingRecord");
      entity.HasQueryFilter(x => !x.CertificationParticipantLahan.CertificationParticipant.CertificationCycle.Association.IsDeleted
          && !x.CertificationParticipantLahan.Lahan.IsDeleted);
      entity.HasKey(x => x.Id);
      entity.Property(x => x.GeoJson).HasColumnType("jsonb");
      entity.Property(x => x.MappedArea).HasPrecision(18, 2);
      entity.Property(x => x.Notes).HasMaxLength(2000);
      entity.HasIndex(x => x.CertificationParticipantLahanId).IsUnique();
      entity.HasOne(x => x.CertificationParticipantLahan).WithOne(x => x.LandMapping)
          .HasForeignKey<LandMappingRecord>(x => x.CertificationParticipantLahanId).OnDelete(DeleteBehavior.Cascade);
      entity.HasOne(x => x.MappedByUser).WithMany().HasForeignKey(x => x.MappedByUserId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.Entity<CertificationAudit>(entity => {
      entity.ToTable("CertificationAudit");
      entity.HasQueryFilter(x => !x.CertificationCycle.Association.IsDeleted);
      entity.HasKey(x => x.Id);
      entity.Property(x => x.ResultNotes).HasMaxLength(4000);
      entity.HasIndex(x => new { x.CertificationCycleId, x.Type }).IsUnique();
      entity.HasOne(x => x.CertificationCycle).WithMany(x => x.Audits).HasForeignKey(x => x.CertificationCycleId).OnDelete(DeleteBehavior.Cascade);
      entity.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.Entity<AuditFinding>(entity => {
      entity.ToTable("AuditFinding");
      entity.HasQueryFilter(x => !x.CertificationAudit.CertificationCycle.Association.IsDeleted);
      entity.HasKey(x => x.Id);
      entity.Property(x => x.Code).HasMaxLength(100).IsRequired();
      entity.Property(x => x.Description).HasMaxLength(4000).IsRequired();
      entity.Property(x => x.CorrectiveAction).HasMaxLength(4000);
      entity.Property(x => x.ClosureEvidence).HasMaxLength(4000);
      entity.HasIndex(x => new { x.CertificationAuditId, x.Code }).IsUnique();
      entity.HasOne(x => x.CertificationAudit).WithMany(x => x.Findings).HasForeignKey(x => x.CertificationAuditId).OnDelete(DeleteBehavior.Cascade);
      entity.HasOne(x => x.ClosedByUser).WithMany().HasForeignKey(x => x.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.Entity<Certificate>(entity => {
      entity.ToTable("Certificate");
      entity.HasQueryFilter(x => !x.CertificationCycle.Association.IsDeleted && !x.Document.IsDeleted);
      entity.HasKey(x => x.Id);
      entity.Property(x => x.Number).HasMaxLength(255).IsRequired();
      entity.Property(x => x.CertificationBody).HasMaxLength(255).IsRequired();
      entity.HasIndex(x => x.Number).IsUnique();
      entity.HasIndex(x => x.DocumentId).IsUnique();
      entity.HasIndex(x => x.CertificationCycleId).IsUnique();
      entity.HasOne(x => x.CertificationCycle).WithMany(x => x.Certificates).HasForeignKey(x => x.CertificationCycleId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.Document).WithOne().HasForeignKey<Certificate>(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
      entity.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
    });
    modelBuilder.Entity<CertificateParticipant>(entity => {
      entity.ToTable("CertificateParticipant");
      entity.HasQueryFilter(x => !x.Certificate.CertificationCycle.Association.IsDeleted && !x.Certificate.Document.IsDeleted);
      entity.HasKey(x => new { x.CertificateId, x.PetaniId });
      entity.Property(x => x.PetaniName).HasMaxLength(255).IsRequired();
      entity.Property(x => x.PoktanName).HasMaxLength(255).IsRequired();
      entity.HasOne(x => x.Certificate).WithMany(x => x.Participants).HasForeignKey(x => x.CertificateId).OnDelete(DeleteBehavior.Cascade);
    });
    modelBuilder.Entity<CertificateLahan>(entity => {
      entity.ToTable("CertificateLahan");
      entity.HasQueryFilter(x => !x.Certificate.CertificationCycle.Association.IsDeleted && !x.Certificate.Document.IsDeleted);
      entity.HasKey(x => new { x.CertificateId, x.LahanId });
      entity.Property(x => x.LegalNumber).HasMaxLength(255);
      entity.Property(x => x.LegalArea).HasPrecision(18, 2);
      entity.HasOne(x => x.Certificate).WithMany(x => x.Lahan).HasForeignKey(x => x.CertificateId).OnDelete(DeleteBehavior.Cascade);
    });
  }
}
