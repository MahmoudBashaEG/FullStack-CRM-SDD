using Crm.Application.Common.Validation;
using FluentValidation;

namespace Crm.Application.Customers.CreateCustomer;

public sealed class CreateCustomerValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerValidator()
    {
        RuleFor(c => c.FirstName).PersonName();
        RuleFor(c => c.LastName).PersonName();
        RuleFor(c => c.Email).AccountEmail();
        RuleFor(c => c.PhoneNumber).OptionalPhone();
        RuleFor(c => c.Company).OptionalCompany();
    }
}
