using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using FluentValidation;
using Mediator;

namespace Crm.Application.AgentTasks.SetTaskArchived;

/// <summary>Archive cancels a pending reminder; restore does not recreate it. No hard delete. Repeating the current state is a no-op.</summary>
public sealed record SetTaskArchivedCommand(StaffActor Actor, Guid Id, byte[] ExpectedVersion, bool Archived)
    : ICommand<AgentTaskDetails>;

public sealed class SetTaskArchivedCommandHandler(
    IAgentTaskRepository tasks,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<SetTaskArchivedCommand, AgentTaskDetails>
{
    public async ValueTask<AgentTaskDetails> Handle(SetTaskArchivedCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var task = await tasks.GetByIdAsync(command.Id, command.Actor.AgentScope, cancellationToken)
            ?? throw new NotFoundException($"Task '{command.Id}' was not found.");

        AgentTaskGuards.EnsureVersion(task, command.ExpectedVersion);

        var changed = command.Archived ? task.Archive(now) : task.Restore(now);
        if (changed)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return (await tasks.FindDetailsByIdAsync(task.Id, command.Actor, now, cancellationToken))!;
    }
}

public sealed class SetTaskArchivedValidator : AbstractValidator<SetTaskArchivedCommand>
{
    public SetTaskArchivedValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.ExpectedVersion).NotEmpty();
    }
}
