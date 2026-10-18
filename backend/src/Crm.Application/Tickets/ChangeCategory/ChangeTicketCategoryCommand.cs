using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Domain.Tickets;
using Mediator;

namespace Crm.Application.Tickets.ChangeCategory;

/// <summary>
/// AI-001 D1: category-only basic edit (e.g. applying an AI suggestion). Same scope and FieldChanged history as PUT /tickets/{id},
/// but subject/description/priority are taken from the loaded row, so a stale page cannot overwrite them.
/// ExpectedCategory = the category the caller last saw; a different current value ⇒ 409. Single save — not transactional.
/// Never touches SLA, assignment or status (Ticket.UpdateDetails does not).
/// </summary>
public sealed record ChangeTicketCategoryCommand(TicketActor Actor, Guid Id, TicketCategory? ExpectedCategory, TicketCategory? Category)
    : ICommand<TicketDetails>;

public sealed class ChangeTicketCategoryCommandHandler(ITicketRepository tickets, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    : ICommandHandler<ChangeTicketCategoryCommand, TicketDetails>
{
    public async ValueTask<TicketDetails> Handle(ChangeTicketCategoryCommand command, CancellationToken cancellationToken)
    {
        var ticket = await tickets.GetByIdAsync(command.Id, command.Actor.AgentScope, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{command.Id}' was not found.");

        if (ticket.Category != command.ExpectedCategory!.Value) // validator: NotNull
        {
            throw new ConflictException(
                $"The ticket category is now {ticket.Category}. Review the current category before applying a suggestion.");
        }

        var changed = ticket.UpdateDetails(
            ticket.Subject, ticket.Description, command.Category!.Value, ticket.Priority, command.Actor.UserId, timeProvider.GetUtcNow());
        if (changed)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken); // Tickets.Version rowversion ⇒ 409 on an overlapping write
        }

        return (await tickets.FindDetailsByIdAsync(ticket.Id, command.Actor.AgentScope, cancellationToken))!;
    }
}
