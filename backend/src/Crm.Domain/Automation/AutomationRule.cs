using Crm.Domain.Common;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.Domain.Automation;

/// <summary>
/// SLA-001 S9: one fixed trigger → optional fixed conditions → one fixed action. Combination validity is enforced here
/// (DomainException) and, field by field, by the Application validators (400). Never deleted — disabled instead.
/// </summary>
public sealed class AutomationRule
{
    private AutomationRule() { } // EF Core

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }

    /// <summary>Last time the rule was created enabled or re-enabled. SLA events due before it are ignored ("subsequent events only").</summary>
    public DateTimeOffset? EnabledAtUtc { get; private set; }

    public AutomationTrigger Trigger { get; private set; }
    public AutomationAction Action { get; private set; }
    public int SortOrder { get; private set; }
    public TicketCategory? CategoryCondition { get; private set; }
    public TicketPriority? PriorityCondition { get; private set; }
    public bool? AssignedCondition { get; private set; }
    public SlaKind? SlaKindCondition { get; private set; }
    public Guid? TargetAgentUserId { get; private set; }
    public int? WarningLeadMinutes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public Guid UpdatedByUserId { get; private set; }

    public static AutomationRule Create(Guid id, AutomationRuleDefinition definition, Guid actorUserId, DateTimeOffset now)
    {
        var rule = new AutomationRule { Id = id, CreatedAtUtc = now };
        rule.Apply(definition, actorUserId, now);
        return rule;
    }

    public void Update(AutomationRuleDefinition definition, Guid actorUserId, DateTimeOffset now) => Apply(definition, actorUserId, now);

    /// <summary>Pure condition check, shared by ticket creation, the worker and the SLA display (warning lead).</summary>
    public bool Matches(TicketCategory category, TicketPriority priority, bool isAssigned, SlaKind? kind) =>
        (CategoryCondition is null || CategoryCondition == category)
        && (PriorityCondition is null || PriorityCondition == priority)
        && (AssignedCondition is null || AssignedCondition == isAssigned)
        && (SlaKindCondition is null || kind is null || SlaKindCondition == kind);

    public static bool IsValidCombination(AutomationTrigger trigger, AutomationAction action) => (trigger, action) switch
    {
        (AutomationTrigger.TicketCreated, AutomationAction.AssignToAgent) => true,
        (AutomationTrigger.SlaWarningDue, AutomationAction.CreateInAppNotification) => true,
        (AutomationTrigger.SlaBreached, AutomationAction.CreateInAppNotification) => true,
        (AutomationTrigger.SlaBreached, AutomationAction.EscalateTicket) => true,
        _ => false,
    };

    private void Apply(AutomationRuleDefinition d, Guid actorUserId, DateTimeOffset now)
    {
        if (!IsValidCombination(d.Trigger, d.Action))
        {
            throw new DomainException($"{d.Trigger} cannot run {d.Action}.");
        }

        // Parameter shape per combination (the validators already returned 400 for user input; this is the invariant).
        if ((d.Action == AutomationAction.AssignToAgent) != (d.TargetAgentUserId is not null)
            || (d.Trigger == AutomationTrigger.SlaWarningDue) != (d.WarningLeadMinutes is not null)
            || (d.Trigger == AutomationTrigger.TicketCreated && (d.AssignedCondition is not null || d.SlaKindCondition is not null)))
        {
            throw new DomainException("The rule parameters do not match its trigger and action.");
        }

        if (d.IsEnabled && (!IsEnabled || EnabledAtUtc is null))
        {
            EnabledAtUtc = now;
        }

        Name = d.Name;
        IsEnabled = d.IsEnabled;
        Trigger = d.Trigger;
        Action = d.Action;
        SortOrder = d.SortOrder;
        CategoryCondition = d.CategoryCondition;
        PriorityCondition = d.PriorityCondition;
        AssignedCondition = d.AssignedCondition;
        SlaKindCondition = d.SlaKindCondition;
        TargetAgentUserId = d.TargetAgentUserId;
        WarningLeadMinutes = d.WarningLeadMinutes;
        UpdatedAtUtc = now;
        UpdatedByUserId = actorUserId;
    }
}

public sealed record AutomationRuleDefinition(
    string Name, bool IsEnabled, AutomationTrigger Trigger, AutomationAction Action, int SortOrder,
    TicketCategory? CategoryCondition, TicketPriority? PriorityCondition, bool? AssignedCondition, SlaKind? SlaKindCondition,
    Guid? TargetAgentUserId, int? WarningLeadMinutes);
