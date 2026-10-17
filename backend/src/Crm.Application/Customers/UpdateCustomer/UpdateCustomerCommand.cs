using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Messaging;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Mediator;

namespace Crm.Application.Customers.UpdateCustomer;

/// <summary>Registered customer: CRM row + Identity name/phone = two saves, hence transactional.</summary>
public sealed record UpdateCustomerCommand(Guid Id, string FirstName, string LastName, string Email, string? PhoneNumber, string? Company)
    : ICommand<CustomerDetails>, ITransactionalCommand;

public sealed class UpdateCustomerCommandHandler(
    ICustomerRepository customers,
    IIdentityService identity,
    TimeProvider timeProvider) : ICommandHandler<UpdateCustomerCommand, CustomerDetails>
{
    public async ValueTask<CustomerDetails> Handle(UpdateCustomerCommand command, CancellationToken cancellationToken)
    {
        var customer = await customers.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException($"Customer '{command.Id}' was not found.");

        var now = timeProvider.GetUtcNow();
        customer.ChangeContactEmail(command.Email.Trim(), now); // 422 for a changed login email, before any write
        customer.UpdateDetails(
            command.FirstName.Trim(),
            command.LastName.Trim(),
            AccountRules.NormalizeOptional(command.PhoneNumber),
            AccountRules.NormalizeOptional(command.Company),
            now);

        await identity.SyncLinkedAccountAsync(customer, cancellationToken);
        return CustomerDetails.From(customer); // EfUnitOfWork saves and commits after the handler
    }
}
