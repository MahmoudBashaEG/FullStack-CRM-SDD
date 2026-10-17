using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Application.Customers;
using Mediator;

namespace Crm.Application.Profile.GetMyCustomerProfile;

/// <summary>UserId always comes from the validated JWT subject, never from the request.</summary>
public sealed record GetMyCustomerProfileQuery(Guid UserId) : IQuery<CustomerDetails>;

public sealed class GetMyCustomerProfileQueryHandler(ICustomerRepository customers)
    : IQueryHandler<GetMyCustomerProfileQuery, CustomerDetails>
{
    public async ValueTask<CustomerDetails> Handle(GetMyCustomerProfileQuery query, CancellationToken cancellationToken) =>
        await customers.FindDetailsByUserIdAsync(query.UserId, cancellationToken)
            ?? throw new NotFoundException("Customer profile not found.");
}
