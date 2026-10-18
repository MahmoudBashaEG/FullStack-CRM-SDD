namespace Crm.Domain.Collaboration;

/// <summary>
/// Append-only internal staff message on a ticket (DASH-002 D5). Never edited or deleted (no methods exist for it).
/// Not a TicketActivity and never changes the ticket. Id is a store-generated identity (insertion order breaks ties).
/// </summary>
public sealed class TicketInternalMessage
{
    private TicketInternalMessage() { } // EF Core

    public long Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static TicketInternalMessage Post(Guid ticketId, Guid authorUserId, string body, DateTimeOffset now) =>
        new() { TicketId = ticketId, AuthorUserId = authorUserId, Body = body, CreatedAtUtc = now };
}
