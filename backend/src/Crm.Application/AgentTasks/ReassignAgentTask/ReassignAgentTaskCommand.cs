using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Messaging;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using FluentValidation;
using Mediator;

namespace Crm.Application.AgentTasks.ReassignAgentTask;

/// <summary>
/// SuperAdmin only (API policy). Transactional (F2 + F3): the task update and the transfer of its unread reminders commit together;
/// a kept ticket link is authorized against the new owner from the row-locked ticket. Replay-safe (R1): primary-constructor
/// dependencies only; every attempt re-loads everything.
/// </summary>
public sealed record ReassignAgentTaskCommand(
    StaffActor Actor, Guid Id, byte[] ExpectedVersion, Guid? AssignedAgentUserId, bool RemoveTicketLink)
    : ICommand<AgentTaskDetails>, ITransactionalCommand;

public sealed class ReassignAgentTaskCommandHandler(
    IAgentTaskRepository tasks,
    ITaskReminderRepository reminders,
    ITicketAccessLock ticketLock,
    IIdentityService identity,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ReassignAgentTaskCommand, AgentTaskDetails>
{
    public async ValueTask<AgentTaskDetails> Handle(ReassignAgentTaskCommand command, CancellationToken cancellationToken)
    {
        if (!command.Actor.IsSuperAdmin)
        {
            throw new InvalidOperationException("Only SuperAdmin can reassign a task."); // guarded by the SuperAdmin policy
        }

        var now = timeProvider.GetUtcNow();
        var task = await tasks.GetByIdAsync(command.Id, null, cancellationToken)
            ?? throw new NotFoundException($"Task '{command.Id}' was not found.");

        AgentTaskGuards.EnsureVersion(task, command.ExpectedVersion);
        AgentTaskGuards.EnsureNotArchived(task);

        var newOwner = command.AssignedAgentUserId!.Value; // validator: NotNull
        var ownerChanged = task.AssignedAgentUserId != newOwner;

        // No-op first: same owner and nothing to unlink.
        if (!ownerChanged && !(command.RemoveTicketLink && task.TicketId is not null))
        {
            return (await tasks.FindDetailsByIdAsync(task.Id, command.Actor, now, cancellationToken))!;
        }

        if (ownerChanged)
        {
            await AgentTaskGuards.EnsureActiveAgentAsync(identity, newOwner, nameof(command.AssignedAgentUserId), cancellationToken);
        }

        if (task.TicketId is { } ticketId && !command.RemoveTicketLink && ownerChanged)
        {
            var ticket = await ticketLock.LockTicketAsync(ticketId, cancellationToken);
            if (ticket?.AssignedAgentUserId != newOwner)
            {
                throw new ConflictException("The linked ticket is not assigned to the new owner. Remove the link to reassign the task.");
            }
        }

        // The rowversion-checked UPDATE takes the task row's X lock, so a concurrent IssueDueAsync waits and then sees the new owner.
        task.Reassign(newOwner, command.RemoveTicketLink, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (ownerChanged)
        {
            await reminders.TransferUnreadAsync(task.Id, newOwner, now, cancellationToken);
        }

        return (await tasks.FindDetailsByIdAsync(task.Id, command.Actor, now, cancellationToken))!;
    }
}

public sealed class ReassignAgentTaskValidator : AbstractValidator<ReassignAgentTaskCommand>
{
    public ReassignAgentTaskValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.ExpectedVersion).NotEmpty();
        RuleFor(c => c.AssignedAgentUserId).NotNull().NotEqual(Guid.Empty);
    }
}
