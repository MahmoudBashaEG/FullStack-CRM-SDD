namespace Crm.Domain.Tickets;

public sealed class TicketHistoryEntry
{
    private TicketHistoryEntry() { }
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public long Revision { get; private set; }
    public int Sequence { get; private set; }
    public string Kind { get; private set; } = "";
    public string? Field { get; private set; }
    public TicketChangeValue? PreviousValue { get; private set; }
    public TicketChangeValue? NewValue { get; private set; }
    public string ActorIssuer { get; private set; } = "";
    public string ActorSubject { get; private set; } = "";
    public string ActorDisplayName { get; private set; } = "";
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public static TicketHistoryEntry Created(Ticket ticket, CategorySnapshot category, ActorSnapshot actor) => new()
    {
        Id = Guid.NewGuid(), TicketId = ticket.Id, Revision = 1, Sequence = 1, Kind = "Created", Field = null, PreviousValue = null,
        NewValue = new TicketCreationValue(ticket.Reference, ticket.Subject, ticket.Description, category, ticket.Priority.ToString(), ticket.Status.ToString(), null),
        ActorIssuer = actor.Issuer, ActorSubject = actor.Subject, ActorDisplayName = string.IsNullOrWhiteSpace(actor.DisplayName) ? actor.Subject : actor.DisplayName,
        OccurredAtUtc = ticket.CreatedAtUtc
    };
}
