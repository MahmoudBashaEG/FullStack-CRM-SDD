using Crm.Application.Common.Paging;

namespace Crm.Application.Abstractions.Identity;

/// <summary>Role must be one of UserRoles.All or null; Search is trimmed or null.</summary>
public sealed record AccountListFilter(string? Search, string? Role, bool? IsActive);

public sealed record LockedAccountRow(Guid UserId, bool IsActive);

/// <summary>ADMIN-001 account administration reads and write-time locks. IIdentityService stays the write boundary.</summary>
public interface IAccountAdministration
{
    /// <summary>All roles. Order: LastName, FirstName, Email, Id. Filters apply before paging.</summary>
    Task<PagedResult<UserAccount>> ListAsync(AccountListFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Call only inside an ITransactionalCommand. UPDLOCK + HOLDLOCK on the AspNetUsers row until commit; reads the latest committed
    /// state. null = no such account.
    /// </summary>
    Task<LockedAccountRow?> LockAccountAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Call only inside an ITransactionalCommand. Locks every SuperAdmin account row until commit and returns them.</summary>
    Task<IReadOnlyList<LockedAccountRow>> LockSuperAdminsAsync(CancellationToken cancellationToken = default);
}
