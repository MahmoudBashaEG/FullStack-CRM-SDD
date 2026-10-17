using Crm.Application.Common.Validation;
using FluentValidation;

namespace Crm.Application.Tickets.UpdateTicket;

public sealed class UpdateTicketValidator : AbstractValidator<UpdateTicketCommand>
{
    public UpdateTicketValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Subject).NotEmpty().MaximumLength(TicketRules.SubjectMaxLength);
        RuleFor(c => c.Description).NotEmpty().MaximumLength(TicketRules.DescriptionMaxLength);
        RuleFor(c => c.Category).NotNull().IsInEnum();
        RuleFor(c => c.Priority).NotNull().IsInEnum();
    }
}
