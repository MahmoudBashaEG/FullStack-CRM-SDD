using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Messaging;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Application.Customers;
using Mediator;

namespace Crm.Application.Profile.UpdateMyCustomerProfile;

/// <summary>No email field: the login email is not editable (AUTH-001). CRM row + Identity = transactional.</summary>
public sealed record UpdateMyCustomerProfileCommand(Guid UserId, string FirstName, string LastName, string? PhoneNumber, string? Company)
    : ICommand<CustomerDetails>, ITransactionalCommand;

public sealed class UpdateMyCustomerProfileCommandHandler(
    ICustomerRepository customers,
    IIdentityService identity,
    TimeProvider timeProvider) : ICommandHandler<UpdateMyCustomerProfileCommand, CustomerDetails>
{
    public async ValueTask<CustomerDetails> Handle(UpdateMyCustomerProfileCommand command, CancellationToken cancellationToken)
    {
        var customer = await customers.GetByUserIdAsync(command.UserId, cancellationToken)
            ?? throw new NotFoundException("Customer profile not found.");

        customer.UpdateDetails(
            command.FirstName.Trim(),
            command.LastName.Trim(),
            AccountRules.NormalizeOptional(command.PhoneNumber),
            AccountRules.NormalizeOptional(command.Company),
            timeProvider.GetUtcNow());

        await identity.SyncLinkedAccountAsync(customer, cancellationToken);
        return CustomerDetails.From(customer);
    }
}
