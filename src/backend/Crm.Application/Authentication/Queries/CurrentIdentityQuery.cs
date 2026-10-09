using Crm.Application.Accounts.Contracts;
using Crm.Application.Authentication.Contracts;
using MediatR;
namespace Crm.Application.Authentication.Queries;

public sealed record CurrentIdentityQuery(TrustedAuthentication Authentication) : IRequest<AccountIdentity>;
public sealed class CurrentIdentityHandler : IRequestHandler<CurrentIdentityQuery,AccountIdentity>
{
    public Task<AccountIdentity> Handle(CurrentIdentityQuery request,CancellationToken ct){ct.ThrowIfCancellationRequested();return Task.FromResult(request.Authentication.Identity);}
}
