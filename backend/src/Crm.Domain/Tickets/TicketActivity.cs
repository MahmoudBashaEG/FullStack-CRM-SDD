namespace Crm.Domain.Tickets;

/// <summary>
/// Append-only history entry, created only by Ticket's own operations (so a change cannot be saved without its event).
/// Id is a store-generated identity (insertion order = deterministic tie-breaker). Never updated or deleted.
/// User ids reference AspNetUsers; display names are joined at read time.
/// </summary>
public sealed class TicketActivity
{
    private TicketActivity() { } // EF Core

    public long Id { get; private set; }
    public Guid TicketId { get; private set; }
    public TicketActivityType Type { get; private set; }
    public Guid ActorUserId { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public TicketField? Field { get; private set; }
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public Guid? FromAgentUserId { get; private set; }
    public Guid? ToAgentUserId { get; private set; }
    public string? Reason { get; private set; }

    internal static TicketActivity Created(Guid ticketId, Guid actorUserId, Guid? assignedAgentUserId, DateTimeOffset now) =>
        New(ticketId, TicketActivityType.Created, actorUserId, now, toAgentUserId: assignedAgentUserId);

    internal static TicketActivity FieldChanged(Guid ticketId, Guid actorUserId, DateTimeOffset now, TicketField field, string? oldValue, string? newValue) =>
        New(ticketId, TicketActivityType.FieldChanged, actorUserId, now, field: field, oldValue: oldValue, newValue: newValue);

    internal static TicketActivity AssigneeChanged(Guid ticketId, Guid actorUserId, DateTimeOffset now, Guid? fromAgentUserId, Guid? toAgentUserId) =>
        New(ticketId, TicketActivityType.AssigneeChanged, actorUserId, now, fromAgentUserId: fromAgentUserId, toAgentUserId: toAgentUserId);

    internal static TicketActivity StatusChanged(Guid ticketId, Guid actorUserId, DateTimeOffset now, TicketStatus from, TicketStatus to) =>
        New(ticketId, TicketActivityType.StatusChanged, actorUserId, now, oldValue: from.ToString(), newValue: to.ToString());

    internal static TicketActivity Escalated(Guid ticketId, Guid actorUserId, DateTimeOffset now, string reason) =>
        New(ticketId, TicketActivityType.Escalated, actorUserId, now, reason: reason);

    internal static TicketActivity EscalationCleared(Guid ticketId, Guid actorUserId, DateTimeOffset now, bool automatic) =>
        New(ticketId, automatic ? TicketActivityType.EscalationAutoCleared : TicketActivityType.EscalationCleared, actorUserId, now);

    private static TicketActivity New(
        Guid ticketId, TicketActivityType type, Guid actorUserId, DateTimeOffset now,
        TicketField? field = null, string? oldValue = null, string? newValue = null,
        Guid? fromAgentUserId = null, Guid? toAgentUserId = null, string? reason = null) =>
        new()
        {
            TicketId = ticketId,
            Type = type,
            ActorUserId = actorUserId,
            OccurredAtUtc = now,
            Field = field,
            OldValue = oldValue,
            NewValue = newValue,
            FromAgentUserId = fromAgentUserId,
            ToAgentUserId = toAgentUserId,
            Reason = reason,
        };
}
