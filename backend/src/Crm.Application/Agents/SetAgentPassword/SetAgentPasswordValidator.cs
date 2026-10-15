using Crm.Application.Common.Validation;
using FluentValidation;

namespace Crm.Application.Agents.SetAgentPassword;

public sealed class SetAgentPasswordValidator : AbstractValidator<SetAgentPasswordCommand>
{
    public SetAgentPasswordValidator()
    {
        RuleFor(c => c.NewPassword).NewPassword();
    }
}
