using Crm.Application.Automation;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.Application.Sla;

public sealed record SlaPolicyDetails(
    bool IsConfigured, bool IsEnabled, IReadOnlyList<SlaTarget> Targets, DateTimeOffset? UpdatedAtUtc, string? UpdatedByName);

public sealed record BusinessCalendarDetails(
    bool IsConfigured, string? TimeZoneId, IReadOnlyList<DayOfWeek> WorkingDays, TimeOnly? OpensAt, TimeOnly? ClosesAt,
    IReadOnlyList<BusinessHoliday> Holidays, DateTimeOffset? UpdatedAtUtc, string? UpdatedByName);

public sealed record SlaDuePreview(DateTimeOffset StartUtc, int Minutes, DateTimeOffset DueAtUtc, string TimeZoneId);

/// <summary>Counts are over the first SlaRules.OverviewScanLimit active tracked tickets (oldest due first).</summary>
public sealed record SlaOverview(
    int WarningCount, int BreachedCount, int OnTrackCount, IReadOnlyList<SlaOverviewTicket> Warning,
    IReadOnlyList<SlaOverviewTicket> Breached, IReadOnlyList<AutomationExecutionDetails> RecentExecutions, DateTimeOffset GeneratedAtUtc);

public sealed record SlaOverviewTicket(Guid TicketId, string Subject, TicketStatus Status, string? AssignedAgentName, TicketSlaView Sla);
