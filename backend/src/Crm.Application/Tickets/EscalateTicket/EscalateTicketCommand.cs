using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Domain.Tickets;
using Mediator;

namespace Crm.Application.Tickets.EscalateTicket;

/// <summary>SuperAdmin (any ticket) or the currently assigned Agent. Open/InProgress only; reason required. Single save — not transactional.</summary>
public sealed record EscalateTicketCommand(TicketActor Actor, Guid Id, string Reason) : ICommand<TicketDetails>;

public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<EscalateTicketCommand, TicketDetails>
{
    public async ValueTask<TicketDetails> Handle(EscalateTicketCommand command, CancellationToken cancellationToken)
    {
        var ticket = await tickets.GetByIdAsync(command.Id, command.Actor.AgentScope, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{command.Id}' was not found.");

        if (ticket.IsEscalated)
        {
            throw new ConflictException("The ticket is already escalated.");
        }

        if (!TicketWorkflow.CanEscalate(ticket.Status))
        {
            throw new ConflictException($"A {ticket.Status} ticket cannot be escalated.");
        }

        ticket.Escalate(command.Reason.Trim(), command.Actor.UserId, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return (await tickets.FindDetailsByIdAsync(ticket.Id, command.Actor.AgentScope, cancellationToken))!;
    }
}
