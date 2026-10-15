using Crm.Application.Abstractions.Identity;
using Crm.Application.Common.Validation;
using Mediator;

namespace Crm.Application.Profile.UpdateMyProfile;

/// <summary>Single save, so deliberately not transactional.</summary>
public sealed record UpdateMyProfileCommand(Guid UserId, string FirstName, string LastName, string? PhoneNumber)
    : ICommand<UserAccount>;

public sealed class UpdateMyProfileCommandHandler(IIdentityService identity)
    : ICommandHandler<UpdateMyProfileCommand, UserAccount>
{
    public async ValueTask<UserAccount> Handle(UpdateMyProfileCommand command, CancellationToken cancellationToken) =>
        await identity.UpdateProfileAsync(
            command.UserId,
            new UserProfileUpdate(command.FirstName.Trim(), command.LastName.Trim(), AccountRules.NormalizeOptional(command.PhoneNumber)),
            cancellationToken);
}
