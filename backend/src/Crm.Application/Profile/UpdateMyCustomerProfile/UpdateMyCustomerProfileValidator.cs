using Crm.Application.Common.Validation;
using FluentValidation;

namespace Crm.Application.Profile.UpdateMyCustomerProfile;

public sealed class UpdateMyCustomerProfileValidator : AbstractValidator<UpdateMyCustomerProfileCommand>
{
    public UpdateMyCustomerProfileValidator()
    {
        RuleFor(c => c.FirstName).PersonName();
        RuleFor(c => c.LastName).PersonName();
        RuleFor(c => c.PhoneNumber).OptionalPhone();
        RuleFor(c => c.Company).OptionalCompany();
    }
}
