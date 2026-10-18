using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Messaging;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using Crm.Domain.Collaboration;
using Crm.Domain.Identity;
using FluentValidation;
using FluentValidation.Results;
using Mediator;

namespace Crm.Application.Collaboration.InviteCollaborator;

/// <summary>
/// Transactional (F3). SuperAdmin (any ticket) or the ticket's current primary Agent (own ticket) only. Access is decided from the
/// row-locked ticket; the membership key range is locked so parallel invites of the same Agent wait and then see the row.
/// </summary>
public sealed record InviteCollaboratorCommand(StaffActor Actor, Guid TicketId, Guid AgentUserId)
    : ICommand<TicketCollaboratorDetails>, ITransactionalCommand;

public sealed class InviteCollaboratorCommandHandler(
    ICollaborationRepository collaboration,
    ITicketAccessLock ticketLock,
    IIdentityService identity,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<InviteCollaboratorCommand, TicketCollaboratorDetails>
{
    public async ValueTask<TicketCollaboratorDetails> Handle(InviteCollaboratorCommand command, CancellationToken cancellationToken)
    {
        var access = await CollaborationWriteAccess.ResolveAsync(ticketLock, command.Actor, command.TicketId, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{command.TicketId}' was not found.");

        if (access.Access is not (CollaborationAccess.SuperAdmin or CollaborationAccess.PrimaryAgent))
        {
            throw new ForbiddenException("Only SuperAdmin or the ticket's assigned Agent can manage collaborators.");
        }

        if (command.AgentUserId == command.Actor.UserId)
        {
            throw Invalid("You cannot invite yourself.");
        }

        if (command.AgentUserId == access.Ticket.AssignedAgentUserId)
        {
            throw Invalid("The assigned Agent already has access.");
        }

        var agent = await identity.FindByIdAsync(command.AgentUserId, cancellationToken);
        if (agent is not { Role: UserRoles.Agent, IsActive: true })
        {
            throw Invalid("Select an active Agent.");
        }

        if (await ticketLock.LockActiveMembershipAsync(command.TicketId, command.AgentUserId, cancellationToken))
        {
            throw new ConflictException("This Agent is already a collaborator.");
        }

        collaboration.AddCollaborator(TicketCollaborator.Invite(
            Guid.CreateVersion7(), command.TicketId, command.AgentUserId, command.Actor.UserId, timeProvider.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return (await collaboration.ListCollaboratorsAsync(command.TicketId, cancellationToken))
            .Single(c => c.AgentUserId == command.AgentUserId);
    }

    private static ValidationException Invalid(string message) => new([new ValidationFailure("agentUserId", message)]);
}

public sealed class InviteCollaboratorValidator : AbstractValidator<InviteCollaboratorCommand>
{
    public InviteCollaboratorValidator()
    {
        RuleFor(c => c.TicketId).NotEmpty();
        RuleFor(c => c.AgentUserId).NotEmpty();
    }
}
