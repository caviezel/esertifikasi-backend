using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Esertifikasi.Api.IntegrationTests.Fixtures;

public sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions> {
  public const string SchemeName = "IntegrationTest";
  public const string UserIdHeader = "X-Test-UserId";
  public const string RolesHeader = "X-Test-Roles";

  public TestAuthenticationHandler(
      IOptionsMonitor<AuthenticationSchemeOptions> options,
      ILoggerFactory logger,
      UrlEncoder encoder) : base(options, logger, encoder) { }

  protected override Task<AuthenticateResult> HandleAuthenticateAsync() {
    if (!Request.Headers.TryGetValue(UserIdHeader, out var userId)
        || !Guid.TryParse(userId.ToString(), out _)) {
      return Task.FromResult(AuthenticateResult.NoResult());
    }

    var claims = new List<Claim> {
      new(ClaimTypes.NameIdentifier, userId.ToString())
    };
    if (Request.Headers.TryGetValue(RolesHeader, out var roles)) {
      claims.AddRange(roles.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
          .Select(role => new Claim(ClaimTypes.Role, role)));
    }

    var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
    return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
  }
}
