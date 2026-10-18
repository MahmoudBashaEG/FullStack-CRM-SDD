using Crm.Application.Abstractions.Identity;
using Crm.Application.Common.Exceptions;
using Mediator;

namespace Crm.Application.Administration.GetAccount;

public sealed record GetAccountQuery(Guid Id) : IQuery<UserAccount>;

public sealed class GetAccountQueryHandler(IIdentityService identity) : IQueryHandler<GetAccountQuery, UserAccount>
{
    public async ValueTask<UserAccount> Handle(GetAccountQuery query, CancellationToken cancellationToken) =>
        await identity.FindByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException($"Account '{query.Id}' was not found.");
}
