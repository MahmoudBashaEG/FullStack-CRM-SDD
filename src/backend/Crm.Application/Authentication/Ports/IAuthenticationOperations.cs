using Crm.Application.Authentication.Contracts;
using Crm.Application.Common;
namespace Crm.Application.Authentication.Ports;

public sealed record SessionIssue(AccessTokenEnvelope Access,string RefreshCredential,DateTimeOffset SessionExpiresAtUtc);
public interface IRefreshSessionReader
{
    Task<Guid?> ReadLiveSessionAsync(string? credential,CancellationToken ct);
}
public interface IAuthenticationOperations
{
    Task<OperationResult<SessionIssue>> SignInAsync(string email,string password,string? priorCredential,CancellationToken ct);
    Task<OperationResult<SessionIssue>> RefreshAsync(string? credential,CancellationToken ct);
    Task<OperationResult<bool>> SignOutAsync(string? credential,TrustedAuthentication? bearer,CancellationToken ct);
}
