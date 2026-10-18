using Crm.Application.Automation;
using Crm.Domain.Automation;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.Api.Contracts.Automation;

/// <summary>Nullable enums/bools so a missing JSON field fails validation (400) instead of defaulting.</summary>
public sealed record AutomationRuleRequest(
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
    int? WarningLeadMinutes);

public sealed record AutomationRuleResponse(
    Guid Id,
    string Name,
    bool IsEnabled,
    DateTimeOffset? EnabledAtUtc,
    AutomationTrigger Trigger,
    AutomationAction Action,
    int SortOrder,
    TicketCategory? CategoryCondition,
    TicketPriority? PriorityCondition,
    bool? AssignedCondition,
    SlaKind? SlaKindCondition,
    Guid? TargetAgentUserId,
    string? TargetAgentName,
    bool? TargetAgentIsActive,
    int? WarningLeadMinutes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public static AutomationRuleResponse From(AutomationRuleDetails r) =>
        new(r.Id, r.Name, r.IsEnabled, r.EnabledAtUtc, r.Trigger, r.Action, r.SortOrder, r.CategoryCondition, r.PriorityCondition,
            r.AssignedCondition, r.SlaKindCondition, r.TargetAgentUserId, r.TargetAgentName, r.TargetAgentIsActive, r.WarningLeadMinutes,
            r.CreatedAtUtc, r.UpdatedAtUtc);
}

public sealed record AutomationExecutionResponse(
    long Id,
    Guid RuleId,
    string RuleName,
    Guid TicketId,
    string TicketSubject,
    SlaKind? SlaKind,
    AutomationTrigger Trigger,
    AutomationAction Action,
    AutomationOutcome Outcome,
    string? Detail,
    DateTimeOffset OccurredAtUtc)
{
    public static AutomationExecutionResponse From(AutomationExecutionDetails e) =>
        new(e.Id, e.RuleId, e.RuleName, e.TicketId, e.TicketSubject, e.SlaKind, e.Trigger, e.Action, e.Outcome, e.Detail, e.OccurredAtUtc);
}
