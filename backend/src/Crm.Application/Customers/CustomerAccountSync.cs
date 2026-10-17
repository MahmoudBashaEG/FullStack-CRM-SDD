using Crm.Application.Abstractions.Identity;
using Crm.Domain.Customers;

namespace Crm.Application.Customers;

internal static class CustomerAccountSync
{
    /// <summary>
    /// Identity is authoritative for a linked customer's name/phone: push the (already updated)
    /// CRM values to the account. Call only inside an ITransactionalCommand so both rows commit together.
    /// No-op for contact-only customers.
    /// </summary>
    public static async Task SyncLinkedAccountAsync(this IIdentityService identity, Customer customer, CancellationToken cancellationToken)
    {
        if (customer.UserId is { } userId)
        {
            await identity.UpdateProfileAsync(
                userId, new UserProfileUpdate(customer.FirstName, customer.LastName, customer.PhoneNumber), cancellationToken);
        }
    }
}
