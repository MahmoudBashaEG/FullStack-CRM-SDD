namespace Crm.Application.Abstractions.Identity;

/// <summary>Read model of an account. Never returned to clients as-is (SecurityStamp is internal).</summary>
public sealed record UserAccount(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string Role,
    bool IsActive,
    string SecurityStamp,
    DateTimeOffset CreatedAtUtc);

public sealed record NewUserAccount(string Email, string FirstName, string LastName, string? PhoneNumber);

public sealed record UserProfileUpdate(string FirstName, string LastName, string? PhoneNumber);
