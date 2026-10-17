using Crm.Application.Common.Validation;
using FluentValidation;

namespace Crm.Application.Tickets.CreateTicket;

public sealed class CreateTicketValidator : AbstractValidator<CreateTicketCommand>
{
    public CreateTicketValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.Subject).NotEmpty().MaximumLength(TicketRules.SubjectMaxLength);         // NotEmpty also rejects whitespace-only
        RuleFor(c => c.Description).NotEmpty().MaximumLength(TicketRules.DescriptionMaxLength);
        RuleFor(c => c.Category).NotNull().IsInEnum();
        RuleFor(c => c.Priority).IsInEnum();                                                    // null = Medium
        RuleFor(c => c.AssignedAgentUserId).NotEqual(Guid.Empty);
    }
}
