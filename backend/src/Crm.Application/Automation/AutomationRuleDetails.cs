using Crm.Domain.Automation;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.Application.Automation;

/// <summary>Rule read model. The target Agent's name and active flag are joined at read time.</summary>
public sealed record AutomationRuleDetails(
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
    DateTimeOffset UpdatedAtUtc);
