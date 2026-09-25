namespace Esertifikasi.Api.Services;

public interface ITaniBaikVerifier {
  Task<bool> VerifyAsync(string providerSubjectId, CancellationToken ct);
}

public sealed class TaniBaikVerifier : ITaniBaikVerifier {
  private readonly IConfiguration _configuration;
  private readonly IWebHostEnvironment _environment;

  public TaniBaikVerifier(IConfiguration configuration, IWebHostEnvironment environment) {
    _configuration = configuration;
    _environment = environment;
  }

  public Task<bool> VerifyAsync(string providerSubjectId, CancellationToken ct) {
    // Replace with Tani Baik OIDC/API validation when its contract is available.
    var allowed = _environment.IsDevelopment() && _configuration.GetValue<bool>("TaniBaik:AllowUnverifiedDevelopmentRegistrations");
    return Task.FromResult(allowed && !string.IsNullOrWhiteSpace(providerSubjectId));
  }
}
