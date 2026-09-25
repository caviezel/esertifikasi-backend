using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Esertifikasi.Api.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Esertifikasi.Api.Security;

public sealed record IssuedToken(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

public sealed class TokenService {
  private readonly AppDbContext _db;
  private readonly UserManager<ApplicationUser> _users;
  private readonly JwtOptions _options;

  public TokenService(AppDbContext db, UserManager<ApplicationUser> users, IOptions<JwtOptions> options) {
    _db = db;
    _users = users;
    _options = options.Value;
  }

  public async Task<IssuedToken> IssueAsync(ApplicationUser user, CancellationToken ct) {
    var now = DateTimeOffset.UtcNow;
    var accessTokenExpiresAt = now.AddMinutes(_options.AccessTokenMinutes);
    var refreshTokenExpiresAt = now.AddDays(_options.RefreshTokenDays);
    var roles = await _users.GetRolesAsync(user);
    var claims = new List<Claim> {
      new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
      new(ClaimTypes.NameIdentifier, user.Id.ToString()),
      new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
      new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };
    claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
    var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)), SecurityAlgorithms.HmacSha256);
    var jwt = new JwtSecurityToken(
        _options.Issuer,
        _options.Audience,
        claims,
        now.UtcDateTime,
        accessTokenExpiresAt.UtcDateTime,
        credentials);
    var rawRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    _db.RefreshTokens.Add(new RefreshToken {
      Id = Guid.NewGuid(),
      UserId = user.Id,
      TokenHash = Hash(rawRefreshToken),
      CreatedAt = now,
      ExpiresAt = refreshTokenExpiresAt
    });
    await _db.SaveChangesAsync(ct);

    return new IssuedToken(
        new JwtSecurityTokenHandler().WriteToken(jwt),
        accessTokenExpiresAt,
        rawRefreshToken,
        refreshTokenExpiresAt);
  }

  public async Task<IssuedToken?> RotateAsync(string rawToken, CancellationToken ct) {
    var token = await _db.RefreshTokens.Include(x => x.User)
        .SingleOrDefaultAsync(x => x.TokenHash == Hash(rawToken), ct);
    if (token is null || token.RevokedAt is not null || token.ExpiresAt <= DateTimeOffset.UtcNow || token.User.Status != AccountStatus.Active) {
      return null;
    }

    token.RevokedAt = DateTimeOffset.UtcNow;

    return await IssueAsync(token.User, ct);
  }

  public async Task RevokeAsync(string rawToken, CancellationToken ct) {
    var hash = Hash(rawToken);
    var token = await _db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
    if (token is not null && token.RevokedAt is null) {
      token.RevokedAt = DateTimeOffset.UtcNow;
      await _db.SaveChangesAsync(ct);
    }
  }

  public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct) {
    var now = DateTimeOffset.UtcNow;
    var tokens = await _db.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAt == null).ToListAsync(ct);
    foreach (var token in tokens) {
      token.RevokedAt = now;
    }

    if (tokens.Count > 0) {
      await _db.SaveChangesAsync(ct);
    }
  }

  private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
