using Crm.Application.Common.Validation;
using FluentValidation;

namespace Crm.Application.Auth.RegisterCustomer;

public sealed class RegisterCustomerValidator : AbstractValidator<RegisterCustomerCommand>
{
    public RegisterCustomerValidator()
    {
        RuleFor(c => c.FirstName).PersonName();
        RuleFor(c => c.LastName).PersonName();
        RuleFor(c => c.Email).AccountEmail();
        RuleFor(c => c.Password).NewPassword();
        RuleFor(c => c.PhoneNumber).OptionalPhone();
    }
}
