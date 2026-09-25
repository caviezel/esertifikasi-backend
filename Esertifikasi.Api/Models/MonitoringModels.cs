using System.ComponentModel.DataAnnotations;
using Esertifikasi.Api.Domain.Entities;

namespace Esertifikasi.Api.Models;

public abstract class CreateMonitoringSubmissionRequest {
  [NotEmptyGuid] public Guid PoktanId { get; set; }
  public DateOnly PeriodStart { get; set; }
  public DateOnly PeriodEnd { get; set; }
  [StringLength(4000)] public string? Summary { get; set; }
  [StringLength(255)] public string? PreparedByName { get; set; }
  [StringLength(255)] public string? AcknowledgedByName { get; set; }
}

public sealed class CreateLandBoundaryMonitoringRequest : CreateMonitoringSubmissionRequest { public List<LandBoundaryInspection> Inspections { get; set; } = new(); }
public sealed class CreateTurneraMonitoringRequest : CreateMonitoringSubmissionRequest { public List<TurneraInspection> Inspections { get; set; } = new(); }
public sealed class CreateChemicalBufferMonitoringRequest : CreateMonitoringSubmissionRequest { public List<ChemicalBufferInspection> Inspections { get; set; } = new(); }
public sealed class CreateWoodyPlantMonitoringRequest : CreateMonitoringSubmissionRequest { public List<WoodyPlantInspection> Inspections { get; set; } = new(); }
public sealed class CreateFirstAidKitMonitoringRequest : CreateMonitoringSubmissionRequest { public List<FirstAidKitInspection> Inspections { get; set; } = new(); }
public sealed class CreatePpeMonitoringRequest : CreateMonitoringSubmissionRequest { public List<PpeInspection> Inspections { get; set; } = new(); }
public sealed class CreateHighConservationValueMonitoringRequest : CreateMonitoringSubmissionRequest { public List<HcvLocationAssessment> Locations { get; set; } = new(); public List<ProtectedSpeciesObservation> SpeciesObservations { get; set; } = new(); }
public abstract class IncidentMonitoringRequest : CreateMonitoringSubmissionRequest { public bool NoIncidents { get; set; } [StringLength(2000)] public string? ZeroIncidentDeclaration { get; set; } }
public sealed class CreateFireMonitoringRequest : IncidentMonitoringRequest { public List<FireIncident> Incidents { get; set; } = new(); }
public sealed class CreateWorkplaceAccidentMonitoringRequest : IncidentMonitoringRequest { public List<WorkplaceAccidentIncident> Incidents { get; set; } = new(); }
public sealed class CreateWeedMonitoringRequest : CreateMonitoringSubmissionRequest { public List<WeedInspection> Inspections { get; set; } = new(); }
public sealed class CreatePlantDiseaseMonitoringRequest : CreateMonitoringSubmissionRequest { public List<PlantDiseaseInspection> Inspections { get; set; } = new(); }
public sealed class CreatePestMonitoringRequest : CreateMonitoringSubmissionRequest { public List<PestInspection> Inspections { get; set; } = new(); }
public sealed class CreateMemberComplaintMonitoringRequest : CreateMonitoringSubmissionRequest { public bool NoComplaints { get; set; } [StringLength(2000)] public string? ZeroComplaintDeclaration { get; set; } public List<MemberComplaint> Complaints { get; set; } = new(); }

public sealed class MonitoringSubmissionQuery : PagedQuery {
  [NotEmptyGuid] public Guid AssociationId { get; set; }
  public Guid? PoktanId { get; set; }
  public MonitoringType? Type { get; set; }
  public MonitoringSubmissionStatus? Status { get; set; }
  public int? Year { get; set; }
  public Guid? PetaniId { get; set; }
  public Guid? LahanId { get; set; }
}

public sealed class BulkLandBoundaryRow {
  [Required, StringLength(50)] public string Nik { get; set; } = string.Empty;
  [StringLength(100)] public string? LandLegalNumber { get; set; }
  public DateOnly ObservedOn { get; set; }
  public DateOnly? InstalledOn { get; set; }
  [Range(0, int.MaxValue)] public int MarkerCount { get; set; }
  [EnumDataType(typeof(ItemCondition))] public ItemCondition Condition { get; set; }
  [StringLength(2000)] public string? Notes { get; set; }
  [StringLength(2000)] public string? FollowUp { get; set; }
}
public sealed class ImportLandBoundaryRequest {
  [NotEmptyGuid] public Guid PoktanId { get; set; }
  public DateOnly PeriodStart { get; set; }
  public DateOnly PeriodEnd { get; set; }
  [Required, MinLength(1)] public List<BulkLandBoundaryRow> Rows { get; set; } = new();
}
public sealed record LandBoundaryImportPreviewRow(int RowNumber, BulkLandBoundaryRow? Data, List<string> Errors);
public sealed record LandBoundaryImportPreview(bool IsValid, int TotalRows, int ValidRows, IReadOnlyList<LandBoundaryImportPreviewRow> Rows);

public sealed class ReopenMonitoringRequest { [Required, StringLength(2000)] public string Reason { get; set; } = string.Empty; }
public sealed class UpsertMonitoringFollowUpRequest { [Required, StringLength(2000)] public string Description { get; set; } = string.Empty; public FollowUpStatus Status { get; set; } public Guid? AssignedToUserId { get; set; } public DateOnly? DueDate { get; set; } }

public sealed record MonitoringSubmissionListItem(Guid Id, Guid AssociationId, Guid PoktanId, string PoktanName,
    MonitoringType Type, MonitoringGroup Group, DateOnly PeriodStart, DateOnly PeriodEnd,
    MonitoringSubmissionStatus Status, string? Summary, int OpenFollowUps, int Attachments,
    Guid CreatedByUserId, DateTimeOffset CreatedAt, DateTimeOffset? FinalizedAt);

public sealed record MonitoringComplianceItem(MonitoringType Type, string Name, MonitoringFrequency Frequency,
    int Required, int Finalized, int Remaining, string Status);
