namespace Crm.Api.Contracts.Profile;

public sealed record UpdateMyProfileRequest(string FirstName, string LastName, string? PhoneNumber);
