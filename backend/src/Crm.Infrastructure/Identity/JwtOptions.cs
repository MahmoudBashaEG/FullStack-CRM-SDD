using Microsoft.IdentityModel.Tokens;

namespace Crm.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    /// <summary>Base64 of >= 32 random bytes. User secrets / environment only.</summary>
    public string SigningKey { get; set; } = string.Empty;
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);
    /// <summary>Concurrent-tab tolerance for re-presented rotated tokens. Zero = strict rotation.</summary>
    public TimeSpan RefreshTokenReuseGracePeriod { get; set; } = TimeSpan.FromSeconds(30);

    public SymmetricSecurityKey CreateSigningKey() => new(Convert.FromBase64String(SigningKey));

    internal bool HasValidSigningKey()
    {
        try { return Convert.FromBase64String(SigningKey).Length >= 32; }
        catch (FormatException) { return false; }
    }
}
