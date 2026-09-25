using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.IntegrationTests.Fixtures;
using Esertifikasi.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Esertifikasi.Api.IntegrationTests.Authorization;

public sealed class AuthCookieTests : IClassFixture<EsertifikasiWebApplicationFactory>, IAsyncLifetime {
  private const string Password = "IntegrationTest@2026!";
  private readonly EsertifikasiWebApplicationFactory _factory;

  public AuthCookieTests(EsertifikasiWebApplicationFactory factory) {
    _factory = factory;
  }

  public async Task InitializeAsync() {
    await _factory.SeedAsync();

    using var scope = _factory.Services.CreateScope();
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var user = await users.FindByIdAsync(TestIds.SuperAdmin.ToString());

    if (!await roles.RoleExistsAsync(AppRoles.SuperAdmin)) {
      Assert.True((await roles.CreateAsync(new IdentityRole<Guid>(AppRoles.SuperAdmin))).Succeeded);
    }

    if (!await users.HasPasswordAsync(user!)) {
      Assert.True((await users.AddPasswordAsync(user!, Password)).Succeeded);
    }

    if (!await users.IsInRoleAsync(user!, AppRoles.SuperAdmin)) {
      Assert.True((await users.AddToRoleAsync(user!, AppRoles.SuperAdmin)).Succeeded);
    }
  }

  public Task DisposeAsync() => Task.CompletedTask;

  [Fact]
  public async Task LoginSetsProtectedRefreshCookieWithoutReturningRefreshToken() {
    using var client = CreateClientWithoutCookieStorage();

    var response = await LoginAsync(client);
    var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var setCookie = GetSetCookie(response);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.True(json.RootElement.TryGetProperty("accessToken", out _));
    Assert.True(json.RootElement.TryGetProperty("expiresAt", out _));
    Assert.False(json.RootElement.TryGetProperty("refreshToken", out _));
    Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("path=/api/auth", setCookie, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public async Task RefreshRotatesCookieAndLogoutRevokesIt() {
    using var client = CreateClientWithoutCookieStorage();
    var login = await LoginAsync(client);
    var firstToken = GetCookieValue(login);

    var refresh = await PostWithCookieAsync(client, "/api/auth/refresh", firstToken);
    var secondToken = GetCookieValue(refresh);

    Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
    Assert.NotEqual(firstToken, secondToken);

    var replay = await PostWithCookieAsync(client, "/api/auth/refresh", firstToken);
    Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

    var logout = await PostWithCookieAsync(client, "/api/auth/logout", secondToken);
    Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
    Assert.Contains("expires=thu, 01 jan 1970", GetSetCookie(logout), StringComparison.OrdinalIgnoreCase);

    var afterLogout = await PostWithCookieAsync(client, "/api/auth/refresh", secondToken);
    Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
  }

  [Fact]
  public async Task RefreshDoesNotAcceptTokenFromJsonBody() {
    using var client = CreateClientWithoutCookieStorage();
    var login = await LoginAsync(client);
    var refreshToken = GetCookieValue(login);

    var response = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task MeReturnsCurrentUserAndRoles() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();

    var response = await client.GetAsync("/api/auth/me");
    var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(TestIds.SuperAdmin, json.RootElement.GetProperty("id").GetGuid());
    Assert.Contains(
        json.RootElement.GetProperty("roles").EnumerateArray(),
        role => role.GetString() == AppRoles.SuperAdmin);
  }

  private HttpClient CreateClientWithoutCookieStorage() {
    return _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
  }

  private static Task<HttpResponseMessage> LoginAsync(HttpClient client) {
    return client.PostAsJsonAsync("/api/auth/login", new {
      email = "superadmin@test.local",
      password = Password
    });
  }

  private static Task<HttpResponseMessage> PostWithCookieAsync(HttpClient client, string path, string token) {
    var request = new HttpRequestMessage(HttpMethod.Post, path);
    request.Headers.Add("Cookie", $"{RefreshTokenCookie.Name}={token}");

    return client.SendAsync(request);
  }

  private static string GetCookieValue(HttpResponseMessage response) {
    var setCookie = GetSetCookie(response);
    var prefix = $"{RefreshTokenCookie.Name}=";
    var cookie = setCookie.Split(';', 2)[0];

    Assert.StartsWith(prefix, cookie, StringComparison.Ordinal);

    return cookie[prefix.Length..];
  }

  private static string GetSetCookie(HttpResponseMessage response) {
    return Assert.Single(response.Headers.GetValues("Set-Cookie"));
  }
}
