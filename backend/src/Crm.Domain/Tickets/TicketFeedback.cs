using Crm.Domain.Common;

namespace Crm.Domain.Tickets;

/// <summary>
/// PORTAL-001 CSAT: at most one record per ticket (unique TicketId), created and edited only by the owning customer while the
/// ticket is Resolved or Closed. Ownership comes from Tickets.CustomerId (no copy). Never deleted; never changes the ticket.
/// Id is a store-generated identity.
/// </summary>
public sealed class TicketFeedback
{
    public const int MinRating = 1;
    public const int MaxRating = 5;

    private TicketFeedback() { } // EF Core

    public long Id { get; private set; }
    public Guid TicketId { get; private set; }
    public int Rating { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Feedback can be created or edited only while the ticket's current status is Resolved or Closed (CP5).</summary>
    public static bool IsEligible(TicketStatus status) => status is TicketStatus.Resolved or TicketStatus.Closed;

    public static TicketFeedback Create(Guid ticketId, int rating, string? comment, DateTimeOffset now)
    {
        EnsureRating(rating);
        return new() { TicketId = ticketId, Rating = rating, Comment = comment, CreatedAtUtc = now, UpdatedAtUtc = now };
    }

    /// <summary>false = nothing changed (no save, no timestamp change).</summary>
    public bool Update(int rating, string? comment, DateTimeOffset now)
    {
        EnsureRating(rating);
        if (Rating == rating && string.Equals(Comment, comment, StringComparison.Ordinal))
        {
            return false;
        }

        Rating = rating;
        Comment = comment;
        UpdatedAtUtc = now;
        return true;
    }

    // Backstop only: the validator returns 400 first.
    private static void EnsureRating(int rating)
    {
        if (rating is < MinRating or > MaxRating)
        {
            throw new DomainException($"A rating must be between {MinRating} and {MaxRating}.");
        }
    }
}
