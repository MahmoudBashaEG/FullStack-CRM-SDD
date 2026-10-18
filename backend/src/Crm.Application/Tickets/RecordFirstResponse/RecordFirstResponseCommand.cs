using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Domain.Tickets;
using Mediator;

namespace Crm.Application.Tickets.RecordFirstResponse;

/// <summary>
/// SLA-001 S2. SuperAdmin (any ticket) or the currently assigned Agent (others 404). Internal tracking only — sends nothing.
/// Single save — not transactional; Tickets.Version turns a concurrent reassignment into 409.
/// </summary>
public sealed record RecordFirstResponseCommand(TicketActor Actor, Guid Id) : ICommand<TicketDetails>;

public sealed class RecordFirstResponseCommandHandler(ITicketRepository tickets, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    : ICommandHandler<RecordFirstResponseCommand, TicketDetails>
{
    public async ValueTask<TicketDetails> Handle(RecordFirstResponseCommand command, CancellationToken cancellationToken)
    {
        var ticket = await tickets.GetByIdAsync(command.Id, command.Actor.AgentScope, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{command.Id}' was not found.");

        if (!ticket.IsSlaTracked)
        {
            throw new ConflictException("This ticket has no SLA tracking.");
        }

        if (!TicketWorkflow.CanEscalate(ticket.Status))
        {
            throw new ConflictException($"A first response cannot be recorded on a {ticket.Status} ticket.");
        }

        if (!ticket.RecordFirstResponse(command.Actor.UserId, timeProvider.GetUtcNow()))
        {
            throw new ConflictException("The first response was already recorded.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (await tickets.FindDetailsByIdAsync(ticket.Id, command.Actor.AgentScope, cancellationToken))!;
    }
}
