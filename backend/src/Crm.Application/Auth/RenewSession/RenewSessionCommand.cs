using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Messaging;
using Crm.Application.Abstractions.Persistence;
using Crm.Domain.Identity;
using Mediator;

namespace Crm.Application.Auth.RenewSession;

/// <summary>
/// Rotates a refresh token. Transactional (revoke old + insert new are atomic).
/// Rejections are RETURNED, not thrown, so that reuse-detection revocations still commit.
/// A token rotated within the grace period is a concurrent tab, not replay: it gets a sibling session.
/// </summary>
public sealed record RenewSessionCommand(string RefreshToken) : ICommand<RenewSessionResult>, ITransactionalCommand;

public sealed record RenewSessionResult(AuthSession? Session)
{
    public static readonly RenewSessionResult Rejected = new((AuthSession?)null);
}

public sealed class RenewSessionCommandHandler(
    IRefreshSessionRepository sessions,
    IIdentityService identity,
    AuthSessionFactory sessionFactory,
    ITokenService tokenService,
    TimeProvider timeProvider) : ICommandHandler<RenewSessionCommand, RenewSessionResult>
{
    public async ValueTask<RenewSessionResult> Handle(RenewSessionCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return RenewSessionResult.Rejected;
        }

        var now = timeProvider.GetUtcNow();
        var tokenHash = RefreshTokens.Hash(command.RefreshToken);
        var current = await sessions.FindByTokenHashAsync(tokenHash, cancellationToken);
        if (current is null || current.ExpiresAtUtc <= now)
        {
            return RenewSessionResult.Rejected;
        }

        if (current.RevokedAtUtc is not null)
        {
            return await HandleRevokedAsync(current, now, cancellationToken);
        }

        var user = await ActiveUserOrRevokeFamilyAsync(current, now, cancellationToken);
        if (user is null)
        {
            return RenewSessionResult.Rejected;
        }

        var nextSessionId = Guid.CreateVersion7();
        if (await sessions.TryRevokeAsync(current.Id, RefreshSessionRevocationReason.Rotated, nextSessionId, now, cancellationToken))
        {
            return new RenewSessionResult(sessionFactory.Issue(user, current.FamilyId, now, nextSessionId));
        }

        // Lost the row-level race to another request with the same token (it has now committed):
        // re-read and apply the same grace rule instead of assuming theft.
        var winner = await sessions.FindByTokenHashAsync(tokenHash, cancellationToken);
        return winner is null ? RenewSessionResult.Rejected : await HandleRevokedAsync(winner, now, cancellationToken);
    }

    private async Task<RenewSessionResult> HandleRevokedAsync(RefreshSession presented, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (presented.RevocationReason != RefreshSessionRevocationReason.Rotated)
        {
            return RenewSessionResult.Rejected; // logout, password change, disable, reuse: already dead
        }

        var rotatedRecently = presented.RevokedAtUtc >= now - tokenService.RefreshTokenReuseGracePeriod;
        if (rotatedRecently && await sessions.AnyActiveInFamilyAsync(presented.FamilyId, now, cancellationToken))
        {
            // Benign race: another tab of the same login rotated this token moments ago.
            // Issue a sibling session in the same family; the old token stays revoked.
            var user = await ActiveUserOrRevokeFamilyAsync(presented, now, cancellationToken);
            return user is null
                ? RenewSessionResult.Rejected
                : new RenewSessionResult(sessionFactory.Issue(user, presented.FamilyId, now));
        }

        if (!rotatedRecently)
        {
            // A rotated token replayed after the grace window: assume theft, kill the family.
            await sessions.RevokeFamilyAsync(presented.FamilyId, RefreshSessionRevocationReason.ReuseDetected, now, cancellationToken);
        }
        return RenewSessionResult.Rejected;
    }

    private async Task<UserAccount?> ActiveUserOrRevokeFamilyAsync(RefreshSession session, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var user = await identity.FindByIdAsync(session.UserId, cancellationToken);
        if (user is { IsActive: true })
        {
            return user;
        }

        await sessions.RevokeFamilyAsync(session.FamilyId, RefreshSessionRevocationReason.AccountInvalid, now, cancellationToken);
        return null;
    }
}
