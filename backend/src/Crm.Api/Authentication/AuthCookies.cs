using System.Buffers.Text;
using System.Security.Cryptography;

namespace Crm.Api.Authentication;

/// <summary>Refresh cookie (HttpOnly, auth path only) + double-submit CSRF cookie (readable by Angular).</summary>
internal static class AuthCookies
{
    public const string RefreshCookieName = "crm_refresh";
    public const string CsrfCookieName = "XSRF-TOKEN";
    public const string CsrfHeaderName = "X-XSRF-TOKEN";
    private const string RefreshCookiePath = "/" + ApiRoutes.V1 + "/auth";

    public static void Issue(HttpResponse response, string refreshToken, DateTimeOffset expiresAtUtc)
    {
        response.Cookies.Append(RefreshCookieName, refreshToken, Options(RefreshCookiePath, httpOnly: true, expiresAtUtc));
        response.Cookies.Append(CsrfCookieName, Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32)), Options("/", httpOnly: false, expiresAtUtc));
    }

    public static void Clear(HttpResponse response)
    {
        response.Cookies.Delete(RefreshCookieName, Options(RefreshCookiePath, httpOnly: true, expires: null));
        response.Cookies.Delete(CsrfCookieName, Options("/", httpOnly: false, expires: null));
    }

    private static CookieOptions Options(string path, bool httpOnly, DateTimeOffset? expires) => new()
    {
        Path = path,
        HttpOnly = httpOnly,
        Secure = true,               // always; browsers accept Secure cookies on http://localhost
        SameSite = SameSiteMode.Strict,
        Expires = expires,
        IsEssential = true,
    };
}
