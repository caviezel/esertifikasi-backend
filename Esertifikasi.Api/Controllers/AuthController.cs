using Esertifikasi.Api.Domain.Entities;
using Esertifikasi.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Esertifikasi.Api.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController : ControllerBase {
  private readonly UserManager<ApplicationUser> _users;
  private readonly SignInManager<ApplicationUser> _signIn;
  private readonly TokenService _tokens;

  public AuthController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, TokenService tokens) {
    _users = users;
    _signIn = signIn;
    _tokens = tokens;
  }

  [AllowAnonymous, HttpPost("login")]
  public async Task<ActionResult<AccessTokenResponse>> Login(LoginRequest request, CancellationToken ct) {
    var user = await _users.FindByEmailAsync(request.Email);
    if (user is null || user.Status != AccountStatus.Active) {
      return Unauthorized(new { message = "Email atau kata sandi salah, atau akun tidak aktif." });
    }

    var result = await _signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
    if (!result.Succeeded) {
      return Unauthorized(new { message = "Email atau kata sandi salah, atau akun tidak aktif." });
    }

    user.LastLoginAt = DateTimeOffset.UtcNow;
    await _users.UpdateAsync(user);

    var token = await _tokens.IssueAsync(user, ct);
    SetRefreshCookie(token);

    return Ok(ToResponse(token));
  }

  [AllowAnonymous, HttpPost("refresh")]
  public async Task<ActionResult<AccessTokenResponse>> Refresh(CancellationToken ct) {
    var refreshToken = RefreshTokenCookie.Read(Request);
    if (refreshToken is null) {
      return Unauthorized(new { message = "Cookie refresh token tidak ditemukan." });
    }

    var token = await _tokens.RotateAsync(refreshToken, ct);
    if (token is null) {
      RefreshTokenCookie.Delete(Response);
      return Unauthorized(new { message = "Refresh token tidak valid atau sudah kedaluwarsa." });
    }

    SetRefreshCookie(token);

    return Ok(ToResponse(token));
  }

  [AllowAnonymous, HttpPost("logout")]
  public async Task<IActionResult> Logout(CancellationToken ct) {
    var refreshToken = RefreshTokenCookie.Read(Request);
    if (refreshToken is not null) {
      await _tokens.RevokeAsync(refreshToken, ct);
    }

    RefreshTokenCookie.Delete(Response);

    return NoContent();
  }

  [Authorize, HttpGet("me")]
  public async Task<ActionResult<CurrentUserResponse>> Me() {
    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    var user = userId is null ? null : await _users.FindByIdAsync(userId);
    if (user is null || user.Status != AccountStatus.Active) {
      return Unauthorized(new { message = "Akun tidak ditemukan atau tidak aktif." });
    }

    var roles = await _users.GetRolesAsync(user);

    return Ok(new CurrentUserResponse(user.Id, user.Email ?? string.Empty, roles));
  }

  private void SetRefreshCookie(IssuedToken token) {
    RefreshTokenCookie.Append(Response, token.RefreshToken, token.RefreshTokenExpiresAt);
  }

  private static AccessTokenResponse ToResponse(IssuedToken token) {
    return new AccessTokenResponse(token.AccessToken, token.AccessTokenExpiresAt);
  }
}

public sealed record LoginRequest(string Email, string Password);

public sealed record AccessTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);

public sealed record CurrentUserResponse(Guid Id, string Email, IList<string> Roles);
