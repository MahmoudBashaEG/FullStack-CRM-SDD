using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Mediator;

namespace Crm.Application.Automation.GetAutomationRule;

public sealed record GetAutomationRuleQuery(Guid Id) : IQuery<AutomationRuleDetails>;

public sealed class GetAutomationRuleQueryHandler(IAutomationRuleRepository rules)
    : IQueryHandler<GetAutomationRuleQuery, AutomationRuleDetails>
{
    public async ValueTask<AutomationRuleDetails> Handle(GetAutomationRuleQuery query, CancellationToken cancellationToken) =>
        await rules.FindDetailsByIdAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException($"Automation rule '{query.Id}' was not found.");
}
