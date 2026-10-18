using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Mediator;

namespace Crm.Application.AgentTasks.Reminders.MarkTaskReminderRead;

/// <summary>
/// One conditional UPDATE (no IUnitOfWork): only the current recipient who still owns the task can mark a reminder read, so the former
/// owner can never touch a reminder that moved to the new owner. Already read = success. Never touches the task.
/// </summary>
public sealed record MarkTaskReminderReadCommand(Guid AgentUserId, long Id) : ICommand;

public sealed class MarkTaskReminderReadCommandHandler(ITaskReminderRepository reminders, TimeProvider timeProvider)
    : ICommandHandler<MarkTaskReminderReadCommand>
{
    public async ValueTask<Unit> Handle(MarkTaskReminderReadCommand command, CancellationToken cancellationToken)
    {
        var outcome = await reminders.MarkReadAsync(command.Id, command.AgentUserId, timeProvider.GetUtcNow(), cancellationToken);
        if (outcome == MarkReadOutcome.NotFound)
        {
            throw new NotFoundException($"Reminder '{command.Id}' was not found.");
        }

        return Unit.Value;
    }
}
