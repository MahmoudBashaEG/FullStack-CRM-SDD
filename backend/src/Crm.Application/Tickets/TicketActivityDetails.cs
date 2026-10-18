using Crm.Domain.Tickets;

namespace Crm.Application.Tickets;

/// <summary>History read model. Names joined from AspNetUsers at query time; no user ids or Identity fields exposed.
/// IsAutomatic = performed by SLA-001 automation; ActorName is then "Automation".</summary>
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
    string? Reason,
    bool IsAutomatic);
