using Crm.Application.Accounts.Contracts;
using Crm.Domain.Accounts;
namespace Crm.Application.Authentication.Contracts;

public sealed record AuthenticationBinding(Guid UserId, Guid SessionId, AccountRole Role, long AuthenticationVersion);
public sealed record TrustedAuthentication(AccountIdentity Identity, Guid SessionId, long AuthenticationVersion, DateTimeOffset ExpiresAtUtc);
public sealed record AccessTokenEnvelope(string AccessToken, string TokenType, DateTimeOffset ExpiresAtUtc);
