using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Mediator;

namespace Crm.Application.Tickets.ClearTicketEscalation;

/// <summary>SuperAdmin only (API policy). Not escalated = 409. Single save — not transactional.</summary>
public sealed record ClearTicketEscalationCommand(TicketActor Actor, Guid Id) : ICommand<TicketDetails>;

public sealed class ClearTicketEscalationCommandHandler(
    ITicketRepository tickets,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ClearTicketEscalationCommand, TicketDetails>
{
    public async ValueTask<TicketDetails> Handle(ClearTicketEscalationCommand command, CancellationToken cancellationToken)
    {
        if (!command.Actor.IsSuperAdmin)
        {
            throw new InvalidOperationException("Only SuperAdmin can clear a ticket escalation."); // guarded by the SuperAdmin policy
        }

        var ticket = await tickets.GetByIdAsync(command.Id, command.Actor.AgentScope, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{command.Id}' was not found.");

        if (!ticket.IsEscalated)
        {
            throw new ConflictException("The ticket is not escalated.");
        }

        ticket.ClearEscalation(command.Actor.UserId, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return (await tickets.FindDetailsByIdAsync(ticket.Id, command.Actor.AgentScope, cancellationToken))!;
    }
}
