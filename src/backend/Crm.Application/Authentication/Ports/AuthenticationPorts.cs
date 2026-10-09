using Crm.Application.Authentication.Contracts;
namespace Crm.Application.Authentication.Ports;

public interface IAccessTokenIssuer
{
    AccessTokenEnvelope Issue(TrustedAuthentication authentication, CancellationToken cancellationToken);
}
public interface ICurrentAuthenticationReader
{
    Task<TrustedAuthentication?> ValidateAsync(AuthenticationBinding binding, CancellationToken cancellationToken);
}
