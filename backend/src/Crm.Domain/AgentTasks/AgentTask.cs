using Crm.Domain.Common;

namespace Crm.Domain.AgentTasks;

/// <summary>
/// Agent work item (DASH-002). AssignedAgentUserId / CreatedByUserId are Identity user ids set by the server only.
/// TicketId optionally references Tickets.Id (no ticket data is copied). Never hard-deleted: archive instead.
/// Reminder: at most one; RemindAtUtc null = none. ReminderGeneration increases on every genuine (re)schedule so that
/// each generation is issued at most once (unique index on TaskReminderNotifications).
/// Version: SQL rowversion, exposed to clients as the optimistic-concurrency token (ETag / If-Match).
/// </summary>
public sealed class AgentTask
{
    private const string ArchivedMessage = "Restore the task before editing it.";

    private AgentTask() { } // EF Core

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public AgentTaskStatus Status { get; private set; }
    public Guid AssignedAgentUserId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid? TicketId { get; private set; }
    public DateTimeOffset? DueAtUtc { get; private set; }
    public DateTimeOffset? RemindAtUtc { get; private set; }
    public int ReminderGeneration { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTimeOffset? ArchivedAtUtc { get; private set; }
    public byte[] Version { get; private set; } = []; // store-generated rowversion

    public bool IsOpenForReminders => Status == AgentTaskStatus.Pending && !IsArchived;

    /// <summary>New tasks are Pending. The caller has validated owner, ticket link and reminder.</summary>
    public static AgentTask Create(
        Guid id, string title, string? description, Guid assignedAgentUserId, Guid createdByUserId,
        Guid? ticketId, DateTimeOffset? dueAtUtc, DateTimeOffset? remindAtUtc, DateTimeOffset now) =>
        new()
        {
            Id = id,
            Title = title,
            Description = description,
            Status = AgentTaskStatus.Pending,
            AssignedAgentUserId = assignedAgentUserId,
            CreatedByUserId = createdByUserId,
            TicketId = ticketId,
            DueAtUtc = dueAtUtc,
            RemindAtUtc = remindAtUtc,
            ReminderGeneration = remindAtUtc is null ? 0 : 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

    public bool UpdateDetails(string title, string? description, DateTimeOffset? dueAtUtc, DateTimeOffset now)
    {
        EnsureEditable();
        if (string.Equals(Title, title, StringComparison.Ordinal)
            && string.Equals(Description, description, StringComparison.Ordinal)
            && DueAtUtc == dueAtUtc)
        {
            return false;
        }

        Title = title;
        Description = description;
        DueAtUtc = dueAtUtc;
        UpdatedAtUtc = now;
        return true;
    }

    /// <summary>null = unlink.</summary>
    public bool LinkTicket(Guid? ticketId, DateTimeOffset now)
    {
        EnsureEditable();
        if (TicketId == ticketId)
        {
            return false;
        }

        TicketId = ticketId;
        UpdatedAtUtc = now;
        return true;
    }

    /// <summary>null = cancel (generation unchanged). A different time starts a new generation; the same time is a no-op.</summary>
    public bool ScheduleReminder(DateTimeOffset? remindAtUtc, DateTimeOffset now)
    {
        if (RemindAtUtc == remindAtUtc)
        {
            return false;
        }

        if (remindAtUtc is not null)
        {
            if (!IsOpenForReminders)
            {
                throw new DomainException("Reopen or restore the task before scheduling a reminder.");
            }

            ReminderGeneration++;
        }

        RemindAtUtc = remindAtUtc;
        UpdatedAtUtc = now;
        return true;
    }

    public bool Complete(DateTimeOffset now)
    {
        EnsureEditable();
        if (Status == AgentTaskStatus.Completed)
        {
            return false;
        }

        Status = AgentTaskStatus.Completed;
        CompletedAtUtc = now;
        RemindAtUtc = null;
        UpdatedAtUtc = now;
        return true;
    }

    public bool Reopen(DateTimeOffset now)
    {
        EnsureEditable();
        if (Status == AgentTaskStatus.Pending)
        {
            return false;
        }

        Status = AgentTaskStatus.Pending;
        CompletedAtUtc = null; // the reminder is not recreated
        UpdatedAtUtc = now;
        return true;
    }

    public bool Archive(DateTimeOffset now)
    {
        if (IsArchived)
        {
            return false;
        }

        IsArchived = true;
        ArchivedAtUtc = now;
        RemindAtUtc = null;
        UpdatedAtUtc = now;
        return true;
    }

    public bool Restore(DateTimeOffset now)
    {
        if (!IsArchived)
        {
            return false;
        }

        IsArchived = false;
        ArchivedAtUtc = null; // the reminder is not recreated
        UpdatedAtUtc = now;
        return true;
    }

    /// <summary>Reminder fields stay untouched: the recipient is resolved at issuance.</summary>
    public bool Reassign(Guid assignedAgentUserId, bool removeTicketLink, DateTimeOffset now)
    {
        var ownerChanged = AssignedAgentUserId != assignedAgentUserId;
        var linkChanged = removeTicketLink && TicketId is not null;
        if (!ownerChanged && !linkChanged)
        {
            return false;
        }

        EnsureEditable();
        AssignedAgentUserId = assignedAgentUserId;
        if (removeTicketLink)
        {
            TicketId = null;
        }

        UpdatedAtUtc = now;
        return true;
    }

    private void EnsureEditable()
    {
        if (IsArchived)
        {
            throw new DomainException(ArchivedMessage);
        }
    }
}
