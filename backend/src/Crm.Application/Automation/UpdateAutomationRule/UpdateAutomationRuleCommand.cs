using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Domain.Automation;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;
using Mediator;

namespace Crm.Application.Automation.UpdateAutomationRule;

/// <summary>Single save — not transactional. Re-enabling a disabled rule resets EnabledAtUtc (subsequent events only).</summary>
public sealed record UpdateAutomationRuleCommand(
    Guid Id,
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

public sealed class UpdateAutomationRuleCommandHandler(
    IAutomationRuleRepository rules,
    IIdentityService identity,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<UpdateAutomationRuleCommand, AutomationRuleDetails>
{
    public async ValueTask<AutomationRuleDetails> Handle(UpdateAutomationRuleCommand command, CancellationToken cancellationToken)
    {
        var rule = await rules.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException($"Automation rule '{command.Id}' was not found.");
        await UpdateAutomationRuleValidator.EnsureActiveAgentAsync(identity, command.TargetAgentUserId, cancellationToken);

        rule.Update(UpdateAutomationRuleValidator.ToDefinition(command), command.ActorUserId, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return (await rules.FindDetailsByIdAsync(rule.Id, cancellationToken))!;
    }
}

public sealed class UpdateAutomationRuleValidator : AutomationRuleInputValidator<UpdateAutomationRuleCommand>;
