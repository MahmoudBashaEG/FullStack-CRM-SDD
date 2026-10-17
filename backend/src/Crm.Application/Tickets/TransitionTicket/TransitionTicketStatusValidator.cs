using FluentValidation;

namespace Crm.Application.Tickets.TransitionTicket;

public sealed class TransitionTicketStatusValidator : AbstractValidator<TransitionTicketStatusCommand>
{
    public TransitionTicketStatusValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.FromStatus).NotNull().IsInEnum();
        RuleFor(c => c.ToStatus).NotNull().IsInEnum();
    }
}
