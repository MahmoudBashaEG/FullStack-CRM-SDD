using Crm.Application.Abstractions.Persistence;
using Mediator;

namespace Crm.Application.AgentTasks.Reminders.CheckTaskReminders;

/// <summary>
/// Issues the caller's due, not-yet-issued reminders and returns the unread count. Deliberately NOT transactional and without
/// IUnitOfWork: issuance is one self-contained, idempotent SQL statement (see TaskReminderRepository.IssueDueAsync).
/// </summary>
public sealed record CheckTaskRemindersCommand(Guid AgentUserId) : ICommand<TaskReminderCheck>;

public sealed class CheckTaskRemindersCommandHandler(ITaskReminderRepository reminders, TimeProvider timeProvider)
    : ICommandHandler<CheckTaskRemindersCommand, TaskReminderCheck>
{
    public async ValueTask<TaskReminderCheck> Handle(CheckTaskRemindersCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var issued = await reminders.IssueDueAsync(command.AgentUserId, now, cancellationToken);
        var unread = await reminders.CountUnreadAsync(command.AgentUserId, cancellationToken);
        return new TaskReminderCheck(unread, issued, now);
    }
}
