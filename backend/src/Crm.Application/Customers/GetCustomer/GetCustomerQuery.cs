using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Mediator;

namespace Crm.Application.Customers.GetCustomer;

public sealed record GetCustomerQuery(Guid Id) : IQuery<CustomerDetails>;

public sealed class GetCustomerQueryHandler(ICustomerRepository customers) : IQueryHandler<GetCustomerQuery, CustomerDetails>
{
    public async ValueTask<CustomerDetails> Handle(GetCustomerQuery query, CancellationToken cancellationToken) =>
        await customers.FindDetailsByIdAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException($"Customer '{query.Id}' was not found.");
}
