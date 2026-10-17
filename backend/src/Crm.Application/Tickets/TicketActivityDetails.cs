using Crm.Domain.Tickets;

namespace Crm.Application.Tickets;

/// <summary>History read model. Names joined from AspNetUsers at query time; no user ids or Identity fields exposed.</summary>
public sealed record TicketActivityDetails(
    long Id,
    TicketActivityType Type,
    DateTimeOffset OccurredAtUtc,
    string ActorName,
    TicketField? Field,
    string? OldValue,
    string? NewValue,
    string? FromAgentName,
    string? ToAgentName,
    string? Reason);
