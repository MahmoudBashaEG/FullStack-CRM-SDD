namespace Crm.Domain.Tickets;

/// <summary>
/// Support ticket for a CRM customer (Customers.Id — registered or contact-only, never AspNetUsers.Id).
/// CreatedByUserId / AssignedAgentUserId are Identity user ids set by the server only.
/// TICK-001: customer, creator, assignment and status never change after creation.
/// </summary>
public sealed class Ticket
{
    private Ticket() { } // EF Core

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public TicketCategory Category { get; private set; }
    public TicketPriority Priority { get; private set; }
    public TicketStatus Status { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid? AssignedAgentUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>New tickets are always Open. The caller has already resolved the initial assignee.</summary>
    public static Ticket Create(
        Guid id,
        Guid customerId,
        string subject,
        string description,
        TicketCategory category,
        TicketPriority priority,
        Guid createdByUserId,
        Guid? assignedAgentUserId,
        DateTimeOffset now) =>
        new()
        {
            Id = id,
            CustomerId = customerId,
            Subject = subject,
            Description = description,
            Category = category,
            Priority = priority,
            Status = TicketStatus.Open,
            CreatedByUserId = createdByUserId,
            AssignedAgentUserId = assignedAgentUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

    /// <summary>TICK-001 basic edit: only these four fields.</summary>
    public void UpdateDetails(string subject, string description, TicketCategory category, TicketPriority priority, DateTimeOffset now)
    {
        Subject = subject;
        Description = description;
        Category = category;
        Priority = priority;
        UpdatedAtUtc = now;
    }
}
