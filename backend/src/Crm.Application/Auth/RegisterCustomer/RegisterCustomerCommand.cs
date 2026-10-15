using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Messaging;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Domain.Identity;
using Mediator;

namespace Crm.Application.Auth.RegisterCustomer;

/// <summary>Public signup. Role is always Customer — there is no role input.</summary>
public sealed record RegisterCustomerCommand(string FirstName, string LastName, string Email, string Password, string? PhoneNumber)
    : ICommand<UserAccount>, ITransactionalCommand; // create user + add role = two saves

public sealed class RegisterCustomerCommandHandler(IIdentityService identity)
    : ICommandHandler<RegisterCustomerCommand, UserAccount>
{
    public async ValueTask<UserAccount> Handle(RegisterCustomerCommand command, CancellationToken cancellationToken)
    {
        var email = command.Email.Trim();
        if (await identity.EmailExistsAsync(email, cancellationToken))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        return await identity.CreateUserAsync(
            new NewUserAccount(email, command.FirstName.Trim(), command.LastName.Trim(), AccountRules.NormalizeOptional(command.PhoneNumber)),
            command.Password,
            UserRoles.Customer,
            cancellationToken);
    }
}
