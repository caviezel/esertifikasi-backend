using Esertifikasi.Api.Domain.Entities;

namespace Esertifikasi.Api.IntegrationTests.Fixtures;

public static class HttpClientExtensions {
  public static void AuthenticateAs(this HttpClient client, Guid userId, params string[] roles) {
    client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.UserIdHeader);
    client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.RolesHeader);
    client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeader, userId.ToString());
    if (roles.Length > 0) client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RolesHeader, string.Join(',', roles));
  }

  public static void AuthenticateAsSuperAdmin(this HttpClient client) {
    client.AuthenticateAs(TestIds.SuperAdmin, AppRoles.SuperAdmin);
  }
}
