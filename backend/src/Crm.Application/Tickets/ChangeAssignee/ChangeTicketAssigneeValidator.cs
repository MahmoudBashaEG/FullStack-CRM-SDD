using FluentValidation;

namespace Crm.Application.Tickets.ChangeAssignee;

public sealed class ChangeTicketAssigneeValidator : AbstractValidator<ChangeTicketAssigneeCommand>
{
    public ChangeTicketAssigneeValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.AssignedAgentUserId).NotEqual(Guid.Empty);
    }
}
