using Crm.Application.Common.Validation;
using FluentValidation;

namespace Crm.Application.Auth.Login;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(c => c.Email).NotEmpty().MaximumLength(AccountRules.EmailMaxLength);
        RuleFor(c => c.Password).NotEmpty().MaximumLength(AccountRules.PasswordMaxLength);
    }
}
