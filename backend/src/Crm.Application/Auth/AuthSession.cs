using Crm.Application.Abstractions.Identity;

namespace Crm.Application.Auth;

/// <summary>Result of login/renewal. RefreshToken is plaintext only in memory, for the cookie.</summary>
public sealed record AuthSession(
    AccessToken AccessToken,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    UserAccount User);
