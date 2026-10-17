using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Messaging;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Domain.Customers;
using Crm.Domain.Identity;
using Mediator;

namespace Crm.Application.Auth.RegisterCustomer;

/// <summary>Public signup. Role is always Customer — there is no role input.</summary>
public sealed record RegisterCustomerCommand(string FirstName, string LastName, string Email, string Password, string? PhoneNumber)
    : ICommand<UserAccount>, ITransactionalCommand; // create user + add role + CRM profile, all in one transaction

public sealed class RegisterCustomerCommandHandler(IIdentityService identity, ICustomerRepository customers)
    : ICommandHandler<RegisterCustomerCommand, UserAccount>
{
    public async ValueTask<UserAccount> Handle(RegisterCustomerCommand command, CancellationToken cancellationToken)
    {
        var email = command.Email.Trim();
        if (await identity.EmailExistsAsync(email, cancellationToken))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var user = await identity.CreateUserAsync(
            new NewUserAccount(email, command.FirstName.Trim(), command.LastName.Trim(), AccountRules.NormalizeOptional(command.PhoneNumber)),
            command.Password,
            UserRoles.Customer,
            cancellationToken);

        // Linked CRM profile; saved by EfUnitOfWork in the same transaction (no account without a profile).
        // Never linked to an existing contact-only record, even with the same email (docs/customer-management.md §6).
        customers.Add(Customer.CreateForAccount(
            Guid.CreateVersion7(), user.Id, user.FirstName, user.LastName, user.Email, user.PhoneNumber, user.CreatedAtUtc));

        return user;
    }
}
