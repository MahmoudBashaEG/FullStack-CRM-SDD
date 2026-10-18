using Crm.Application.Abstractions.Identity;
using Crm.Application.Common.Validation;
using Crm.Domain.Automation;
using Crm.Domain.Identity;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;
using FluentValidation;
using FluentValidation.Results;

namespace Crm.Application.Automation;

/// <summary>Create and update share one shape, so they share one set of field rules.</summary>
public interface IAutomationRuleInput
{
    string Name { get; }
    bool? IsEnabled { get; }
    AutomationTrigger? Trigger { get; }
    AutomationAction? Action { get; }
    int SortOrder { get; }
    TicketCategory? CategoryCondition { get; }
    TicketPriority? PriorityCondition { get; }
    bool? AssignedCondition { get; }
    SlaKind? SlaKindCondition { get; }
    Guid? TargetAgentUserId { get; }
    int? WarningLeadMinutes { get; }
}

public abstract class AutomationRuleInputValidator<T> : AbstractValidator<T> where T : IAutomationRuleInput
{
    protected AutomationRuleInputValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(SlaRules.RuleNameMaxLength);
        RuleFor(c => c.IsEnabled).NotNull();
        RuleFor(c => c.Trigger).NotNull().IsInEnum();
        RuleFor(c => c.Action).NotNull().IsInEnum();
        RuleFor(c => c.SortOrder).InclusiveBetween(1, SlaRules.MaxSortOrder);
        RuleFor(c => c.CategoryCondition).IsInEnum();
        RuleFor(c => c.PriorityCondition).IsInEnum();
        RuleFor(c => c.SlaKindCondition).IsInEnum();

        RuleFor(c => c.Action)
            .Must((c, action) => AutomationRule.IsValidCombination(c.Trigger!.Value, action!.Value))
            .WithMessage("This action is not available for the selected trigger.")
            .When(c => c.Trigger is not null && c.Action is not null);

        RuleFor(c => c.TargetAgentUserId)
            .NotNull().WithMessage("Select the Agent to assign tickets to.")
            .When(c => c.Action == AutomationAction.AssignToAgent);
        RuleFor(c => c.TargetAgentUserId)
            .Null().WithMessage("Only Assign to agent uses a target agent.")
            .When(c => c.Action is not null && c.Action != AutomationAction.AssignToAgent);

        RuleFor(c => c.WarningLeadMinutes)
            .NotNull().WithMessage("Enter how many business minutes before the deadline to warn.")
            .InclusiveBetween(1, SlaRules.MaxWarningLeadMinutes)
            .When(c => c.Trigger == AutomationTrigger.SlaWarningDue);
        RuleFor(c => c.WarningLeadMinutes)
            .Null().WithMessage("Only the SLA warning trigger uses a warning lead.")
            .When(c => c.Trigger is not null && c.Trigger != AutomationTrigger.SlaWarningDue);

        RuleFor(c => c.AssignedCondition)
            .Null().WithMessage("New tickets are always unassigned and have no SLA kind condition.")
            .When(c => c.Trigger == AutomationTrigger.TicketCreated);
        RuleFor(c => c.SlaKindCondition)
            .Null().WithMessage("New tickets are always unassigned and have no SLA kind condition.")
            .When(c => c.Trigger == AutomationTrigger.TicketCreated);
    }

    /// <summary>400 on targetAgentUserId unless the id is an existing, active Agent (not SuperAdmin, not Customer).</summary>
    public static async Task EnsureActiveAgentAsync(IIdentityService identity, Guid? agentId, CancellationToken cancellationToken)
    {
        if (agentId is not { } id)
        {
            return;
        }

        var agent = await identity.FindByIdAsync(id, cancellationToken);
        if (agent is not { Role: UserRoles.Agent, IsActive: true })
        {
            throw new ValidationException([new ValidationFailure(nameof(IAutomationRuleInput.TargetAgentUserId), "Select an active Agent.")]);
        }
    }

    public static AutomationRuleDefinition ToDefinition(IAutomationRuleInput c) =>
        new(c.Name.Trim(), c.IsEnabled!.Value, c.Trigger!.Value, c.Action!.Value, c.SortOrder, c.CategoryCondition,
            c.PriorityCondition, c.AssignedCondition, c.SlaKindCondition, c.TargetAgentUserId, c.WarningLeadMinutes);
}
