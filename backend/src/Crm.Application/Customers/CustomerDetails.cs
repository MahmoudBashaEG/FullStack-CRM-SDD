using Crm.Domain.Customers;

namespace Crm.Application.Customers;

/// <summary>Read model returned by every customer use case. Never exposes UserId.</summary>
public sealed record CustomerDetails(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string? Company,
    bool IsRegistered,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public static CustomerDetails From(Customer customer) =>
        new(customer.Id, customer.FirstName, customer.LastName, customer.Email, customer.PhoneNumber,
            customer.Company, customer.IsRegistered, customer.CreatedAtUtc, customer.UpdatedAtUtc);
}
