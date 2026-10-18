using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Validation;
using FluentValidation;
using Mediator;

namespace Crm.Application.AgentTasks.ListLinkableTickets;

/// <summary>
/// F5 picker data. Agent: the owner is always the caller. SuperAdmin: the prospective owner is required and must be an active Agent.
/// A display aid only — linking re-checks under a lock.
/// </summary>
public sealed record ListLinkableTicketsQuery(StaffActor Actor, Guid? OwnerUserId, string? Search)
    : IQuery<IReadOnlyList<LinkableTicket>>;

public sealed class ListLinkableTicketsQueryHandler(IAgentTaskRepository tasks, IIdentityService identity)
    : IQueryHandler<ListLinkableTicketsQuery, IReadOnlyList<LinkableTicket>>
{
    private const int PickerLimit = 10;

    public async ValueTask<IReadOnlyList<LinkableTicket>> Handle(ListLinkableTicketsQuery query, CancellationToken cancellationToken)
    {
        Guid ownerId;
        if (!query.Actor.IsSuperAdmin)
        {
            if (query.OwnerUserId is { } requested && requested != query.Actor.UserId)
            {
                throw AgentTaskGuards.Invalid(nameof(query.OwnerUserId), "Agents can only link their own tickets.");
            }

            ownerId = query.Actor.UserId;
        }
        else
        {
            ownerId = query.OwnerUserId ?? throw AgentTaskGuards.Invalid(nameof(query.OwnerUserId), "Select an active Agent.");
            await AgentTaskGuards.EnsureActiveAgentAsync(identity, ownerId, nameof(query.OwnerUserId), cancellationToken);
        }

        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        return await tasks.ListLinkableTicketsAsync(ownerId, search, PickerLimit, cancellationToken);
    }
}

public sealed class ListLinkableTicketsValidator : AbstractValidator<ListLinkableTicketsQuery>
{
    public ListLinkableTicketsValidator()
    {
        RuleFor(q => q.Search).MaximumLength(AgentTaskRules.SearchMaxLength);
        RuleFor(q => q.OwnerUserId).NotEqual(Guid.Empty);
    }
}
