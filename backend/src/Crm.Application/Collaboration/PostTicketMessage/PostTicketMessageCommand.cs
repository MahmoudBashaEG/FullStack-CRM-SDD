using Crm.Application.Abstractions.Messaging;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Domain.Collaboration;
using FluentValidation;
using Mediator;

namespace Crm.Application.Collaboration.PostTicketMessage;

/// <summary>
/// Transactional (F3): access is decided from the row-locked ticket (and membership) and the message is written before commit.
/// Any access level may post, on a ticket of any status. Never loads or changes the Ticket (the lock is a read lock).
/// </summary>
public sealed record PostTicketMessageCommand(StaffActor Actor, Guid TicketId, string Body)
    : ICommand<TicketMessageDetails>, ITransactionalCommand;

public sealed class PostTicketMessageCommandHandler(
    ICollaborationRepository collaboration,
    ITicketAccessLock ticketLock,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<PostTicketMessageCommand, TicketMessageDetails>
{
    public async ValueTask<TicketMessageDetails> Handle(PostTicketMessageCommand command, CancellationToken cancellationToken)
    {
        _ = await CollaborationWriteAccess.ResolveAsync(ticketLock, command.Actor, command.TicketId, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{command.TicketId}' was not found.");

        var message = TicketInternalMessage.Post(command.TicketId, command.Actor.UserId, command.Body.Trim(), timeProvider.GetUtcNow());
        collaboration.AddMessage(message);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await collaboration.GetMessageAsync(message.Id, cancellationToken);
    }
}

public sealed class PostTicketMessageValidator : AbstractValidator<PostTicketMessageCommand>
{
    public PostTicketMessageValidator()
    {
        RuleFor(c => c.TicketId).NotEmpty();
        RuleFor(c => c.Body).NotEmpty().MaximumLength(CollaborationRules.MessageMaxLength);
    }
}
