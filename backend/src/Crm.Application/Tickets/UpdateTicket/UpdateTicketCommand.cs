using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Domain.Tickets;
using Mediator;

namespace Crm.Application.Tickets.UpdateTicket;

/// <summary>Only Subject/Description/Category/Priority, each actual change recorded as a FieldChanged event. Single save — not transactional; no-op = no save.</summary>
public sealed record UpdateTicketCommand(
    TicketActor Actor,
    Guid Id,
    string Subject,
    string Description,
    TicketCategory? Category,
    TicketPriority? Priority) : ICommand<TicketDetails>;

public sealed class UpdateTicketCommandHandler(
    ITicketRepository tickets,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<UpdateTicketCommand, TicketDetails>
{
    public async ValueTask<TicketDetails> Handle(UpdateTicketCommand command, CancellationToken cancellationToken)
    {
        // An Agent asking for a ticket not assigned to them gets the same 404 as for a missing id.
        var ticket = await tickets.GetByIdAsync(command.Id, command.Actor.AgentScope, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{command.Id}' was not found.");

        var changed = ticket.UpdateDetails(
            command.Subject.Trim(),
            command.Description.Trim(),
            command.Category!.Value, // validator: NotNull
            command.Priority!.Value, // validator: NotNull
            command.Actor.UserId,
            timeProvider.GetUtcNow());

        if (changed)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Same scope as the load above (the assignment cannot change here), so the ticket is still visible.
        return (await tickets.FindDetailsByIdAsync(ticket.Id, command.Actor.AgentScope, cancellationToken))!;
    }
}
