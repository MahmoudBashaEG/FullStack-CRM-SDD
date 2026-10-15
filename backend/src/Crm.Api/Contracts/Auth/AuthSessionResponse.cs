using Crm.Api.Contracts.Profile;
using Crm.Application.Auth;

namespace Crm.Api.Contracts.Auth;

/// <summary>The refresh token travels only in the HttpOnly cookie, never in the body.</summary>
public sealed record AuthSessionResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAtUtc, CurrentUserResponse User)
{
    public static AuthSessionResponse From(AuthSession session) =>
        new(session.AccessToken.Token, session.AccessToken.ExpiresAtUtc, CurrentUserResponse.From(session.User));
}
