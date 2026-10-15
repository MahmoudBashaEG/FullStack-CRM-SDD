namespace Crm.Application.Abstractions.Identity;

/// <summary>
/// Application-facing identity boundary. Implemented in Infrastructure over ASP.NET Core Identity.
/// Methods that write persist immediately on the request-scoped DbContext, so they join the
/// transaction opened by TransactionBehavior for ITransactionalCommand messages.
/// </summary>
public interface IIdentityService
{
    Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Returns the account on success; null for unknown email, wrong password, locked out or disabled. Records failed attempts (lockout).</summary>
    Task<UserAccount?> CheckCredentialsAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<UserAccount> CreateUserAsync(NewUserAccount account, string password, string role, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserAccount>> ListByRoleAsync(string role, CancellationToken cancellationToken = default);
    Task<UserAccount> UpdateProfileAsync(Guid userId, UserProfileUpdate update, CancellationToken cancellationToken = default);

    /// <summary>False when the current password is wrong. Rotates the security stamp on success.</summary>
    Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>Replaces the password without the old one (SuperAdmin reset). Rotates the security stamp.</summary>
    Task SetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>Sets the active flag and rotates the security stamp.</summary>
    Task SetActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default);
}
