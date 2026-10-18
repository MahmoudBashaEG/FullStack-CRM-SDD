using Crm.Application.Abstractions.Messaging;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using FluentValidation;
using Mediator;

namespace Crm.Application.Collaboration.RemoveCollaborator;

/// <summary>Transactional (F3). Records who removed the Agent and when; messages are never touched.</summary>
public sealed record RemoveCollaboratorCommand(StaffActor Actor, Guid TicketId, Guid AgentUserId) : ICommand, ITransactionalCommand;

public sealed class RemoveCollaboratorCommandHandler(
    ICollaborationRepository collaboration,
    ITicketAccessLock ticketLock,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<RemoveCollaboratorCommand>
{
    public async ValueTask<Unit> Handle(RemoveCollaboratorCommand command, CancellationToken cancellationToken)
    {
        var access = await CollaborationWriteAccess.ResolveAsync(ticketLock, command.Actor, command.TicketId, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{command.TicketId}' was not found.");

        if (access.Access is not (CollaborationAccess.SuperAdmin or CollaborationAccess.PrimaryAgent))
        {
            throw new ForbiddenException("Only SuperAdmin or the ticket's assigned Agent can manage collaborators.");
        }

        if (!await ticketLock.LockActiveMembershipAsync(command.TicketId, command.AgentUserId, cancellationToken))
        {
            throw new NotFoundException("Collaborator not found.");
        }

        var membership = await collaboration.GetActiveMembershipAsync(command.TicketId, command.AgentUserId, cancellationToken)
            ?? throw new NotFoundException("Collaborator not found.");
        membership.Remove(command.Actor.UserId, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class RemoveCollaboratorValidator : AbstractValidator<RemoveCollaboratorCommand>
{
    public RemoveCollaboratorValidator()
    {
        RuleFor(c => c.TicketId).NotEmpty();
        RuleFor(c => c.AgentUserId).NotEmpty();
    }
}
