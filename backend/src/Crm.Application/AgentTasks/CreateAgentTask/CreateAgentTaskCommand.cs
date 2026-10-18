using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Messaging;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Domain.AgentTasks;
using Mediator;

namespace Crm.Application.AgentTasks.CreateAgentTask;

/// <summary>
/// Transactional (F3): an optional ticket link is authorized from the row-locked ticket, which stays locked until commit.
/// Owner and creator come from Actor, never trusted from the client:
/// Agent ⇒ always themselves (another id is rejected); SuperAdmin ⇒ a required active Agent.
/// Optional reminder must be future and ≤ due. One explicit save.
/// </summary>
public sealed record CreateAgentTaskCommand(
    StaffActor Actor,
    string Title,
    string? Description,
    Guid? AssignedAgentUserId,
    Guid? TicketId,
    DateTimeOffset? DueAtUtc,
    DateTimeOffset? RemindAtUtc) : ICommand<AgentTaskDetails>, ITransactionalCommand;

public sealed class CreateAgentTaskCommandHandler(
    IAgentTaskRepository tasks,
    ITicketAccessLock ticketLock,
    IIdentityService identity,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CreateAgentTaskCommand, AgentTaskDetails>
{
    public async ValueTask<AgentTaskDetails> Handle(CreateAgentTaskCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var ownerId = await ResolveOwnerAsync(command, cancellationToken);

        if (command.RemindAtUtc is { } remindAt)
        {
            AgentTaskGuards.EnsureValidReminder(remindAt, command.DueAtUtc, now);
        }

        // Last check before the write: the lock keeps the ticket's assignment fixed until this transaction commits.
        if (command.TicketId is { } ticketId)
        {
            await AgentTaskGuards.EnsureTicketAssignedToOwnerAsync(ticketLock, ticketId, ownerId, cancellationToken);
        }

        var task = AgentTask.Create(
            Guid.CreateVersion7(),
            command.Title.Trim(),
            string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
            ownerId,
            command.Actor.UserId,
            command.TicketId,
            command.DueAtUtc,
            command.RemindAtUtc,
            now);

        tasks.Add(task);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Always visible: an Agent owns it; SuperAdmin is unrestricted. Carries the store-generated Version.
        return (await tasks.FindDetailsByIdAsync(task.Id, command.Actor, now, cancellationToken))!;
    }

    private async Task<Guid> ResolveOwnerAsync(CreateAgentTaskCommand command, CancellationToken cancellationToken)
    {
        if (!command.Actor.IsSuperAdmin)
        {
            if (command.AssignedAgentUserId is { } requested && requested != command.Actor.UserId)
            {
                throw AgentTaskGuards.Invalid(nameof(command.AssignedAgentUserId), "Agents cannot choose a task owner; tasks you create are yours.");
            }

            return command.Actor.UserId;
        }

        if (command.AssignedAgentUserId is not { } agentId)
        {
            throw AgentTaskGuards.Invalid(nameof(command.AssignedAgentUserId), "Select an active Agent.");
        }

        await AgentTaskGuards.EnsureActiveAgentAsync(identity, agentId, nameof(command.AssignedAgentUserId), cancellationToken);
        return agentId;
    }
}
