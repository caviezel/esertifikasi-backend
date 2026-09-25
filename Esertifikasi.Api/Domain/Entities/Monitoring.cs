namespace Esertifikasi.Api.Domain.Entities;

public sealed class MonitoringSubmission {
  public Guid Id { get; set; }
  public Guid AssociationId { get; set; }
  public Association Association { get; set; } = null!;
  public Guid PoktanId { get; set; }
  public Poktan Poktan { get; set; } = null!;
  public MonitoringType Type { get; set; }
  public MonitoringGroup Group { get; set; }
  public DateOnly PeriodStart { get; set; }
  public DateOnly PeriodEnd { get; set; }
  public MonitoringSubmissionStatus Status { get; set; } = MonitoringSubmissionStatus.Draft;
  public string? Summary { get; set; }
  public Guid CreatedByUserId { get; set; }
  public ApplicationUser CreatedByUser { get; set; } = null!;
  public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
  public Guid? FinalizedByUserId { get; set; }
  public ApplicationUser? FinalizedByUser { get; set; }
  public DateTimeOffset? FinalizedAt { get; set; }
  public Guid? ReopenedByUserId { get; set; }
  public ApplicationUser? ReopenedByUser { get; set; }
  public DateTimeOffset? ReopenedAt { get; set; }
  public string? ReopenReason { get; set; }
  public string? PreparedByName { get; set; }
  public string? AcknowledgedByName { get; set; }
  public ICollection<MonitoringFollowUp> FollowUps { get; set; } = new List<MonitoringFollowUp>();
  public ICollection<MonitoringAttachment> Attachments { get; set; } = new List<MonitoringAttachment>();
}

public sealed class MonitoringDefinition {
  public MonitoringType Type { get; set; }
  public string Name { get; set; } = string.Empty;
  public MonitoringGroup Group { get; set; }
  public MonitoringFrequency Frequency { get; set; }
  public int RequiredOccurrencesPerYear { get; set; }
  public bool IsActive { get; set; } = true;
}

public sealed class MonitoringFollowUp {
  public Guid Id { get; set; }
  public Guid MonitoringSubmissionId { get; set; }
  public MonitoringSubmission MonitoringSubmission { get; set; } = null!;
  public string Description { get; set; } = string.Empty;
  public FollowUpStatus Status { get; set; } = FollowUpStatus.Open;
  public Guid? AssignedToUserId { get; set; }
  public ApplicationUser? AssignedToUser { get; set; }
  public DateOnly? DueDate { get; set; }
  public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class MonitoringAttachment {
  public Guid Id { get; set; }
  public Guid MonitoringSubmissionId { get; set; }
  public MonitoringSubmission MonitoringSubmission { get; set; } = null!;
  public string StorageKey { get; set; } = string.Empty;
  public string OriginalFileName { get; set; } = string.Empty;
  public string ContentType { get; set; } = string.Empty;
  public string FileExtension { get; set; } = string.Empty;
  public long FileSize { get; set; }
  public string Sha256Hash { get; set; } = string.Empty;
  public Guid UploadedByUserId { get; set; }
  public ApplicationUser UploadedByUser { get; set; } = null!;
  public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class MonitoringReferenceItem {
  public Guid Id { get; set; }
  public MonitoringReferenceKind Kind { get; set; }
  public string Code { get; set; } = string.Empty;
  public string Name { get; set; } = string.Empty;
  public string? Description { get; set; }
  public bool IsActive { get; set; } = true;
}

public abstract class MonitoringDetailBase {
  public Guid MonitoringSubmissionId { get; set; }
  public MonitoringSubmission MonitoringSubmission { get; set; } = null!;
}

public abstract class FarmerLandMonitoringRow {
  public Guid Id { get; set; }
  public Guid? PetaniId { get; set; }
  public Petani? Petani { get; set; }
  public Guid? LahanId { get; set; }
  public Lahan? Lahan { get; set; }
  public string? FarmerNameSnapshot { get; set; }
  public string? NikSnapshot { get; set; }
  public string? LandLegalNumberSnapshot { get; set; }
  public decimal? LandAreaSnapshot { get; set; }
  public DateOnly ObservedOn { get; set; }
  public string? Notes { get; set; }
  public string? FollowUp { get; set; }
}

public sealed class LandBoundaryMonitoring : MonitoringDetailBase { public ICollection<LandBoundaryInspection> Inspections { get; set; } = new List<LandBoundaryInspection>(); }
public sealed class LandBoundaryInspection : FarmerLandMonitoringRow { public Guid MonitoringSubmissionId { get; set; } public DateOnly? InstalledOn { get; set; } public int MarkerCount { get; set; } public ItemCondition Condition { get; set; } }

public sealed class TurneraMonitoring : MonitoringDetailBase { public ICollection<TurneraInspection> Inspections { get; set; } = new List<TurneraInspection>(); }
public sealed class TurneraInspection : FarmerLandMonitoringRow { public Guid MonitoringSubmissionId { get; set; } public TurneraCondition Condition { get; set; } public string? Description { get; set; } }

public sealed class ChemicalBufferMonitoring : MonitoringDetailBase { public ICollection<ChemicalBufferInspection> Inspections { get; set; } = new List<ChemicalBufferInspection>(); }
public sealed class ChemicalBufferInspection : FarmerLandMonitoringRow { public Guid MonitoringSubmissionId { get; set; } public bool HasRiverBoundaryMarker { get; set; } public bool NoChemicalActivityWithinFiveMeters { get; set; } public bool HasWoodyPlantsWithinFiveMeters { get; set; } public bool NoPlantingOnSteepSlope { get; set; } }

public sealed class WoodyPlantMonitoring : MonitoringDetailBase { public ICollection<WoodyPlantInspection> Inspections { get; set; } = new List<WoodyPlantInspection>(); }
public sealed class WoodyPlantInspection : FarmerLandMonitoringRow { public Guid MonitoringSubmissionId { get; set; } public ICollection<WoodyPlantObservation> Observations { get; set; } = new List<WoodyPlantObservation>(); }
public sealed class WoodyPlantObservation { public Guid Id { get; set; } public Guid WoodyPlantInspectionId { get; set; } public string TreeName { get; set; } = string.Empty; public int Quantity { get; set; } public decimal? HeightCentimeters { get; set; } public string? DamageSymptoms { get; set; } public string? Remarks { get; set; } }

public sealed class FirstAidKitMonitoring : MonitoringDetailBase { public ICollection<FirstAidKitInspection> Inspections { get; set; } = new List<FirstAidKitInspection>(); }
public sealed class FirstAidKitInspection { public Guid Id { get; set; } public Guid MonitoringSubmissionId { get; set; } public string Location { get; set; } = string.Empty; public DateOnly ObservedOn { get; set; } public string? Notes { get; set; } public ICollection<FirstAidKitItemInspection> Items { get; set; } = new List<FirstAidKitItemInspection>(); }
public sealed class FirstAidKitItemInspection { public Guid Id { get; set; } public Guid FirstAidKitInspectionId { get; set; } public Guid? ReferenceItemId { get; set; } public string ItemName { get; set; } = string.Empty; public ItemCondition Condition { get; set; } public string? Notes { get; set; } public string? FollowUp { get; set; } }

public sealed class PpeMonitoring : MonitoringDetailBase { public ICollection<PpeInspection> Inspections { get; set; } = new List<PpeInspection>(); }
public sealed class PpeInspection : FarmerLandMonitoringRow { public Guid MonitoringSubmissionId { get; set; } public WorkActivity Activity { get; set; } public ICollection<PpeItemInspection> Items { get; set; } = new List<PpeItemInspection>(); }
public sealed class PpeItemInspection { public Guid Id { get; set; } public Guid PpeInspectionId { get; set; } public Guid? ReferenceItemId { get; set; } public string ItemName { get; set; } = string.Empty; public bool IsAvailable { get; set; } public bool IsUsed { get; set; } public ItemCondition? Condition { get; set; } public string? Notes { get; set; } }

public sealed class HighConservationValueMonitoring : MonitoringDetailBase { public ICollection<HcvLocationAssessment> Locations { get; set; } = new List<HcvLocationAssessment>(); public ICollection<ProtectedSpeciesObservation> SpeciesObservations { get; set; } = new List<ProtectedSpeciesObservation>(); }
public sealed class HcvLocationAssessment : FarmerLandMonitoringRow { public Guid MonitoringSubmissionId { get; set; } public int Semester { get; set; } public string Location { get; set; } = string.Empty; }
public sealed class ProtectedSpeciesObservation : FarmerLandMonitoringRow { public Guid MonitoringSubmissionId { get; set; } public Guid? ReferenceItemId { get; set; } public SpeciesKind SpeciesKind { get; set; } public string SpeciesName { get; set; } = string.Empty; public string? Description { get; set; } }

public sealed class FireMonitoring : MonitoringDetailBase { public bool NoIncidents { get; set; } public string? ZeroIncidentDeclaration { get; set; } public ICollection<FireIncident> Incidents { get; set; } = new List<FireIncident>(); }
public sealed class FireIncident : FarmerLandMonitoringRow { public Guid MonitoringSubmissionId { get; set; } public IncidentSeverity Severity { get; set; } public string Chronology { get; set; } = string.Empty; }

public sealed class WorkplaceAccidentMonitoring : MonitoringDetailBase { public bool NoIncidents { get; set; } public string? ZeroIncidentDeclaration { get; set; } public ICollection<WorkplaceAccidentIncident> Incidents { get; set; } = new List<WorkplaceAccidentIncident>(); }
public sealed class WorkplaceAccidentIncident : FarmerLandMonitoringRow { public Guid MonitoringSubmissionId { get; set; } public AccidentCategory Category { get; set; } public int CaseCount { get; set; } public string Chronology { get; set; } = string.Empty; }

public sealed class WeedMonitoring : MonitoringDetailBase { public ICollection<WeedInspection> Inspections { get; set; } = new List<WeedInspection>(); }
public sealed class WeedInspection : FarmerLandMonitoringRow { public Guid MonitoringSubmissionId { get; set; } public Guid? ReferenceItemId { get; set; } public string WeedType { get; set; } = string.Empty; public string? Result { get; set; } public string? Treatment { get; set; } }

public sealed class PlantDiseaseMonitoring : MonitoringDetailBase { public ICollection<PlantDiseaseInspection> Inspections { get; set; } = new List<PlantDiseaseInspection>(); }
public sealed class PlantDiseaseInspection : FarmerLandMonitoringRow { public Guid MonitoringSubmissionId { get; set; } public Guid? ReferenceItemId { get; set; } public string DiseaseType { get; set; } = string.Empty; public string? Result { get; set; } public string? Treatment { get; set; } }

public sealed class PestMonitoring : MonitoringDetailBase { public ICollection<PestInspection> Inspections { get; set; } = new List<PestInspection>(); }
public sealed class PestInspection : FarmerLandMonitoringRow { public Guid MonitoringSubmissionId { get; set; } public Guid? ReferenceItemId { get; set; } public string PestType { get; set; } = string.Empty; public PestSeverity Severity { get; set; } public decimal? ObservedDensity { get; set; } public string? DensityUnit { get; set; } public string? Treatment { get; set; } }

public sealed class MemberComplaintMonitoring : MonitoringDetailBase { public bool NoComplaints { get; set; } public string? ZeroComplaintDeclaration { get; set; } public ICollection<MemberComplaint> Complaints { get; set; } = new List<MemberComplaint>(); }
public sealed class MemberComplaint { public Guid Id { get; set; } public Guid MonitoringSubmissionId { get; set; } public Guid? PetaniId { get; set; } public Petani? Petani { get; set; } public string? FarmerNameSnapshot { get; set; } public string? NikSnapshot { get; set; } public DateOnly ReceivedOn { get; set; } public string ComplaintType { get; set; } = string.Empty; public string Description { get; set; } = string.Empty; public string? FollowUp { get; set; } public ComplaintStatus Status { get; set; } public DateOnly? ResolvedOn { get; set; } public bool IsAnonymous { get; set; } public bool IsConfidential { get; set; } }

public enum MonitoringType { LandBoundaryMarker, Turnera, ChemicalBufferBoundary, WoodyPlantAndErosionControl, FirstAidKit, PersonalProtectiveEquipment, HighConservationValue, FireIncident, WorkplaceAccident, Weed, PlantDisease, Pest, MemberComplaint }
public enum MonitoringGroup { Budidaya, Ics, Environment, OccupationalSafety, Social }
public enum MonitoringFrequency { Annual, SemiAnnual }
public enum MonitoringSubmissionStatus { Draft, Finalized, Reopened }
public enum FollowUpStatus { Open, InProgress, Completed, Cancelled }
public enum MonitoringReferenceKind { Tree, Weed, Disease, Pest, ProtectedAnimal, ProtectedPlant, PpeItem, FirstAidItem }
public enum ItemCondition { Good, Damaged, Missing }
public enum TurneraCondition { Good, Dead }
public enum WorkActivity { Harvesting, Spraying, Pruning, Fertilizing, Other }
public enum SpeciesKind { Animal, Plant }
public enum IncidentSeverity { Light, Moderate, Severe }
public enum AccidentCategory { Insignificant, Minor, Moderate, Major, Disaster }
public enum PestSeverity { None, Light, Moderate, Severe }
public enum ComplaintStatus { Open, InProgress, Resolved, Rejected }
