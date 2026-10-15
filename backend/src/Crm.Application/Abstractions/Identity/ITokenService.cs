namespace Crm.Application.Abstractions.Identity;

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAtUtc);

public interface ITokenService
{
    AccessToken CreateAccessToken(UserAccount user, DateTimeOffset now);
    TimeSpan RefreshTokenLifetime { get; }

    /// <summary>How long after rotation a re-presented token counts as a concurrent tab, not replay. Zero disables.</summary>
    TimeSpan RefreshTokenReuseGracePeriod { get; }
}
