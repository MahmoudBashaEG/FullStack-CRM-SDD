using Crm.Application.Common.Validation;
using FluentValidation;

namespace Crm.Application.Profile.ChangeMyPassword;

public sealed class ChangeMyPasswordValidator : AbstractValidator<ChangeMyPasswordCommand>
{
    public ChangeMyPasswordValidator()
    {
        RuleFor(c => c.CurrentPassword).NotEmpty();
        RuleFor(c => c.NewPassword).NewPassword();
    }
}
