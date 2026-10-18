using Crm.Domain.Automation;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.Application.Sla;

/// <summary>Raw persisted SLA columns of one ticket (projected in SQL). Null on TicketDetails when the ticket is not tracked.</summary>
public sealed record TicketSlaSnapshot(
    DateTimeOffset CreatedAtUtc, string TimeZoneId,
    int FirstResponseTargetMinutes, DateTimeOffset FirstResponseDueAtUtc, DateTimeOffset? FirstRespondedAtUtc, string? FirstRespondedByName,
    int ResolutionTargetMinutes, DateTimeOffset ResolutionDueAtUtc, DateTimeOffset? ResolvedAtUtc);

public sealed record TicketSlaInput(TicketCategory Category, TicketPriority Priority, bool IsAssigned, TicketSlaSnapshot Snapshot);

public enum SlaIndicator { NotTracked = 0, OnTrack = 1, Warning = 2, Breached = 3, Met = 4 }

public enum SlaTargetState { Pending = 0, Met = 1, Breached = 2 }

/// <summary>BusinessMinutesRemaining: positive = left, negative = overdue (null once completed or when no calendar exists).</summary>
public sealed record SlaTargetView(
    SlaKind Kind, int TargetMinutes, DateTimeOffset DueAtUtc, DateTimeOffset? CompletedAtUtc, string? CompletedByName,
    SlaTargetState State, bool IsWarning, int? BusinessMinutesRemaining, int BusinessMinutesElapsed);

public sealed record TicketSlaView(SlaIndicator Indicator, string TimeZoneId, SlaTargetView FirstResponse, SlaTargetView Resolution);

/// <summary>Loaded once per request/run: calendar rules (null = no calendar => no business minutes, no warnings), enabled SlaWarningDue rules, server now.</summary>
public sealed record SlaEvaluationContext(BusinessCalendarRules? Calendar, IReadOnlyList<AutomationRule> WarningRules, DateTimeOffset Now);
