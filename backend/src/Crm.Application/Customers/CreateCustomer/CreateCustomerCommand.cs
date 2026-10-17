using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Validation;
using Crm.Domain.Customers;
using Mediator;

namespace Crm.Application.Customers.CreateCustomer;

/// <summary>Contact-only customer: no Identity user, password, role or login. Single save — not transactional.</summary>
public sealed record CreateCustomerCommand(string FirstName, string LastName, string Email, string? PhoneNumber, string? Company)
    : ICommand<CustomerDetails>;

public sealed class CreateCustomerCommandHandler(
    ICustomerRepository customers,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CreateCustomerCommand, CustomerDetails>
{
    public async ValueTask<CustomerDetails> Handle(CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        // Duplicate contact emails are allowed on purpose (docs/customer-management.md §6); no Identity lookup.
        var customer = Customer.CreateContact(
            Guid.CreateVersion7(),
            command.FirstName.Trim(),
            command.LastName.Trim(),
            command.Email.Trim(),
            AccountRules.NormalizeOptional(command.PhoneNumber),
            AccountRules.NormalizeOptional(command.Company),
            timeProvider.GetUtcNow());

        customers.Add(customer);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CustomerDetails.From(customer);
    }
}
