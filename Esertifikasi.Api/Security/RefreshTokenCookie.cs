namespace Esertifikasi.Api.Security;

public static class RefreshTokenCookie {
  public const string Name = "__Secure-esertifikasi-refresh";
  public const string Path = "/api/auth";

  public static void Append(HttpResponse response, string token, DateTimeOffset expiresAt) {
    response.Cookies.Append(Name, token, CreateOptions(expiresAt));
  }

  public static string? Read(HttpRequest request) {
    return request.Cookies.TryGetValue(Name, out var token) && !string.IsNullOrWhiteSpace(token)
        ? token
        : null;
  }

  public static void Delete(HttpResponse response) {
    response.Cookies.Delete(Name, CreateOptions(DateTimeOffset.UnixEpoch));
  }

  private static CookieOptions CreateOptions(DateTimeOffset expiresAt) {
    return new CookieOptions {
      HttpOnly = true,
      Secure = true,
      SameSite = SameSiteMode.Lax,
      Path = Path,
      Expires = expiresAt,
      IsEssential = true
    };
  }
}
