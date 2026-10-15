using Crm.Application.Abstractions.Persistence;
using Crm.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

internal sealed class RefreshSessionRepository(CrmDbContext dbContext) : IRefreshSessionRepository
{
    public Task<RefreshSession?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        dbContext.RefreshSessions.AsNoTracking().SingleOrDefaultAsync(s => s.TokenHash == tokenHash, cancellationToken);

    public void Add(RefreshSession session) => dbContext.RefreshSessions.Add(session);

    public async Task<bool> TryRevokeAsync(
        Guid sessionId,
        RefreshSessionRevocationReason reason,
        Guid? replacedBySessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        await dbContext.RefreshSessions
            .Where(s => s.Id == sessionId && s.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(s => s.RevokedAtUtc, (DateTimeOffset?)now)
                    .SetProperty(s => s.RevocationReason, (RefreshSessionRevocationReason?)reason)
                    .SetProperty(s => s.ReplacedBySessionId, replacedBySessionId),
                cancellationToken) == 1;

    public Task<bool> AnyActiveInFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        dbContext.RefreshSessions.AnyAsync(
            s => s.FamilyId == familyId && s.RevokedAtUtc == null && s.ExpiresAtUtc > now, cancellationToken);

    public Task<int> RevokeFamilyAsync(Guid familyId, RefreshSessionRevocationReason reason, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        dbContext.RefreshSessions
            .Where(s => s.FamilyId == familyId && s.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(s => s.RevokedAtUtc, (DateTimeOffset?)now)
                    .SetProperty(s => s.RevocationReason, (RefreshSessionRevocationReason?)reason),
                cancellationToken);

    public Task<int> RevokeAllForUserAsync(Guid userId, RefreshSessionRevocationReason reason, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        dbContext.RefreshSessions
            .Where(s => s.UserId == userId && s.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(s => s.RevokedAtUtc, (DateTimeOffset?)now)
                    .SetProperty(s => s.RevocationReason, (RefreshSessionRevocationReason?)reason),
                cancellationToken);
}
