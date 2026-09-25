using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.IntegrationTests.Fixtures;
using Esertifikasi.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Esertifikasi.Api.IntegrationTests.Authorization;

public sealed class AdminUserManagementTests : IClassFixture<EsertifikasiWebApplicationFactory>, IAsyncLifetime {
  private const string Password = "IntegrationTest@2026!";
  private readonly EsertifikasiWebApplicationFactory _factory;

  public AdminUserManagementTests(EsertifikasiWebApplicationFactory factory) {
    _factory = factory;
  }

  public Task InitializeAsync() => _factory.SeedAsync();

  public Task DisposeAsync() => Task.CompletedTask;

  [Fact]
  public async Task Roles_Get_ReturnsFourManageableRolesOnly() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();

    var response = await client.GetAsync("/api/admin/roles");
    var roles = (await response.Content.ReadFromJsonAsync<string[]>())!;

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(4, roles.Length);
    Assert.Contains(AppRoles.SuperAdmin, roles);
    Assert.Contains(AppRoles.AssociationAdmin, roles);
    Assert.Contains(AppRoles.PoktanAdmin, roles);
    Assert.Contains(AppRoles.IcsAuditor, roles);
    Assert.DoesNotContain(AppRoles.MemberTaniBaik, roles);
  }

  [Fact]
  public async Task Create_AllowsSuperAdminRole() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();

    var email = $"{Guid.NewGuid():N}@test.local";
    var createResponse = await client.PostAsJsonAsync("/api/admin/users", new {
      email, password = Password, roles = new[] { AppRoles.SuperAdmin }, associationId = (Guid?)null, poktanId = (Guid?)null
    });
    Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
    var createdId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

    var getResponse = await client.GetAsync($"/api/admin/users/{createdId}");
    var json = await getResponse.Content.ReadFromJsonAsync<JsonElement>();

    Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    var roles = json.GetProperty("roles").EnumerateArray().Select(x => x.GetString()).ToArray();
    Assert.Equal(new[] { AppRoles.SuperAdmin }, roles);
    Assert.Equal(JsonValueKind.Null, json.GetProperty("associationId").ValueKind);
    Assert.Equal(JsonValueKind.Null, json.GetProperty("poktanId").ValueKind);
  }

  [Fact]
  public async Task Create_RejectsSuperAdminWithScopeIds() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();

    var response = await client.PostAsJsonAsync("/api/admin/users", new {
      email = $"{Guid.NewGuid():N}@test.local", password = Password,
      roles = new[] { AppRoles.SuperAdmin }, associationId = TestIds.AssociationA, poktanId = (Guid?)null
    });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Create_RejectsSuperAdminCombinedWithOtherRoles() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();

    var response = await client.PostAsJsonAsync("/api/admin/users", new {
      email = $"{Guid.NewGuid():N}@test.local", password = Password,
      roles = new[] { AppRoles.SuperAdmin, AppRoles.PoktanAdmin }, associationId = (Guid?)null, poktanId = TestIds.PoktanA1
    });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Create_StillValidatesAssociationScope() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();

    var response = await client.PostAsJsonAsync("/api/admin/users", new {
      email = $"{Guid.NewGuid():N}@test.local", password = Password,
      roles = new[] { AppRoles.AssociationAdmin }, associationId = (Guid?)null, poktanId = (Guid?)null
    });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Create_StillValidatesPoktanScope() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();

    var response = await client.PostAsJsonAsync("/api/admin/users", new {
      email = $"{Guid.NewGuid():N}@test.local", password = Password,
      roles = new[] { AppRoles.PoktanAdmin }, associationId = (Guid?)null, poktanId = (Guid?)null
    });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Create_AllowsPoktanAdminAndIcsAuditorOnSameAccount() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();

    var userId = await CreateAdminAsync(client, new[] { AppRoles.PoktanAdmin, AppRoles.IcsAuditor }, poktanId: TestIds.PoktanA1);

    var getResponse = await client.GetAsync($"/api/admin/users/{userId}");
    var json = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
    var roles = json.GetProperty("roles").EnumerateArray().Select(x => x.GetString()).ToArray();

    Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    Assert.Contains(AppRoles.PoktanAdmin, roles);
    Assert.Contains(AppRoles.IcsAuditor, roles);
    Assert.Equal(TestIds.PoktanA1, json.GetProperty("poktanId").GetGuid());

    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    Assert.True(await db.PoktanAdminAssignments.AnyAsync(x => x.UserId == userId && x.PoktanId == TestIds.PoktanA1));
    Assert.True(await db.IcsAuditorAssignments.AnyAsync(x => x.UserId == userId && x.PoktanId == TestIds.PoktanA1));
  }

  [Fact]
  public async Task List_ExcludesMemberTaniBaikUsers() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();

    var response = await client.GetAsync("/api/admin/users?search=member");
    var json = await response.Content.ReadFromJsonAsync<JsonElement>();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(0, json.GetProperty("totalCount").GetInt32());
  }

  [Fact]
  public async Task GetById_IncludesAssociationScope() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();

    var response = await client.GetAsync($"/api/admin/users/{TestIds.AssociationAdminA}");
    var json = await response.Content.ReadFromJsonAsync<JsonElement>();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var roles = json.GetProperty("roles").EnumerateArray().Select(x => x.GetString()).ToArray();
    Assert.Equal(new[] { AppRoles.AssociationAdmin }, roles);
    Assert.Equal(TestIds.AssociationA, json.GetProperty("associationId").GetGuid());
    Assert.Equal("Association A", json.GetProperty("associationNama").GetString());
  }

  [Fact]
  public async Task GetById_ReturnsNotFoundForNonManageableUser() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();

    var response = await client.GetAsync($"/api/admin/users/{TestIds.MemberA}");

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  [Fact]
  public async Task Update_SwitchingRoles_RemovesStaleScopeAssignmentAndAddsNewOne() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();

    var userId = await CreateAdminAsync(client, new[] { AppRoles.AssociationAdmin }, associationId: TestIds.AssociationA);

    var updateResponse = await client.PutAsJsonAsync($"/api/admin/users/{userId}", new {
      roles = new[] { AppRoles.PoktanAdmin }, associationId = (Guid?)null, poktanId = TestIds.PoktanA1, status = AccountStatus.Active
    });
    Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    Assert.False(await db.AssociationAdminAssignments.AnyAsync(x => x.UserId == userId));
    Assert.True(await db.PoktanAdminAssignments.AnyAsync(x => x.UserId == userId && x.PoktanId == TestIds.PoktanA1));
  }

  [Fact]
  public async Task Update_PromotingToSuperAdmin_ClearsScopeAssignments() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();

    var userId = await CreateAdminAsync(client, new[] { AppRoles.PoktanAdmin }, poktanId: TestIds.PoktanA1);

    var updateResponse = await client.PutAsJsonAsync($"/api/admin/users/{userId}", new {
      roles = new[] { AppRoles.SuperAdmin }, associationId = (Guid?)null, poktanId = (Guid?)null, status = AccountStatus.Active
    });
    Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    Assert.False(await db.PoktanAdminAssignments.AnyAsync(x => x.UserId == userId));
  }

  [Fact]
  public async Task Update_RejectsMissingScopeForAssociationAdmin() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();

    var userId = await CreateAdminAsync(client, new[] { AppRoles.AssociationAdmin }, associationId: TestIds.AssociationA);

    var response = await client.PutAsJsonAsync($"/api/admin/users/{userId}", new {
      roles = new[] { AppRoles.AssociationAdmin }, associationId = (Guid?)null, poktanId = (Guid?)null, status = AccountStatus.Active
    });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Update_RevokesRefreshTokensWhenLeavingActiveStatus() {
    using var admin = _factory.CreateClient();
    admin.AuthenticateAsSuperAdmin();
    var email = $"{Guid.NewGuid():N}@test.local";
    var userId = await CreateAdminAsync(admin, new[] { AppRoles.AssociationAdmin }, associationId: TestIds.AssociationA, email: email);

    using var userClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
    var login = await userClient.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
    Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    var refreshToken = GetCookieValue(login);

    var suspend = await admin.PutAsJsonAsync($"/api/admin/users/{userId}", new {
      roles = new[] { AppRoles.AssociationAdmin }, associationId = TestIds.AssociationA, poktanId = (Guid?)null, status = AccountStatus.Suspended
    });
    Assert.Equal(HttpStatusCode.NoContent, suspend.StatusCode);

    var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
    refreshRequest.Headers.Add("Cookie", $"{RefreshTokenCookie.Name}={refreshToken}");
    var refreshResponse = await userClient.SendAsync(refreshRequest);

    Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
  }

  [Fact]
  public async Task Update_PreventsDemotingLastActiveSuperAdmin() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();
    var targetId = await CreateAdminAsync(client, new[] { AppRoles.SuperAdmin });
    await EnsureOnlyOneActiveSuperAdminAsync(targetId);

    var blocked = await client.PutAsJsonAsync($"/api/admin/users/{targetId}", new {
      roles = new[] { AppRoles.AssociationAdmin }, associationId = TestIds.AssociationA, poktanId = (Guid?)null, status = AccountStatus.Active
    });
    Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

    await CreateAdminAsync(client, new[] { AppRoles.SuperAdmin });

    var allowed = await client.PutAsJsonAsync($"/api/admin/users/{targetId}", new {
      roles = new[] { AppRoles.AssociationAdmin }, associationId = TestIds.AssociationA, poktanId = (Guid?)null, status = AccountStatus.Active
    });
    Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
  }

  [Fact]
  public async Task Delete_DisablesUserWithoutRemovingRow() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();
    var userId = await CreateAdminAsync(client, new[] { AppRoles.AssociationAdmin }, associationId: TestIds.AssociationA);

    var deleteResponse = await client.DeleteAsync($"/api/admin/users/{userId}");
    Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

    using var scope = _factory.Services.CreateScope();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var user = await users.FindByIdAsync(userId.ToString());
    Assert.NotNull(user);
    Assert.Equal(AccountStatus.Disabled, user!.Status);
  }

  [Fact]
  public async Task Delete_IsIdempotentWhenAlreadyDisabled() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();
    var userId = await CreateAdminAsync(client, new[] { AppRoles.AssociationAdmin }, associationId: TestIds.AssociationA);

    var first = await client.DeleteAsync($"/api/admin/users/{userId}");
    var second = await client.DeleteAsync($"/api/admin/users/{userId}");

    Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
    Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
  }

  [Fact]
  public async Task Delete_PreventsDisablingLastActiveSuperAdmin() {
    using var client = _factory.CreateClient();
    client.AuthenticateAsSuperAdmin();
    var targetId = await CreateAdminAsync(client, new[] { AppRoles.SuperAdmin });
    await EnsureOnlyOneActiveSuperAdminAsync(targetId);

    var blocked = await client.DeleteAsync($"/api/admin/users/{targetId}");
    Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

    await CreateAdminAsync(client, new[] { AppRoles.SuperAdmin });

    var allowed = await client.DeleteAsync($"/api/admin/users/{targetId}");
    Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
  }

  [Theory]
  [InlineData(AppRoles.AssociationAdmin)]
  [InlineData(AppRoles.PoktanAdmin)]
  public async Task NonSuperAdmin_CannotAccessAdminEndpoints(string role) {
    using var client = _factory.CreateClient();
    client.AuthenticateAs(Guid.NewGuid(), role);

    Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/roles")).StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/users")).StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/users", new { })).StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/admin/users/{Guid.NewGuid()}", new { })).StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/admin/users/{Guid.NewGuid()}")).StatusCode);
  }

  private static async Task<Guid> CreateAdminAsync(
      HttpClient client, string[] roles, Guid? associationId = null, Guid? poktanId = null, string? email = null) {
    var response = await client.PostAsJsonAsync("/api/admin/users", new {
      email = email ?? $"{Guid.NewGuid():N}@test.local", password = Password, roles, associationId, poktanId
    });
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
  }

  private async Task EnsureOnlyOneActiveSuperAdminAsync(Guid keepUserId) {
    using var scope = _factory.Services.CreateScope();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var superAdmins = await users.GetUsersInRoleAsync(AppRoles.SuperAdmin);
    foreach (var user in superAdmins.Where(x => x.Id != keepUserId && x.Status == AccountStatus.Active)) {
      user.Status = AccountStatus.Disabled;
      await users.UpdateAsync(user);
    }
  }

  private static string GetCookieValue(HttpResponseMessage response) {
    var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
    var prefix = $"{RefreshTokenCookie.Name}=";
    var cookie = setCookie.Split(';', 2)[0];

    Assert.StartsWith(prefix, cookie, StringComparison.Ordinal);

    return cookie[prefix.Length..];
  }
}
