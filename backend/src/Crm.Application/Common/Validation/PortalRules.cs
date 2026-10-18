using Crm.Domain.Tickets;

namespace Crm.Application.Common.Validation;

public static class PortalRules
{
    public const int FeedbackCommentMaxLength = 1000;
    public const int MinRating = TicketFeedback.MinRating;
    public const int MaxRating = TicketFeedback.MaxRating;
    /// <summary>Newest status events kept in one timeline read (CP3 bounded read).</summary>
    public const int MaxTimelineEvents = 200;
}
