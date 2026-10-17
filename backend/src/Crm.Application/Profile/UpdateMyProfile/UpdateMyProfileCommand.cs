using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Messaging;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Validation;
using Mediator;

namespace Crm.Application.Profile.UpdateMyProfile;

/// <summary>
/// Identity update + (for Customers with a CRM profile) the mirrored CRM row = two saves, hence transactional.
/// Contract of PUT /api/v1/me is unchanged; Company is edited via /api/v1/me/customer-profile.
/// </summary>
public sealed record UpdateMyProfileCommand(Guid UserId, string FirstName, string LastName, string? PhoneNumber)
    : ICommand<UserAccount>, ITransactionalCommand;

public sealed class UpdateMyProfileCommandHandler(
    IIdentityService identity,
    ICustomerRepository customers,
    TimeProvider timeProvider) : ICommandHandler<UpdateMyProfileCommand, UserAccount>
{
    public async ValueTask<UserAccount> Handle(UpdateMyProfileCommand command, CancellationToken cancellationToken)
    {
        var user = await identity.UpdateProfileAsync(
            command.UserId,
            new UserProfileUpdate(command.FirstName.Trim(), command.LastName.Trim(), AccountRules.NormalizeOptional(command.PhoneNumber)),
            cancellationToken);

        // Staff accounts have no CRM profile; Customers keep their CRM mirror in sync.
        var customer = await customers.GetByUserIdAsync(command.UserId, cancellationToken);
        customer?.UpdateDetails(user.FirstName, user.LastName, user.PhoneNumber, customer.Company, timeProvider.GetUtcNow());

        return user;
    }
}
