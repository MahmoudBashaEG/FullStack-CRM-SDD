using Crm.Domain.Common;
using Crm.Domain.Tickets;

namespace Crm.Domain.QuickReplies;

/// <summary>
/// Plain-text reply template (DASH-002 D2). Scope and OwnerUserId are fixed at creation (Personal ⇒ owner; Shared ⇒ no owner)
/// and no method changes them. Never hard-deleted: archive instead.
/// </summary>
public sealed class QuickReplyTemplate
{
    private QuickReplyTemplate() { } // EF Core

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public TicketCategory Category { get; private set; }
    public QuickReplyScope Scope { get; private set; }
    public Guid? OwnerUserId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public static QuickReplyTemplate CreatePersonal(
        Guid id, Guid ownerUserId, string title, string body, TicketCategory category, DateTimeOffset now) =>
        new()
        {
            Id = id, Title = title, Body = body, Category = category, Scope = QuickReplyScope.Personal,
            OwnerUserId = ownerUserId, CreatedByUserId = ownerUserId, UpdatedByUserId = ownerUserId,
            CreatedAtUtc = now, UpdatedAtUtc = now,
        };

    public static QuickReplyTemplate CreateShared(
        Guid id, Guid superAdminUserId, string title, string body, TicketCategory category, DateTimeOffset now) =>
        new()
        {
            Id = id, Title = title, Body = body, Category = category, Scope = QuickReplyScope.Shared,
            OwnerUserId = null, CreatedByUserId = superAdminUserId, UpdatedByUserId = superAdminUserId,
            CreatedAtUtc = now, UpdatedAtUtc = now,
        };

    public bool Update(string title, string body, TicketCategory category, Guid actorUserId, DateTimeOffset now)
    {
        if (IsArchived)
        {
            throw new DomainException("Restore the quick reply before editing it.");
        }

        if (string.Equals(Title, title, StringComparison.Ordinal)
            && string.Equals(Body, body, StringComparison.Ordinal)
            && Category == category)
        {
            return false;
        }

        Title = title;
        Body = body;
        Category = category;
        UpdatedByUserId = actorUserId;
        UpdatedAtUtc = now;
        return true;
    }

    public bool Archive(Guid actorUserId, DateTimeOffset now)
    {
        if (IsArchived)
        {
            return false;
        }

        IsArchived = true;
        ArchivedAtUtc = now;
        UpdatedByUserId = actorUserId;
        UpdatedAtUtc = now;
        return true;
    }

    public bool Restore(Guid actorUserId, DateTimeOffset now)
    {
        if (!IsArchived)
        {
            return false;
        }

        IsArchived = false;
        ArchivedAtUtc = null;
        UpdatedByUserId = actorUserId;
        UpdatedAtUtc = now;
        return true;
    }
}
