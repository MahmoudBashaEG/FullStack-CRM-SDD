using Crm.Api.Contracts.Automation;
using Crm.Application.Sla;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.Api.Contracts.Sla;

/// <summary>Priority is nullable only so a missing JSON field fails validation (400) instead of defaulting to Low.</summary>
public sealed record SlaTargetDto(TicketPriority? Priority, int FirstResponseMinutes, int ResolutionMinutes);

public sealed record UpdateSlaPolicyRequest(bool? IsEnabled, IReadOnlyList<SlaTargetDto>? Targets);

public sealed record SlaPolicyResponse(
    bool IsConfigured, bool IsEnabled, IReadOnlyList<SlaTargetDto> Targets, DateTimeOffset? UpdatedAtUtc, string? UpdatedByName)
{
    public static SlaPolicyResponse From(SlaPolicyDetails p) =>
        new(p.IsConfigured, p.IsEnabled, p.Targets.Select(t => new SlaTargetDto(t.Priority, t.FirstResponseMinutes, t.ResolutionMinutes)).ToList(),
            p.UpdatedAtUtc, p.UpdatedByName);
}

public sealed record HolidayDto(DateOnly? Date, string? Name);

/// <summary>Times are "HH:mm:ss"; working days are DayOfWeek names ("Sunday"…"Saturday"); holiday dates "yyyy-MM-dd".</summary>
public sealed record UpdateBusinessCalendarRequest(
    string TimeZoneId, IReadOnlyList<DayOfWeek>? WorkingDays, TimeOnly? OpensAt, TimeOnly? ClosesAt, IReadOnlyList<HolidayDto>? Holidays);

public sealed record BusinessCalendarResponse(
    bool IsConfigured, string? TimeZoneId, IReadOnlyList<DayOfWeek> WorkingDays, TimeOnly? OpensAt, TimeOnly? ClosesAt,
    IReadOnlyList<HolidayDto> Holidays, DateTimeOffset? UpdatedAtUtc, string? UpdatedByName)
{
    public static BusinessCalendarResponse From(BusinessCalendarDetails c) =>
        new(c.IsConfigured, c.TimeZoneId, c.WorkingDays, c.OpensAt, c.ClosesAt,
            c.Holidays.Select(h => new HolidayDto(h.Date, h.Name)).ToList(), c.UpdatedAtUtc, c.UpdatedByName);
}

public sealed record SlaDuePreviewResponse(DateTimeOffset StartUtc, int Minutes, DateTimeOffset DueAtUtc, string TimeZoneId)
{
    public static SlaDuePreviewResponse From(SlaDuePreview p) => new(p.StartUtc, p.Minutes, p.DueAtUtc, p.TimeZoneId);
}

/// <summary>BusinessMinutesRemaining: positive = left, negative = overdue, null once completed or without a calendar.</summary>
public sealed record SlaTargetResponse(
    SlaKind Kind, int TargetMinutes, DateTimeOffset DueAtUtc, DateTimeOffset? CompletedAtUtc, string? CompletedByName,
    SlaTargetState State, bool IsWarning, int? BusinessMinutesRemaining, int BusinessMinutesElapsed)
{
    public static SlaTargetResponse From(SlaTargetView t) =>
        new(t.Kind, t.TargetMinutes, t.DueAtUtc, t.CompletedAtUtc, t.CompletedByName, t.State, t.IsWarning, t.BusinessMinutesRemaining,
            t.BusinessMinutesElapsed);
}

public sealed record TicketSlaResponse(SlaIndicator Indicator, string TimeZoneId, SlaTargetResponse FirstResponse, SlaTargetResponse Resolution)
{
    public static TicketSlaResponse From(TicketSlaView s) =>
        new(s.Indicator, s.TimeZoneId, SlaTargetResponse.From(s.FirstResponse), SlaTargetResponse.From(s.Resolution));
}

public sealed record SlaOverviewTicketResponse(Guid TicketId, string Subject, TicketStatus Status, string? AssignedAgentName, TicketSlaResponse Sla)
{
    public static SlaOverviewTicketResponse From(SlaOverviewTicket t) =>
        new(t.TicketId, t.Subject, t.Status, t.AssignedAgentName, TicketSlaResponse.From(t.Sla));
}

/// <summary>Counts cover the first 500 active tracked tickets (oldest due first); the lists show at most 50 each.</summary>
public sealed record SlaOverviewResponse(
    int WarningCount, int BreachedCount, int OnTrackCount, IReadOnlyList<SlaOverviewTicketResponse> Warning,
    IReadOnlyList<SlaOverviewTicketResponse> Breached, IReadOnlyList<AutomationExecutionResponse> RecentExecutions, DateTimeOffset GeneratedAtUtc)
{
    public static SlaOverviewResponse From(SlaOverview o) =>
        new(o.WarningCount, o.BreachedCount, o.OnTrackCount, o.Warning.Select(SlaOverviewTicketResponse.From).ToList(),
            o.Breached.Select(SlaOverviewTicketResponse.From).ToList(), o.RecentExecutions.Select(AutomationExecutionResponse.From).ToList(),
            o.GeneratedAtUtc);
}
