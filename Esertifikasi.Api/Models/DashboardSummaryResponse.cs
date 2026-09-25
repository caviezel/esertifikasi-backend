namespace Esertifikasi.Api.Models;

public sealed record DashboardSummaryResponse(
    int TotalAssociations,
    int TotalPoktan,
    int TotalPetani,
    int TotalLahan,
    decimal TotalLuasLahan);
