using Crm.Domain.Common;

namespace Crm.Domain.Tickets;

/// <summary>
/// Support ticket for a CRM customer (Customers.Id — registered or contact-only, never AspNetUsers.Id).
/// CreatedByUserId / AssignedAgentUserId are Identity user ids set by the server only.
/// TICK-002: customer and creator never change; assignment, status and escalation change only through the operations below,
/// each of which appends its own TicketActivity.
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
    public bool IsEscalated { get; private set; }
    public string? EscalationReason { get; private set; }
    public DateTimeOffset? EscalatedAtUtc { get; private set; }
    public Guid? EscalatedByUserId { get; private set; }

    // History owned by the aggregate. Loaded tickets start with an EMPTY list (never Include-d):
    // it only holds the events appended in this unit of work, which EF inserts on save.
    private readonly List<TicketActivity> _activities = [];
    public IReadOnlyCollection<TicketActivity> Activities => _activities;

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
        DateTimeOffset now)
    {
        var ticket = new Ticket
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
        ticket.Record(TicketActivity.Created(id, createdByUserId, assignedAgentUserId, now));
        return ticket;
    }

    /// <summary>Basic edit (only these four fields). One FieldChanged event per changed field; false = nothing changed (no event, no timestamp).</summary>
    public bool UpdateDetails(
        string subject, string description, TicketCategory category, TicketPriority priority, Guid actorUserId, DateTimeOffset now)
    {
        var changed = false;
        if (!string.Equals(Subject, subject, StringComparison.Ordinal))
        {
            Record(TicketActivity.FieldChanged(Id, actorUserId, now, TicketField.Subject, Subject, subject));
            Subject = subject;
            changed = true;
        }

        if (!string.Equals(Description, description, StringComparison.Ordinal))
        {
            // Values not copied into history (up to 4000 chars each); the event only says it changed.
            Record(TicketActivity.FieldChanged(Id, actorUserId, now, TicketField.Description, null, null));
            Description = description;
            changed = true;
        }

        if (Category != category)
        {
            Record(TicketActivity.FieldChanged(Id, actorUserId, now, TicketField.Category, Category.ToString(), category.ToString()));
            Category = category;
            changed = true;
        }

        if (Priority != priority)
        {
            Record(TicketActivity.FieldChanged(Id, actorUserId, now, TicketField.Priority, Priority.ToString(), priority.ToString()));
            Priority = priority;
            changed = true;
        }

        if (changed)
        {
            UpdatedAtUtc = now;
        }

        return changed;
    }

    /// <summary>The caller has verified the target is an active Agent (or null = unassigned). false = same assignee (no-op).</summary>
    public bool ChangeAssignee(Guid? assignedAgentUserId, Guid actorUserId, DateTimeOffset now)
    {
        if (AssignedAgentUserId == assignedAgentUserId)
        {
            return false;
        }

        Record(TicketActivity.AssigneeChanged(Id, actorUserId, now, AssignedAgentUserId, assignedAgentUserId));
        AssignedAgentUserId = assignedAgentUserId;
        UpdatedAtUtc = now;
        return true;
    }

    /// <summary>Resolving an escalated ticket also clears the escalation (recorded as EscalationAutoCleared).</summary>
    public void TransitionTo(TicketStatus status, Guid actorUserId, DateTimeOffset now)
    {
        if (!TicketWorkflow.IsAllowed(Status, status))
        {
            throw new DomainException($"A ticket cannot move from {Status} to {status}.");
        }

        Record(TicketActivity.StatusChanged(Id, actorUserId, now, Status, status));
        Status = status;
        if (status == TicketStatus.Resolved && IsEscalated)
        {
            ClearEscalationState();
            Record(TicketActivity.EscalationCleared(Id, actorUserId, now, automatic: true));
        }

        UpdatedAtUtc = now;
    }

    public void Escalate(string reason, Guid actorUserId, DateTimeOffset now)
    {
        if (IsEscalated || !TicketWorkflow.CanEscalate(Status))
        {
            throw new DomainException("Only an Open or In Progress ticket that is not already escalated can be escalated.");
        }

        IsEscalated = true;
        EscalationReason = reason;
        EscalatedAtUtc = now;
        EscalatedByUserId = actorUserId;
        Record(TicketActivity.Escalated(Id, actorUserId, now, reason));
        UpdatedAtUtc = now;
    }

    public void ClearEscalation(Guid actorUserId, DateTimeOffset now)
    {
        if (!IsEscalated)
        {
            throw new DomainException("The ticket is not escalated.");
        }

        ClearEscalationState();
        Record(TicketActivity.EscalationCleared(Id, actorUserId, now, automatic: false));
        UpdatedAtUtc = now;
    }

    private void ClearEscalationState()
    {
        IsEscalated = false;
        EscalationReason = null;
        EscalatedAtUtc = null;
        EscalatedByUserId = null;
    }

    private void Record(TicketActivity activity) => _activities.Add(activity);
}
