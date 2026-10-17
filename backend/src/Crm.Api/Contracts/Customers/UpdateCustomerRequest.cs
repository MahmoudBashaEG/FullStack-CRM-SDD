namespace Crm.Api.Contracts.Customers;

/// <summary>For registered customers Email must equal the current login email (else 422).</summary>
public sealed record UpdateCustomerRequest(string FirstName, string LastName, string Email, string? PhoneNumber, string? Company);
