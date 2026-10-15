using Crm.Application.Common.Validation;
using FluentValidation;

namespace Crm.Application.Profile.UpdateMyProfile;

public sealed class UpdateMyProfileValidator : AbstractValidator<UpdateMyProfileCommand>
{
    public UpdateMyProfileValidator()
    {
        RuleFor(c => c.FirstName).PersonName();
        RuleFor(c => c.LastName).PersonName();
        RuleFor(c => c.PhoneNumber).OptionalPhone();
    }
}
