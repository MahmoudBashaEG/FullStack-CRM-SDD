using Crm.Application.Tickets;
using Crm.Domain.Tickets;

namespace Crm.Api.Contracts.Tickets;

/// <summary>History entry with display names only (no user ids or Identity fields). IsAutomatic = performed by SLA automation (ActorName "Automation").</summary>
public sealed record TicketActivityResponse(
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
    bool IsAutomatic)
{
    public static TicketActivityResponse From(TicketActivityDetails a) =>
        new(a.Id, a.Type, a.OccurredAtUtc, a.ActorName, a.Field, a.OldValue, a.NewValue, a.FromAgentName, a.ToAgentName, a.Reason, a.IsAutomatic);
}
