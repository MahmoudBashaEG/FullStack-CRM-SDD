using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Persistence;
using Crm.Domain.Identity;

namespace Crm.Application.Auth;

/// <summary>Stages a new refresh session (hash only) and creates the access token. Caller saves.</summary>
public sealed class AuthSessionFactory(ITokenService tokenService, IRefreshSessionRepository sessions)
{
    public AuthSession Issue(UserAccount user, Guid familyId, DateTimeOffset now, Guid? sessionId = null)
    {
        var refreshToken = RefreshTokens.Generate();
        var expiresAtUtc = now.Add(tokenService.RefreshTokenLifetime);

        sessions.Add(RefreshSession.Create(
            sessionId ?? Guid.CreateVersion7(), user.Id, familyId, RefreshTokens.Hash(refreshToken), now, expiresAtUtc));

        return new AuthSession(tokenService.CreateAccessToken(user, now), refreshToken, expiresAtUtc, user);
    }
}
