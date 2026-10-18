using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Validation;
using Crm.Application.Sla;
using Crm.Domain.Tickets;
using FluentValidation;
using Mediator;

namespace Crm.Application.Portal.SubmitTicket;

/// <summary>
/// CP2. Single save — not transactional: the same sequence as CreateTicketCommandHandler (Ticket.Create → ITicketCreationAutomation →
/// Add → one save), so the Created activity, SLA snapshot and optional rule-based assignment commit together exactly once (P2/P3).
/// Customer, creator, status, priority and assignee come from the server only. Category is nullable so a missing field fails validation.
/// </summary>
public sealed record SubmitPortalTicketCommand(Guid UserId, string Subject, string Description, TicketCategory? Category)
    : ICommand<PortalTicketDetails>;

public sealed class SubmitPortalTicketCommandHandler(
    ICustomerRepository customers,
    ITicketRepository tickets,
    ITicketCreationAutomation automation,
    IPortalTicketRepository portal,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<SubmitPortalTicketCommand, PortalTicketDetails>
{
    public async ValueTask<PortalTicketDetails> Handle(SubmitPortalTicketCommand command, CancellationToken cancellationToken)
    {
        var customerId = await customers.ResolveIdAsync(command.UserId, cancellationToken);

        var ticket = Ticket.Create(
            Guid.CreateVersion7(),
            customerId,
            command.Subject.Trim(),
            command.Description.Trim(),
            command.Category!.Value, // validator: NotNull
            TicketPriority.Medium,   // CP2: never chosen by the customer
            command.UserId,          // the customer's own account; no staff actor is impersonated
            assignedAgentUserId: null,
            timeProvider.GetUtcNow());

        await automation.ApplyAsync(ticket, cancellationToken); // SLA snapshot + at most one rule-based assignment (SLA-001)
        tickets.Add(ticket);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return (await portal.FindAsync(ticket.Id, customerId, cancellationToken))!; // always owned: just created for this customer
    }
}

public sealed class SubmitPortalTicketValidator : AbstractValidator<SubmitPortalTicketCommand>
{
    public SubmitPortalTicketValidator()
    {
        RuleFor(c => c.Subject).NotEmpty().MaximumLength(TicketRules.SubjectMaxLength);         // NotEmpty also rejects whitespace-only
        RuleFor(c => c.Description).NotEmpty().MaximumLength(TicketRules.DescriptionMaxLength);
        RuleFor(c => c.Category).NotNull().IsInEnum();
    }
}
