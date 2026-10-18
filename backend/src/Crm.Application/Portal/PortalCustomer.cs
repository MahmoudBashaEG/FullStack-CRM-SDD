using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;

namespace Crm.Application.Portal;

internal static class PortalCustomer
{
    /// <summary>
    /// P1: the caller's CRM customer id from the validated JWT subject through Customers.UserId (unique, server-set).
    /// Never by email, never from the request. Same 404 as GET /me/customer-profile when no profile is linked.
    /// </summary>
    public static async Task<Guid> ResolveIdAsync(this ICustomerRepository customers, Guid userId, CancellationToken cancellationToken) =>
        (await customers.FindDetailsByUserIdAsync(userId, cancellationToken))?.Id
            ?? throw new NotFoundException("Customer profile not found.");
}
