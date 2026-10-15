namespace Crm.Api.Contracts.Profile;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
