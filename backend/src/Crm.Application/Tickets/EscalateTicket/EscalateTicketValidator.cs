using Crm.Application.Common.Validation;
using FluentValidation;

namespace Crm.Application.Tickets.EscalateTicket;

public sealed class EscalateTicketValidator : AbstractValidator<EscalateTicketCommand>
{
    public EscalateTicketValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(TicketRules.EscalationReasonMaxLength); // NotEmpty also rejects whitespace-only
    }
}
