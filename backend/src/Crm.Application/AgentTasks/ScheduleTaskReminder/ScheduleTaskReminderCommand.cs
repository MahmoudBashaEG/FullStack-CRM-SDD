using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using FluentValidation;
using Mediator;

namespace Crm.Application.AgentTasks.ScheduleTaskReminder;

/// <summary>RemindAtUtc null = cancel (always allowed). Otherwise future, at or before the due time, Pending and unarchived task only.</summary>
public sealed record ScheduleTaskReminderCommand(StaffActor Actor, Guid Id, byte[] ExpectedVersion, DateTimeOffset? RemindAtUtc)
    : ICommand<AgentTaskDetails>;

public sealed class ScheduleTaskReminderCommandHandler(
    IAgentTaskRepository tasks,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ScheduleTaskReminderCommand, AgentTaskDetails>
{
    public async ValueTask<AgentTaskDetails> Handle(ScheduleTaskReminderCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var task = await tasks.GetByIdAsync(command.Id, command.Actor.AgentScope, cancellationToken)
            ?? throw new NotFoundException($"Task '{command.Id}' was not found.");

        AgentTaskGuards.EnsureVersion(task, command.ExpectedVersion);

        if (command.RemindAtUtc is { } remindAt)
        {
            if (!task.IsOpenForReminders)
            {
                throw new ConflictException("Reopen or restore the task before scheduling a reminder.");
            }

            if (remindAt != task.RemindAtUtc)
            {
                AgentTaskGuards.EnsureValidReminder(remindAt, task.DueAtUtc, now);
            }
        }

        if (task.ScheduleReminder(command.RemindAtUtc, now))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return (await tasks.FindDetailsByIdAsync(task.Id, command.Actor, now, cancellationToken))!;
    }
}

public sealed class ScheduleTaskReminderValidator : AbstractValidator<ScheduleTaskReminderCommand>
{
    public ScheduleTaskReminderValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.ExpectedVersion).NotEmpty();
    }
}
