using Crm.Application.Abstractions.Identity;
using Mediator;

namespace Crm.Application.Agents.GetAgent;

public sealed record GetAgentQuery(Guid Id) : IQuery<UserAccount>;

public sealed class GetAgentQueryHandler(IIdentityService identity) : IQueryHandler<GetAgentQuery, UserAccount>
{
    public async ValueTask<UserAccount> Handle(GetAgentQuery query, CancellationToken cancellationToken) =>
        await identity.GetAgentOrThrowAsync(query.Id, cancellationToken);
}
