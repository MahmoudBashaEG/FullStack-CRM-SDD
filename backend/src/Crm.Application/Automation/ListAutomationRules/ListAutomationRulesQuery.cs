using Crm.Application.Abstractions.Persistence;
using Mediator;

namespace Crm.Application.Automation.ListAutomationRules;

/// <summary>All rules ordered SortOrder, CreatedAtUtc, Id (enabled and disabled).</summary>
public sealed record ListAutomationRulesQuery : IQuery<IReadOnlyList<AutomationRuleDetails>>;

public sealed class ListAutomationRulesQueryHandler(IAutomationRuleRepository rules)
    : IQueryHandler<ListAutomationRulesQuery, IReadOnlyList<AutomationRuleDetails>>
{
    public async ValueTask<IReadOnlyList<AutomationRuleDetails>> Handle(ListAutomationRulesQuery query, CancellationToken cancellationToken) =>
        await rules.ListDetailsAsync(cancellationToken);
}
