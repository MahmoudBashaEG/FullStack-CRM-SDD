using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Administration;
using Crm.Application.Administration.SetAccountStatus;
using Crm.Application.Common.Paging;
using Crm.Domain.Administration;
using Crm.Domain.Identity;

namespace Crm.UnitTests.Administration;

public sealed class SetAccountStatusCommandHandlerTests
{
    [Fact]
    public async Task Handle_DeactivateActiveAgent_RotatesAccessRevokesSessionsAndWritesOneAuditEntryInTheSameSave()
    {
        var actorId = Guid.NewGuid();
        var agent = new UserAccount(Guid.NewGuid(), "agent@example.com", "Ada", "Lovelace", null,
            UserRoles.Agent, IsActive: true, SecurityStamp: "stamp", CreatedAtUtc: DateTimeOffset.UtcNow);
        var now = new DateTimeOffset(2026, 10, 18, 14, 5, 0, TimeSpan.Zero);
        var identity = new FakeIdentityService(agent);
        var sessions = new FakeRefreshSessionRepository();
        var audit = new FakeAdminAuditRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new SetAccountStatusCommandHandler(
            new FakeAccountAdministration(agent.Id), identity, sessions, audit, unitOfWork, new FixedTimeProvider(now));

        var result = await handler.Handle(new SetAccountStatusCommand(actorId, agent.Id, IsActive: false), CancellationToken.None);

        Assert.True(result.Changed);
        Assert.False(result.Account.IsActive);
        Assert.Equal([(agent.Id, false)], identity.SetActiveCalls);
        Assert.Equal([(agent.Id, RefreshSessionRevocationReason.AccountDisabled, now)], sessions.Revocations);
        var entry = Assert.Single(audit.Added);
        Assert.Equal(AdminAuditAction.AccountDeactivated, entry.Action);
        Assert.Equal(actorId, entry.ActorUserId);
        Assert.Equal(AdminAuditTargetType.UserAccount, entry.TargetType);
        Assert.Equal(agent.Id.ToString(), entry.TargetId);
        Assert.Equal("agent@example.com", entry.TargetLabel);
        Assert.Equal(now, entry.OccurredAtUtc);
        Assert.Equal(new AdminAuditChange("isActive", "true", "false"), Assert.Single(entry.Changes));
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeAccountAdministration(Guid lockedId) : IAccountAdministration
    {
        public Task<LockedAccountRow?> LockAccountAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<LockedAccountRow?>(new LockedAccountRow(lockedId, true));

        public Task<IReadOnlyList<LockedAccountRow>> LockSuperAdminsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<UserAccount>> ListAsync(AccountListFilter filter, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeIdentityService(UserAccount account) : IIdentityService
    {
        private UserAccount current = account;
        public List<(Guid, bool)> SetActiveCalls { get; } = [];

        public Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<UserAccount?>(current);

        public Task SetActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default)
        {
            SetActiveCalls.Add((userId, isActive));
            current = current with { IsActive = isActive, SecurityStamp = "rotated" };
            return Task.CompletedTask;
        }

        public Task<UserAccount?> CheckCredentialsAsync(string email, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserAccount> CreateUserAsync(NewUserAccount a, string password, string role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UserAccount>> ListByRoleAsync(string role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserAccount> UpdateProfileAsync(Guid userId, UserProfileUpdate update, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeRefreshSessionRepository : IRefreshSessionRepository
    {
        public List<(Guid, RefreshSessionRevocationReason, DateTimeOffset)> Revocations { get; } = [];

        public Task<int> RevokeAllForUserAsync(Guid userId, RefreshSessionRevocationReason reason, DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            Revocations.Add((userId, reason, now));
            return Task.FromResult(1);
        }

        public Task<RefreshSession?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Add(RefreshSession session) => throw new NotSupportedException();
        public Task<bool> TryRevokeAsync(Guid sessionId, RefreshSessionRevocationReason reason, Guid? replacedBySessionId, DateTimeOffset now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> AnyActiveInFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> RevokeFamilyAsync(Guid familyId, RefreshSessionRevocationReason reason, DateTimeOffset now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeAdminAuditRepository : IAdminAuditRepository
    {
        public List<AdminAuditEntry> Added { get; } = [];
        public void Add(AdminAuditEntry entry) => Added.Add(entry);

        public Task<PagedResult<AdminAuditEntryDetails>> ListAsync(
            AdminAuditAction? action, DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, string? search,
            int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { SaveCount++; return Task.FromResult(1); }
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
