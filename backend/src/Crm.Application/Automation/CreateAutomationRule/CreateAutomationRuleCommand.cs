using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Persistence;
using Crm.Domain.Automation;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;
using Mediator;

namespace Crm.Application.Automation.CreateAutomationRule;

/// <summary>Single save — not transactional. Nullable enum/bool fields exist so a missing JSON field fails validation.</summary>
public sealed record CreateAutomationRuleCommand(
    Guid ActorUserId,
    string Name,
    bool? IsEnabled,
    AutomationTrigger? Trigger,
    AutomationAction? Action,
    int SortOrder,
    TicketCategory? CategoryCondition,
    TicketPriority? PriorityCondition,
    bool? AssignedCondition,
    SlaKind? SlaKindCondition,
    Guid? TargetAgentUserId,
    int? WarningLeadMinutes) : ICommand<AutomationRuleDetails>, IAutomationRuleInput;

public sealed class CreateAutomationRuleCommandHandler(
    IAutomationRuleRepository rules,
    IIdentityService identity,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CreateAutomationRuleCommand, AutomationRuleDetails>
{
    public async ValueTask<AutomationRuleDetails> Handle(CreateAutomationRuleCommand command, CancellationToken cancellationToken)
    {
        await CreateAutomationRuleValidator.EnsureActiveAgentAsync(identity, command.TargetAgentUserId, cancellationToken);

        var rule = AutomationRule.Create(
            Guid.CreateVersion7(), CreateAutomationRuleValidator.ToDefinition(command), command.ActorUserId, timeProvider.GetUtcNow());
        rules.Add(rule);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return (await rules.FindDetailsByIdAsync(rule.Id, cancellationToken))!;
    }
}

public sealed class CreateAutomationRuleValidator : AutomationRuleInputValidator<CreateAutomationRuleCommand>;
