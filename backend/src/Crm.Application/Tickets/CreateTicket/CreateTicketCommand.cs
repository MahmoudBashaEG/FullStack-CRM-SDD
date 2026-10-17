using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Persistence;
using Crm.Domain.Identity;
using Crm.Domain.Tickets;
using FluentValidation;
using FluentValidation.Results;
using Mediator;

namespace Crm.Application.Tickets.CreateTicket;

/// <summary>
/// Single save — not transactional. Creator and (for Agents) assignee come from Actor, never from the client.
/// Category is nullable only so a missing JSON field fails validation instead of defaulting to General.
/// </summary>
public sealed record CreateTicketCommand(
    TicketActor Actor,
    Guid CustomerId,
    string Subject,
    string Description,
    TicketCategory? Category,
    TicketPriority? Priority,
    Guid? AssignedAgentUserId) : ICommand<TicketDetails>;

public sealed class CreateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    IIdentityService identity,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CreateTicketCommand, TicketDetails>
{
    public async ValueTask<TicketDetails> Handle(CreateTicketCommand command, CancellationToken cancellationToken)
    {
        // Registered and contact-only customers are both valid owners; no Identity account is required.
        if (await customers.FindDetailsByIdAsync(command.CustomerId, cancellationToken) is null)
        {
            throw Invalid(nameof(command.CustomerId), "The selected customer does not exist.");
        }

        var assignedAgentUserId = await ResolveInitialAssigneeAsync(command, cancellationToken);

        var ticket = Ticket.Create(
            Guid.CreateVersion7(),
            command.CustomerId,
            command.Subject.Trim(),
            command.Description.Trim(),
            command.Category!.Value, // validator: NotNull
            command.Priority ?? TicketPriority.Medium,
            command.Actor.UserId,
            assignedAgentUserId,
            timeProvider.GetUtcNow());

        tickets.Add(ticket);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Read back under the caller's own scope, like every other ticket read. Always visible here:
        // an Agent's new ticket is assigned to that Agent; SuperAdmin's scope is unrestricted.
        return (await tickets.FindDetailsByIdAsync(ticket.Id, command.Actor.AgentScope, cancellationToken))!;
    }

    /// <summary>
    /// Agent: always themselves (self-assignment only, never routing); another submitted id is rejected, never honoured.
    /// SuperAdmin: null = unassigned, otherwise an existing, active Agent account (not SuperAdmin, not Customer).
    /// </summary>
    private async Task<Guid?> ResolveInitialAssigneeAsync(CreateTicketCommand command, CancellationToken cancellationToken)
    {
        if (!command.Actor.IsSuperAdmin)
        {
            if (command.AssignedAgentUserId is { } requested && requested != command.Actor.UserId)
            {
                throw Invalid(nameof(command.AssignedAgentUserId), "Agents cannot choose an assignee; tickets you create are assigned to you.");
            }

            return command.Actor.UserId;
        }

        if (command.AssignedAgentUserId is not { } agentId)
        {
            return null;
        }

        var agent = await identity.FindByIdAsync(agentId, cancellationToken);
        if (agent is not { Role: UserRoles.Agent, IsActive: true })
        {
            throw Invalid(nameof(command.AssignedAgentUserId), "Select an active Agent or leave the ticket unassigned.");
        }

        return agentId;
    }

    // 400 ValidationProblemDetails keyed by the camelCased field (GlobalExceptionHandler), like IdentityService.ThrowIfFailed.
    private static ValidationException Invalid(string property, string message) =>
        new([new ValidationFailure(property, message)]);
}
