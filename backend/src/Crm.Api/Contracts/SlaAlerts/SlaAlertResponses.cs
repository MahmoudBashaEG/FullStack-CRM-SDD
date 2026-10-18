using Crm.Application.SlaAlerts;
using Crm.Domain.Automation;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.Api.Contracts.SlaAlerts;

/// <summary>No recipient id is exposed.</summary>
public sealed record SlaAlertResponse(
    long Id,
    Guid TicketId,
    string TicketSubject,
    TicketStatus TicketStatus,
    SlaAlertType Type,
    SlaKind Kind,
    DateTimeOffset DueAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReadAtUtc,
    bool IsRead)
{
    public static SlaAlertResponse From(SlaAlertDetails a) =>
        new(a.Id, a.TicketId, a.TicketSubject, a.TicketStatus, a.Type, a.Kind, a.DueAtUtc, a.CreatedAtUtc, a.ReadAtUtc, a.ReadAtUtc is not null);
}

public sealed record SlaAlertCountResponse(int UnreadCount);
