namespace Esertifikasi.Api.Security;

public sealed class JwtOptions {
  public const string SectionName = "Jwt";
  public string Issuer { get; set; } = "Esertifikasi.Api";
  public string Audience { get; set; } = "Esertifikasi.Client";
  public string Key { get; set; } = string.Empty;
  public int AccessTokenMinutes { get; set; } = 15;
  public int RefreshTokenDays { get; set; } = 30;
}
