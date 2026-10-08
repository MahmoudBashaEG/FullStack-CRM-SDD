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
    public void UpdateDetails(string subject, string description, CategorySnapshot previousCategory, CategorySnapshot category, TicketPriority priority, ActorSnapshot actor, DateTimeOffset now)
    {
        if (Status == TicketStatus.Closed) throw new TicketClosedException();
        ValidateActor(actor);
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(description) || !Enum.IsDefined(priority) || category.Id == Guid.Empty || string.IsNullOrWhiteSpace(category.DisplayName) || previousCategory.Id != CategoryId) throw new ArgumentException("Valid details and category snapshots are required.");
        subject = subject.Trim(); description = description.Trim();
        var changes = new List<(string Field, TicketChangeValue Before, TicketChangeValue After)>();
        if (subject != Subject) changes.Add(("subject", new TicketTextValue(Subject), new TicketTextValue(subject)));
        if (description != Description) changes.Add(("description", new TicketTextValue(Description), new TicketTextValue(description)));
        if (category.Id != CategoryId) changes.Add(("category", new TicketReferenceValue(previousCategory.Id, previousCategory.DisplayName), new TicketReferenceValue(category.Id, category.DisplayName)));
        if (priority != Priority) changes.Add(("priority", new TicketTextValue(Priority.ToString()), new TicketTextValue(priority.ToString())));
        if (changes.Count == 0) return;
        Revision = checked(Revision + 1); UpdatedAtUtc = now.ToUniversalTime();
        Subject = subject; Description = description; CategoryId = category.Id; Priority = priority;
        var sequence = 0;
        foreach (var change in changes) pendingHistory.Add(TicketHistoryEntry.Changed(this, ++sequence, "DetailsChanged", change.Field, change.Before, change.After, actor));
    }
    public void Assign(AgentSnapshot? previous, AgentSnapshot? next, ActorSnapshot actor, DateTimeOffset now)
    {
        if (Status == TicketStatus.Closed) throw new TicketClosedException();
        ValidateActor(actor);
        if (previous?.Id != AssignedAgentId || next is not null && (next.Id == Guid.Empty || string.IsNullOrWhiteSpace(next.DisplayName))) throw new ArgumentException("Valid assignment snapshots are required.");
        if (next?.Id == AssignedAgentId) return;
        Revision = checked(Revision + 1); UpdatedAtUtc = now.ToUniversalTime(); AssignedAgentId = next?.Id;
        pendingHistory.Add(TicketHistoryEntry.Changed(this, 1, "AssignmentChanged", "assignedAgent", previous is null ? null : new TicketReferenceValue(previous.Id, previous.DisplayName), next is null ? null : new TicketReferenceValue(next.Id, next.DisplayName), actor));
    }
    public void AcknowledgePersistedHistory() => pendingHistory.Clear();
    private static void ValidateActor(ActorSnapshot actor)
    {
        if (string.IsNullOrWhiteSpace(actor.Issuer) || string.IsNullOrWhiteSpace(actor.Subject)) throw new ArgumentException("A trusted actor is required.");
    }
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
