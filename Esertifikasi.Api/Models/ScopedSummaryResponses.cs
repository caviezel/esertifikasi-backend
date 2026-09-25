namespace Esertifikasi.Api.Models;

using Esertifikasi.Api.Domain.Entities;

public sealed record AssociationSummaryResponse(
    int TotalPoktan,
    int TotalPetani,
    int TotalLahan,
    decimal TotalLuasLahan);

public sealed record PoktanSummaryResponse(
    int TotalPetani,
    int TotalLahan,
    decimal TotalLuasLahan);

public sealed record PetaniSummaryResponse(
    int TotalLahan,
    decimal TotalLuasLahan);

public sealed record LahanSummaryResponse(
    bool BoundaryAvailable,
    BaselineSummaryStatus BaselineStatus,
    bool IsCertified,
    int RequiredDocuments,
    int UploadedDocuments,
    int VerifiedDocuments,
    DocumentCompletionStatus DocumentsStatus);

public sealed record PetaniLahanListItem(
    Guid Id,
    Guid PetaniId,
    string? NoLegalitas,
    string? Komoditas,
    decimal? LuasLegalitas,
    bool BoundaryAvailable,
    BaselineSummaryStatus BaselineStatus,
    bool IsCertified,
    int RequiredDocuments,
    int UploadedDocuments,
    int VerifiedDocuments,
    DocumentCompletionStatus DocumentsStatus);

public enum DocumentCompletionStatus { Incomplete, Complete, Verified }

public static class LahanSummaryRules {
  public static BaselineSummaryStatus BaselineStatus(
      bool isCertified, bool boundaryAvailable, int assessmentCount, int completedChecks, int requiredChecks) =>
      isCertified ? BaselineSummaryStatus.Certified
      : assessmentCount == 0 ? BaselineSummaryStatus.NotAssessed
      : boundaryAvailable && completedChecks == requiredChecks ? BaselineSummaryStatus.Complete
      : BaselineSummaryStatus.Incomplete;

  public static DocumentCompletionStatus DocumentsStatus(
      int requiredDocuments, int uploadedDocuments, int verifiedDocuments) =>
      requiredDocuments > 0 && verifiedDocuments >= requiredDocuments ? DocumentCompletionStatus.Verified
      : requiredDocuments > 0 && uploadedDocuments >= requiredDocuments ? DocumentCompletionStatus.Complete
      : DocumentCompletionStatus.Incomplete;
}

public sealed class CertificationDatabaseQuery : PagedQuery {
  public Guid? PoktanId { get; set; }
}

public sealed class CertificationDatabaseLahanQuery : PagedQuery {
  public Guid? PoktanId { get; set; }
  public LahanParticipationStatus? ParticipationStatus { get; set; }
  public ProgressStatus? MappingStatus { get; set; }
  public bool? IncludedInLatestDisclosure { get; set; }
  public bool? BoundaryAvailable { get; set; }
}

public sealed record CertificationDatabaseLahanFlatItem(
    Guid LahanId, Guid PetaniId, string PetaniName, Guid PoktanId, string PoktanName,
    string? LegalNumber, string? Commodity, decimal? LegalArea,
    LahanParticipationStatus? ParticipationStatus, ProgressStatus? MappingStatus,
    int BaselineCompleted, int BaselineRequired, int UploadedDocuments, int VerifiedDocuments,
    int RequiredDocuments, bool IncludedInLatestDisclosure, bool BoundaryAvailable,
    BaselineSummaryStatus BaselineStatus, bool IsCertified, DocumentCompletionStatus DocumentsStatus);

public sealed record CertificationDatabaseLahanPage(
    Guid? CurrentCycleId, CertificationCycleType? CurrentCycleType,
    IReadOnlyList<CertificationDatabaseLahanFlatItem> Items,
    int Page, int PageSize, int TotalCount, int TotalPages);

public sealed record CertificationDatabaseMapProperties(
    Guid PetaniId, string PetaniName, Guid PoktanId, string PoktanName,
    string? LegalNumber, string? Commodity, decimal? LegalArea,
    LahanParticipationStatus? ParticipationStatus, ProgressStatus? MappingStatus);

public sealed record GeoJsonFeature(
    string Type, Guid Id, System.Text.Json.JsonElement Geometry,
    CertificationDatabaseMapProperties Properties);

public sealed record CertificationDatabaseLahanMapResponse(
    string Type, Guid? CurrentCycleId, CertificationCycleType? CurrentCycleType,
    IReadOnlyList<GeoJsonFeature> Features, int BoundaryUnavailableCount);

public sealed record CertificationDatabaseLahanProjection(
    Guid LahanId, Guid PetaniId, string PetaniName, Guid PoktanId, string PoktanName,
    string? LegalNumber, string? Commodity, decimal? LegalArea,
    LahanParticipationStatus? ParticipationStatus, ProgressStatus? MappingStatus,
    int BaselineCompleted, int BaselineAssessmentCount, bool IsCertified,
    bool IncludedInLatestDisclosure, int UploadedDocuments, int VerifiedDocuments, string? BoundaryGeoJson);

public sealed record CertificationDatabaseLahanItem(
    Guid LahanId, string? LegalNumber, string? Commodity, decimal? LegalArea,
    Guid? ParticipantLahanId, LahanParticipationStatus? ParticipationStatus,
    ProgressStatus? MappingStatus, int BaselineCompleted, int BaselineRequired,
    bool IncludedInLatestDisclosure, int RequiredDocuments, int VerifiedDocuments);

public sealed record CertificationDatabasePetaniItem(
    Guid PetaniId, string PetaniName, string? Nik, Guid PoktanId, string PoktanName,
    Guid? ParticipantId, ParticipantEntryPath? EntryPath, ParticipationStatus? ParticipationStatus,
    int RequiredDocuments, int VerifiedDocuments,
    IReadOnlyList<CertificationDatabaseLahanItem> Lahan);
