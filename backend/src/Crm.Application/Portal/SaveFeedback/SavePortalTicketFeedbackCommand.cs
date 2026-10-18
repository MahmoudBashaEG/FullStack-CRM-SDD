using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Domain.Tickets;
using FluentValidation;
using Mediator;

namespace Crm.Application.Portal.SaveFeedback;

/// <summary>
/// CP5 create-or-update of the single feedback row of an owned Resolved/Closed ticket. Single save. Rating nullable so a missing
/// field fails validation. Never touches the ticket, its activity, SLA or assignment (P6/P7).
/// </summary>
public sealed record SavePortalTicketFeedbackCommand(Guid UserId, Guid TicketId, int? Rating, string? Comment)
    : ICommand<PortalTicketDetails>;

public sealed class SavePortalTicketFeedbackCommandHandler(
    ICustomerRepository customers,
    IPortalTicketRepository portal,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<SavePortalTicketFeedbackCommand, PortalTicketDetails>
{
    public async ValueTask<PortalTicketDetails> Handle(SavePortalTicketFeedbackCommand command, CancellationToken cancellationToken)
    {
        var customerId = await customers.ResolveIdAsync(command.UserId, cancellationToken);
        var ticket = await portal.FindAsync(command.TicketId, customerId, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{command.TicketId}' was not found.");
        if (!ticket.CanSubmitFeedback)
        {
            throw new ConflictException("Feedback can be given once the request is resolved or closed.");
        }

        var rating = command.Rating!.Value; // validator: NotNull + range
        var comment = string.IsNullOrWhiteSpace(command.Comment) ? null : command.Comment.Trim();
        var now = timeProvider.GetUtcNow();
        var existing = await portal.GetFeedbackAsync(command.TicketId, cancellationToken);
        if (existing is null)
        {
            portal.AddFeedback(TicketFeedback.Create(command.TicketId, rating, comment, now));
            await unitOfWork.SaveChangesAsync(cancellationToken); // racing first insert ⇒ unique index ⇒ 409
        }
        else if (existing.Update(rating, comment, now))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken); // overlapping edit ⇒ rowversion ⇒ 409
        }

        return (await portal.FindAsync(command.TicketId, customerId, cancellationToken))!;
    }
}

public sealed class SavePortalTicketFeedbackValidator : AbstractValidator<SavePortalTicketFeedbackCommand>
{
    public SavePortalTicketFeedbackValidator()
    {
        RuleFor(c => c.TicketId).NotEmpty();
        RuleFor(c => c.Rating).NotNull().InclusiveBetween(PortalRules.MinRating, PortalRules.MaxRating);
        RuleFor(c => c.Comment).MaximumLength(PortalRules.FeedbackCommentMaxLength);
    }
}
