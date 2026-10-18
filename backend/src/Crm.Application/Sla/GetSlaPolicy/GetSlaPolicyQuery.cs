using Crm.Application.Abstractions.Persistence;
using Crm.Domain.Sla;
using Mediator;

namespace Crm.Application.Sla.GetSlaPolicy;

/// <summary>Not configured yet => IsConfigured false, IsEnabled false, no targets.</summary>
public sealed record GetSlaPolicyQuery : IQuery<SlaPolicyDetails>;

public sealed class GetSlaPolicyQueryHandler(ISlaConfigurationRepository configuration)
    : IQueryHandler<GetSlaPolicyQuery, SlaPolicyDetails>
{
    public async ValueTask<SlaPolicyDetails> Handle(GetSlaPolicyQuery query, CancellationToken cancellationToken) =>
        await SlaPolicyMapper.ToDetailsAsync(await configuration.FindPolicyAsync(cancellationToken), configuration, cancellationToken);
}

internal static class SlaPolicyMapper
{
    public static async Task<SlaPolicyDetails> ToDetailsAsync(
        SlaPolicy? policy, ISlaConfigurationRepository configuration, CancellationToken cancellationToken)
    {
        if (policy is null)
        {
            return new SlaPolicyDetails(false, false, [], null, null);
        }

        return new SlaPolicyDetails(
            true,
            policy.IsEnabled,
            policy.Targets.OrderBy(t => t.Priority).ToList(),
            policy.UpdatedAtUtc,
            await configuration.FindUserNameAsync(policy.UpdatedByUserId, cancellationToken));
    }
}
