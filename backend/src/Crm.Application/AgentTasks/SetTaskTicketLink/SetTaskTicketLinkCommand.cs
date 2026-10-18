using Crm.Application.Abstractions.Messaging;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using FluentValidation;
using Mediator;

namespace Crm.Application.AgentTasks.SetTaskTicketLink;

/// <summary>
/// Transactional (F3). TicketId null = unlink (always allowed, also for an Inaccessible link). A non-null ticket must be assigned
/// to the task owner at commit time, read from the row-locked ticket.
/// </summary>
public sealed record SetTaskTicketLinkCommand(StaffActor Actor, Guid Id, byte[] ExpectedVersion, Guid? TicketId)
    : ICommand<AgentTaskDetails>, ITransactionalCommand;

public sealed class SetTaskTicketLinkCommandHandler(
    IAgentTaskRepository tasks,
    ITicketAccessLock ticketLock,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<SetTaskTicketLinkCommand, AgentTaskDetails>
{
    public async ValueTask<AgentTaskDetails> Handle(SetTaskTicketLinkCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var task = await tasks.GetByIdAsync(command.Id, command.Actor.AgentScope, cancellationToken)
            ?? throw new NotFoundException($"Task '{command.Id}' was not found.");

        AgentTaskGuards.EnsureVersion(task, command.ExpectedVersion);
        AgentTaskGuards.EnsureNotArchived(task);

        if (command.TicketId is { } ticketId)
        {
            await AgentTaskGuards.EnsureTicketAssignedToOwnerAsync(ticketLock, ticketId, task.AssignedAgentUserId, cancellationToken);
        }

        if (task.LinkTicket(command.TicketId, now))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return (await tasks.FindDetailsByIdAsync(task.Id, command.Actor, now, cancellationToken))!;
    }
}

public sealed class SetTaskTicketLinkValidator : AbstractValidator<SetTaskTicketLinkCommand>
{
    public SetTaskTicketLinkValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.ExpectedVersion).NotEmpty();
        RuleFor(c => c.TicketId).NotEqual(Guid.Empty);
    }
}
