using Crm.Application.Common.Validation;
using FluentValidation;

namespace Crm.Application.Agents.CreateAgent;

public sealed class CreateAgentValidator : AbstractValidator<CreateAgentCommand>
{
    public CreateAgentValidator()
    {
        RuleFor(c => c.FirstName).PersonName();
        RuleFor(c => c.LastName).PersonName();
        RuleFor(c => c.Email).AccountEmail();
        RuleFor(c => c.Password).NewPassword();
        RuleFor(c => c.PhoneNumber).OptionalPhone();
    }
}
