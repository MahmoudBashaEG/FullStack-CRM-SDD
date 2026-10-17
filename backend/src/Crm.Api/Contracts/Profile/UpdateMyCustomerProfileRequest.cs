namespace Crm.Api.Contracts.Profile;

/// <summary>No email: the login email is not editable.</summary>
public sealed record UpdateMyCustomerProfileRequest(string FirstName, string LastName, string? PhoneNumber, string? Company);
