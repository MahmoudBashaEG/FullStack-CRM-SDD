using Crm.Domain.Identity;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>
/// Revoke* methods execute immediately (set-based UPDATE) inside the current transaction, if any.
/// Add only stages; the caller saves through IUnitOfWork.
/// </summary>
public interface IRefreshSessionRepository
{
    Task<RefreshSession?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    void Add(RefreshSession session);

    /// <summary>Revokes one session only if it is still unrevoked. False = someone else revoked/rotated it first.</summary>
    Task<bool> TryRevokeAsync(Guid sessionId, RefreshSessionRevocationReason reason, Guid? replacedBySessionId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>True when the family still has an unrevoked, unexpired session (the login is alive).</summary>
    Task<bool> AnyActiveInFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<int> RevokeFamilyAsync(Guid familyId, RefreshSessionRevocationReason reason, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<int> RevokeAllForUserAsync(Guid userId, RefreshSessionRevocationReason reason, DateTimeOffset now, CancellationToken cancellationToken = default);
}
