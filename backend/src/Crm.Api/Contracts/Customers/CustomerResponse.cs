using Crm.Application.Customers;

namespace Crm.Api.Contracts.Customers;

/// <summary>isRegistered = linked to a login account. The account id itself is never exposed.</summary>
public sealed record CustomerResponse(
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
    public static CustomerResponse From(CustomerDetails c) =>
        new(c.Id, c.FirstName, c.LastName, c.Email, c.PhoneNumber, c.Company, c.IsRegistered, c.CreatedAtUtc, c.UpdatedAtUtc);
}
