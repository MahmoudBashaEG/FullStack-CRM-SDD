namespace Crm.Api.Contracts.Auth;

/// <summary>No role field: the server always assigns Customer.</summary>
public sealed record RegisterCustomerRequest(string FirstName, string LastName, string Email, string Password, string? PhoneNumber);
