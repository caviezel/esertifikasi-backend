using System.ComponentModel.DataAnnotations;
using Esertifikasi.Api.Domain.Entities;
namespace Esertifikasi.Api.Models;
public sealed class RotationRequest {
  [NotEmptyGuid] public Guid LahanId { get; set; }
  [Required, StringLength(255)] public string Name { get; set; } = "";
  public DateOnly StartDate { get; set; }
  public DateOnly EndDate { get; set; }
}
public sealed class FieldLogRequest {
  [NotEmptyGuid] public Guid LahanId { get; set; }
  [EnumDataType(typeof(FieldActivity))] public FieldActivity Activity { get; set; }
  public DateOnly ActivityDate { get; set; }
  public Guid? RotationId { get; set; }
  [StringLength(255)] public string? Buyer { get; set; }
  [StringLength(255)] public string? MaterialName { get; set; }
  [Range(0, 1000000000000d)] public decimal WeightKg { get; set; }
  [Range(0, 1000000000000d)] public decimal UnitPrice { get; set; }
  [Range(0, int.MaxValue)] public int BunchCount { get; set; }
  [Range(0, 1000000000000d)] public decimal Deductions { get; set; }
  [Range(0, 1000000000000d)] public decimal Transport { get; set; }
  [Range(0, int.MaxValue)] public int WorkerCount { get; set; }
  [Range(0, 1000000000000d)] public decimal WagePerPerson { get; set; }
  [Range(0, int.MaxValue)] public int TreeCount { get; set; }
  [Range(0, 1000000d)] public decimal DosePerTreeKg { get; set; }
  [Range(0, 1000000d)] public decimal TreatedAreaHa { get; set; }
  [Range(0, 1000000d)] public decimal DoseLitersPerHa { get; set; }
  [Range(0, 1000000000000d)] public decimal WagePerTree { get; set; }
  [StringLength(2000)] public string? Notes { get; set; }
}
public sealed class FieldLogQuery : PagedQuery {
  public Guid AssociationId { get; set; }
  public Guid? PoktanId { get; set; }
  public Guid? LahanId { get; set; }
  public FieldActivity? Activity { get; set; }
  public int? Year { get; set; }
  public Guid? RotationId { get; set; }
  public MonitoringSubmissionStatus? Status { get; set; }
}
public sealed class AssociationTrainingRequest {
  [Required, StringLength(255)] public string Title { get; set; } = "";
  public Guid? PoktanId { get; set; }
  public DateOnly StartDate { get; set; }
  public DateOnly EndDate { get; set; }
  [Required, MinLength(1)] public DateOnly[] Days { get; set; } = [];
  [Required] public Guid[] PetaniIds { get; set; } = [];
  [Required] public Guid[] TopicIds { get; set; } = [];
  [Required] public Guid[] PackageIds { get; set; } = [];
}
public sealed class TrainingQuery : PagedQuery {
  public DateOnly? From { get; set; }
  public DateOnly? To { get; set; }
  public string? Status { get; set; }
}
public sealed class DailyAttendanceRequest { [Required] public bool? Present { get; set; } [StringLength(2000)] public string? Notes { get; set; } }
public sealed class NameRequest { [Required, StringLength(255)] public string Name { get; set; } = ""; }
public sealed class PackageRequest { [Required, StringLength(255)] public string Name { get; set; } = ""; [Required, MinLength(1)] public Guid[] TopicIds { get; set; } = []; }
