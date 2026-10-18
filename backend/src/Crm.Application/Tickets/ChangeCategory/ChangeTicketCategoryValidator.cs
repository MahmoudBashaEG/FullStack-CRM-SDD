using FluentValidation;

namespace Crm.Application.Tickets.ChangeCategory;

public sealed class ChangeTicketCategoryValidator : AbstractValidator<ChangeTicketCategoryCommand>
{
    public ChangeTicketCategoryValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.ExpectedCategory).NotNull().IsInEnum();
        RuleFor(c => c.Category).NotNull().IsInEnum();
    }
}
