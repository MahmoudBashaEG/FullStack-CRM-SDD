using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using FluentValidation;
using Mediator;

namespace Crm.Application.AgentTasks.UpdateAgentTask;

/// <summary>Title, description and due time only; never touches the link, owner or reminder. Requires the task's current version (If-Match).</summary>
public sealed record UpdateAgentTaskCommand(
    StaffActor Actor, Guid Id, byte[] ExpectedVersion, string Title, string? Description, DateTimeOffset? DueAtUtc)
    : ICommand<AgentTaskDetails>;

public sealed class UpdateAgentTaskCommandHandler(
    IAgentTaskRepository tasks,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<UpdateAgentTaskCommand, AgentTaskDetails>
{
    public async ValueTask<AgentTaskDetails> Handle(UpdateAgentTaskCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var task = await tasks.GetByIdAsync(command.Id, command.Actor.AgentScope, cancellationToken)
            ?? throw new NotFoundException($"Task '{command.Id}' was not found.");

        AgentTaskGuards.EnsureVersion(task, command.ExpectedVersion);
        AgentTaskGuards.EnsureNotArchived(task);

        if (command.DueAtUtc != task.DueAtUtc && task.RemindAtUtc is { } remindAt && command.DueAtUtc is { } due
            && remindAt > due && task.IsOpenForReminders)
        {
            throw AgentTaskGuards.Invalid(
                nameof(command.DueAtUtc), "The due time cannot be before the scheduled reminder. Change or cancel the reminder first.");
        }

        var description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();
        if (task.UpdateDetails(command.Title.Trim(), description, command.DueAtUtc, now))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return (await tasks.FindDetailsByIdAsync(task.Id, command.Actor, now, cancellationToken))!;
    }
}

public sealed class UpdateAgentTaskValidator : AbstractValidator<UpdateAgentTaskCommand>
{
    public UpdateAgentTaskValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.ExpectedVersion).NotEmpty();
        RuleFor(c => c.Title).NotEmpty().MaximumLength(AgentTaskRules.TitleMaxLength);
        RuleFor(c => c.Description).MaximumLength(AgentTaskRules.DescriptionMaxLength);
    }
}
