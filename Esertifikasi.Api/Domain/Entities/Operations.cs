using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
namespace Esertifikasi.Api.Domain.Entities;

public enum FieldActivity { Harvest, Fertilizing, Spraying, Pruning }
public sealed class HarvestRotation {
  public Guid Id { get; set; }
  public Guid LahanId { get; set; }
  [JsonIgnore] public Lahan Lahan { get; set; } = null!;
  public string Name { get; set; } = "";
  public DateOnly StartDate { get; set; }
  public DateOnly EndDate { get; set; }
}
public sealed class FieldLog {
  public Guid Version { get; set; } = Guid.NewGuid();
  public Guid Id { get; set; }
  public Guid LahanId { get; set; }
  [JsonIgnore] public Lahan Lahan { get; set; } = null!;
  public FieldActivity Activity { get; set; }
  public DateOnly ActivityDate { get; set; }
  public int SequenceNumber { get; set; }
  public Guid? RotationId { get; set; }
  [JsonIgnore] public HarvestRotation? Rotation { get; set; }
  public string FarmerNameSnapshot { get; set; } = "";
  public string NikSnapshot { get; set; } = "";
  public string? LandLegalNumberSnapshot { get; set; }
  public decimal? LandAreaHaSnapshot { get; set; }
  public string? Buyer { get; set; }
  public string? MaterialName { get; set; }
  public decimal WeightKg { get; set; }
  public decimal UnitPrice { get; set; }
  public int BunchCount { get; set; }
  public decimal Deductions { get; set; }
  public decimal Transport { get; set; }
  public int WorkerCount { get; set; }
  public decimal WagePerPerson { get; set; }
  public int TreeCount { get; set; }
  public decimal DosePerTreeKg { get; set; }
  public decimal TreatedAreaHa { get; set; }
  public decimal DoseLitersPerHa { get; set; }
  public decimal WagePerTree { get; set; }
  public string? Notes { get; set; }
  public MonitoringSubmissionStatus Status { get; set; }
  public string? ReopenReason { get; set; }
  public Guid CreatedByUserId { get; set; }
  public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
  public bool IsDeleted { get; set; }
  public DateTimeOffset? DeletedAt { get; set; }
  [NotMapped] public decimal MaterialQuantity => Activity == FieldActivity.Fertilizing ? TreeCount * DosePerTreeKg : Activity == FieldActivity.Spraying ? TreatedAreaHa * DoseLitersPerHa : 0;
  [NotMapped] public decimal MaterialCost => MaterialQuantity * UnitPrice;
  [NotMapped] public decimal WorkerCost => WorkerCount * WagePerPerson;
  [NotMapped] public decimal GrossSales => Activity == FieldActivity.Harvest ? WeightKg * UnitPrice : 0;
  [NotMapped] public decimal TotalCost => Activity switch { FieldActivity.Harvest => Deductions + Transport + WorkerCost, FieldActivity.Pruning => TreeCount * WagePerTree, _ => MaterialCost + WorkerCost };
  [NotMapped] public decimal NetIncome => Activity == FieldActivity.Harvest ? GrossSales - TotalCost : 0;
}
public sealed class ActivityCounter {
  public Guid LahanId { get; set; }
  public FieldActivity Activity { get; set; }
  public int Year { get; set; }
  public int LastNumber { get; set; }
  public Guid Version { get; set; }
}
public sealed class AssociationTraining {
  public Guid Version { get; set; } = Guid.NewGuid();
  public Guid Id { get; set; }
  public Guid AssociationId { get; set; }
  public Association Association { get; set; } = null!;
  public Guid? LegacySessionId { get; set; }
  public Guid? OriginalCertificationCycleId { get; set; }
  public Guid? PoktanId { get; set; }
  public Poktan? Poktan { get; set; }
  public string Title { get; set; } = "";
  public string? LegacyDescription { get; set; }
  public DateOnly StartDate { get; set; }
  public DateOnly EndDate { get; set; }
  public DateTimeOffset? CompletedAt { get; set; }
  public string? ReopenReason { get; set; }
  public Guid CreatedByUserId { get; set; }
  public bool IsDeleted { get; set; }
  public ICollection<AssociationTrainingDay> Days { get; set; } = new List<AssociationTrainingDay>();
  public ICollection<AssociationTrainingParticipant> Participants { get; set; } = new List<AssociationTrainingParticipant>();
  public ICollection<AssociationTrainingTopic> Topics { get; set; } = new List<AssociationTrainingTopic>();
}
public sealed class AssociationTrainingDay { public Guid TrainingId { get; set; } public AssociationTraining Training { get; set; } = null!; public DateOnly Date { get; set; } }
public sealed class AssociationTrainingParticipant { public Guid TrainingId { get; set; } public AssociationTraining Training { get; set; } = null!; public Guid PetaniId { get; set; } public Petani Petani { get; set; } = null!; }
public sealed class DailyTrainingAttendance { public Guid TrainingId { get; set; } public AssociationTraining Training { get; set; } = null!; public Guid PetaniId { get; set; } public DateOnly Date { get; set; } public bool Present { get; set; } public string? Notes { get; set; } }
public sealed class TrainingTopic { public Guid Id { get; set; } public Guid? AssociationId { get; set; } public string Name { get; set; } = ""; }
public sealed class TrainingPackage { public Guid Id { get; set; } public string Name { get; set; } = ""; public ICollection<TrainingPackageTopic> Topics { get; set; } = new List<TrainingPackageTopic>(); }
public sealed class TrainingPackageTopic { public Guid PackageId { get; set; } public TrainingPackage Package { get; set; } = null!; public Guid TopicId { get; set; } public TrainingTopic Topic { get; set; } = null!; }
public sealed class AssociationTrainingTopic { public Guid TrainingId { get; set; } public AssociationTraining Training { get; set; } = null!; public Guid TopicId { get; set; } public TrainingTopic Topic { get; set; } = null!; }
public sealed class FirstAidLocation { public Guid Id { get; set; } public Guid AssociationId { get; set; } public string Name { get; set; } = ""; public bool IsActive { get; set; } = true; }
