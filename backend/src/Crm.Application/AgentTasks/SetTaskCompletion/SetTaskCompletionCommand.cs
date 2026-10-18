using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using FluentValidation;
using Mediator;

namespace Crm.Application.AgentTasks.SetTaskCompletion;

/// <summary>Complete cancels a pending reminder; reopen does not recreate it. Repeating the current state is a no-op.</summary>
public sealed record SetTaskCompletionCommand(StaffActor Actor, Guid Id, byte[] ExpectedVersion, bool Completed)
    : ICommand<AgentTaskDetails>;

public sealed class SetTaskCompletionCommandHandler(
    IAgentTaskRepository tasks,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<SetTaskCompletionCommand, AgentTaskDetails>
{
    public async ValueTask<AgentTaskDetails> Handle(SetTaskCompletionCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var task = await tasks.GetByIdAsync(command.Id, command.Actor.AgentScope, cancellationToken)
            ?? throw new NotFoundException($"Task '{command.Id}' was not found.");

        AgentTaskGuards.EnsureVersion(task, command.ExpectedVersion);
        AgentTaskGuards.EnsureNotArchived(task);

        var changed = command.Completed ? task.Complete(now) : task.Reopen(now);
        if (changed)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return (await tasks.FindDetailsByIdAsync(task.Id, command.Actor, now, cancellationToken))!;
    }
}

public sealed class SetTaskCompletionValidator : AbstractValidator<SetTaskCompletionCommand>
{
    public SetTaskCompletionValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.ExpectedVersion).NotEmpty();
    }
}
