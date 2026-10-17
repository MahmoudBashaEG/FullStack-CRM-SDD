using FluentValidation;

namespace Crm.Application.Tickets.ClearTicketEscalation;

public sealed class ClearTicketEscalationValidator : AbstractValidator<ClearTicketEscalationCommand>
{
    public ClearTicketEscalationValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}
