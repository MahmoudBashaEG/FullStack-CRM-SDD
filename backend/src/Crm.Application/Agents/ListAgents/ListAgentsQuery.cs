using Crm.Application.Abstractions.Identity;
using Crm.Domain.Identity;
using Mediator;

namespace Crm.Application.Agents.ListAgents;

public sealed record ListAgentsQuery : IQuery<IReadOnlyList<UserAccount>>;

public sealed class ListAgentsQueryHandler(IIdentityService identity)
    : IQueryHandler<ListAgentsQuery, IReadOnlyList<UserAccount>>
{
    public async ValueTask<IReadOnlyList<UserAccount>> Handle(ListAgentsQuery query, CancellationToken cancellationToken) =>
        await identity.ListByRoleAsync(UserRoles.Agent, cancellationToken);
}
