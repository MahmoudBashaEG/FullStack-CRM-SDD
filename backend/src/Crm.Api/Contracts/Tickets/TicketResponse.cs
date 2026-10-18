using Crm.Api.Contracts.Sla;
using Crm.Application.Tickets;
using Crm.Domain.Tickets;

namespace Crm.Api.Contracts.Tickets;

/// <summary>Customer / creator / assigned Agent / escalating user are shown by id + display name only (no Identity fields).
/// Sla is null when the ticket is not SLA-tracked (created before SLA tracking or while no SLA policy was enabled). EscalatedByName null on an escalated ticket = Automation.</summary>
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
    string? EscalatedByName,
    TicketSlaResponse? Sla)
{
    public static TicketResponse From(TicketDetails t) =>
        new(t.Id, t.Subject, t.Description, t.Category, t.Priority, t.Status, t.CustomerId, t.CustomerName, t.CustomerEmail,
            t.CreatedByUserId, t.CreatedByName, t.AssignedAgentUserId, t.AssignedAgentName, t.CreatedAtUtc, t.UpdatedAtUtc,
            t.IsEscalated, t.EscalationReason, t.EscalatedAtUtc, t.EscalatedByUserId, t.EscalatedByName,
            t.Sla is null ? null : TicketSlaResponse.From(t.Sla));
}
