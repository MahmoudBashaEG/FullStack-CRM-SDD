using Crm.Application.Common.Validation;
using FluentValidation;

namespace Crm.Application.Agents.UpdateAgent;

public sealed class UpdateAgentValidator : AbstractValidator<UpdateAgentCommand>
{
    public UpdateAgentValidator()
    {
        RuleFor(c => c.FirstName).PersonName();
        RuleFor(c => c.LastName).PersonName();
        RuleFor(c => c.PhoneNumber).OptionalPhone();
    }
}
