using Crm.Application.Abstractions.Identity;
using Crm.Application.Common.Exceptions;
using Crm.Domain.Identity;

namespace Crm.Application.Agents;

internal static class AgentLookup
{
    /// <summary>Only Agent accounts are reachable through /agents; SuperAdmin/Customer ids yield 404.</summary>
    public static async Task<UserAccount> GetAgentOrThrowAsync(this IIdentityService identity, Guid id, CancellationToken cancellationToken)
    {
        var user = await identity.FindByIdAsync(id, cancellationToken);
        return user is { Role: UserRoles.Agent } ? user : throw new NotFoundException($"Agent '{id}' was not found.");
    }
}
