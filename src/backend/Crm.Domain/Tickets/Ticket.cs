namespace Crm.Domain.Tickets;

public sealed record CategorySnapshot(Guid Id, string DisplayName);
public sealed record ActorSnapshot(string Issuer, string Subject, string DisplayName);
public sealed class Ticket
{
    public Guid Id { get; private set; }
    public string Reference { get; private set; } = "";
    public string Subject { get; private set; } = "";
    public string Description { get; private set; } = "";
    public Guid CategoryId { get; private set; }
    public TicketPriority Priority { get; private set; }
    public TicketStatus Status { get; private set; }
    public Guid? AssignedAgentId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public long Revision { get; private set; }
    private readonly List<TicketHistoryEntry> pendingHistory = [];
    public IReadOnlyList<TicketHistoryEntry> PendingHistory => pendingHistory.AsReadOnly();
    private Ticket() { }
    public static Ticket Create(string subject, string description, CategorySnapshot category, TicketPriority priority, ActorSnapshot actor, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(description) || !Enum.IsDefined(priority) || category.Id == Guid.Empty || string.IsNullOrWhiteSpace(category.DisplayName) ||
            string.IsNullOrWhiteSpace(actor.Issuer) || string.IsNullOrWhiteSpace(actor.Subject)) throw new ArgumentException("Valid ticket content, reference and actor are required.");
        var id = Guid.NewGuid();
        var ticket = new Ticket
        {
            Id = id, Reference = "TKT-" + id.ToString("N").ToUpperInvariant(), Subject = subject.Trim(), Description = description.Trim(),
            CategoryId = category.Id, Priority = priority, Status = TicketStatus.Open, AssignedAgentId = null,
            CreatedAtUtc = now.ToUniversalTime(), UpdatedAtUtc = now.ToUniversalTime(), Revision = 1
        };
        ticket.pendingHistory.Add(TicketHistoryEntry.Created(ticket, category, actor));
        return ticket;
    }
}
