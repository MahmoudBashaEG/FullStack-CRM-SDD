using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Domain.Tickets;
using Mediator;

namespace Crm.Application.Tickets.TransitionTicket;

/// <summary>
/// SuperAdmin: any ticket. Agent: only tickets currently assigned (agentScope ⇒ 404).
/// FromStatus is the status the caller saw; a mismatch is a stale request (409), never silently re-targeted.
/// Nullable enums only so a missing JSON field fails validation. Single save — not transactional.
/// </summary>
public sealed record TransitionTicketStatusCommand(TicketActor Actor, Guid Id, TicketStatus? FromStatus, TicketStatus? ToStatus)
    : ICommand<TicketDetails>;

public sealed class TransitionTicketStatusCommandHandler(
    ITicketRepository tickets,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<TransitionTicketStatusCommand, TicketDetails>
{
    public async ValueTask<TicketDetails> Handle(TransitionTicketStatusCommand command, CancellationToken cancellationToken)
    {
        var ticket = await tickets.GetByIdAsync(command.Id, command.Actor.AgentScope, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{command.Id}' was not found.");

        var from = command.FromStatus!.Value; // validator: NotNull
        var to = command.ToStatus!.Value;     // validator: NotNull
        if (ticket.Status != from)
        {
            throw new ConflictException($"The ticket is now {ticket.Status}, not {from}. Reload it and try again.");
        }

        if (!TicketWorkflow.IsAllowed(from, to))
        {
            throw new ConflictException($"A ticket cannot move from {from} to {to}.");
        }

        ticket.TransitionTo(to, command.Actor.UserId, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return (await tickets.FindDetailsByIdAsync(ticket.Id, command.Actor.AgentScope, cancellationToken))!;
    }
}
