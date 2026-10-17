using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Domain.Identity;
using FluentValidation;
using FluentValidation.Results;
using Mediator;

namespace Crm.Application.Tickets.ChangeAssignee;

/// <summary>SuperAdmin only (API policy). null = unassign. Same assignee = no-op. Single save — not transactional.</summary>
public sealed record ChangeTicketAssigneeCommand(TicketActor Actor, Guid Id, Guid? AssignedAgentUserId) : ICommand<TicketDetails>;

public sealed class ChangeTicketAssigneeCommandHandler(
    ITicketRepository tickets,
    IIdentityService identity,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ChangeTicketAssigneeCommand, TicketDetails>
{
    public async ValueTask<TicketDetails> Handle(ChangeTicketAssigneeCommand command, CancellationToken cancellationToken)
    {
        if (!command.Actor.IsSuperAdmin)
        {
            throw new InvalidOperationException("Only SuperAdmin can change a ticket's assignee."); // guarded by the SuperAdmin policy
        }

        var ticket = await tickets.GetByIdAsync(command.Id, command.Actor.AgentScope, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{command.Id}' was not found.");

        // No-op first: re-submitting the current (possibly since-disabled) assignee changes nothing and is not an error.
        if (ticket.AssignedAgentUserId != command.AssignedAgentUserId)
        {
            if (command.AssignedAgentUserId is { } agentId)
            {
                var agent = await identity.FindByIdAsync(agentId, cancellationToken);
                if (agent is not { Role: UserRoles.Agent, IsActive: true })
                {
                    throw new ValidationException([new ValidationFailure(
                        nameof(command.AssignedAgentUserId), "Select an active Agent or leave the ticket unassigned.")]);
                }
            }

            ticket.ChangeAssignee(command.AssignedAgentUserId, command.Actor.UserId, timeProvider.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return (await tickets.FindDetailsByIdAsync(ticket.Id, command.Actor.AgentScope, cancellationToken))!;
    }
}
