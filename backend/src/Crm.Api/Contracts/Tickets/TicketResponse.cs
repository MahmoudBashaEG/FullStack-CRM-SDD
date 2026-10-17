using Crm.Application.Tickets;
using Crm.Domain.Tickets;

namespace Crm.Api.Contracts.Tickets;

/// <summary>Customer / creator / assigned Agent / escalating user are shown by id + display name only (no Identity fields).</summary>
public sealed record TicketResponse(
    Guid Id,
    string Subject,
    string Description,
    TicketCategory Category,
    TicketPriority Priority,
    TicketStatus Status,
    Guid CustomerId,
    string CustomerName,
    string CustomerEmail,
    Guid CreatedByUserId,
    string CreatedByName,
    Guid? AssignedAgentUserId,
    string? AssignedAgentName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    bool IsEscalated,
    string? EscalationReason,
    DateTimeOffset? EscalatedAtUtc,
    Guid? EscalatedByUserId,
    string? EscalatedByName)
{
    public static TicketResponse From(TicketDetails t) =>
        new(t.Id, t.Subject, t.Description, t.Category, t.Priority, t.Status, t.CustomerId, t.CustomerName, t.CustomerEmail,
            t.CreatedByUserId, t.CreatedByName, t.AssignedAgentUserId, t.AssignedAgentName, t.CreatedAtUtc, t.UpdatedAtUtc,
            t.IsEscalated, t.EscalationReason, t.EscalatedAtUtc, t.EscalatedByUserId, t.EscalatedByName);
}
