using Crm.Domain.Automation;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.Application.SlaAlerts;

/// <summary>Alert read model. No recipient id is exposed.</summary>
public sealed record SlaAlertDetails(
    long Id,
    Guid TicketId,
    string TicketSubject,
    TicketStatus TicketStatus,
    SlaAlertType Type,
    SlaKind Kind,
    DateTimeOffset DueAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReadAtUtc);
