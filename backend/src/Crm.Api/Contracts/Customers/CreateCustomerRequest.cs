namespace Crm.Api.Contracts.Customers;

/// <summary>No password, role or userId: contact-only records never get a login.</summary>
public sealed record CreateCustomerRequest(string FirstName, string LastName, string Email, string? PhoneNumber, string? Company);
