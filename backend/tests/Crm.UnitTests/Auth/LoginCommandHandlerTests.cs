using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Auth;
using Crm.Application.Auth.Login;
using Crm.Domain.Identity;

namespace Crm.UnitTests.Auth;

public sealed class LoginCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidCredentials_IssuesTokensAndPersistsOnlyRefreshTokenHash()
    {
        var customer = new UserAccount(Guid.NewGuid(), "customer@example.com", "Ada", "Lovelace", null,
            UserRoles.Customer, IsActive: true, SecurityStamp: "stamp", CreatedAtUtc: DateTimeOffset.UtcNow);
        var sessions = new FakeRefreshSessionRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new LoginCommandHandler(
            new FakeIdentityService(customer),
            new AuthSessionFactory(new FakeTokenService(), sessions),
            unitOfWork,
            TimeProvider.System);

        var session = await handler.Handle(new LoginCommand("customer@example.com", "correct horse"), CancellationToken.None);

        Assert.Equal("access-token", session.AccessToken.Token);
        Assert.Equal(UserRoles.Customer, session.User.Role);
        var stored = Assert.Single(sessions.Added);
        Assert.Equal(RefreshTokens.Hash(session.RefreshToken), stored.TokenHash);
        Assert.NotEqual(session.RefreshToken, stored.TokenHash);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    private sealed class FakeIdentityService(UserAccount account) : IIdentityService
    {
        public Task<UserAccount?> CheckCredentialsAsync(string email, string password, CancellationToken cancellationToken = default) =>
            Task.FromResult<UserAccount?>(account);

        public Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserAccount> CreateUserAsync(NewUserAccount a, string password, string role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UserAccount>> ListByRoleAsync(string role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserAccount> UpdateProfileAsync(Guid userId, UserProfileUpdate update, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SetActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeTokenService : ITokenService
    {
        public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(7);
        public TimeSpan RefreshTokenReuseGracePeriod => TimeSpan.FromSeconds(30);
        public AccessToken CreateAccessToken(UserAccount user, DateTimeOffset now) => new("access-token", now.AddMinutes(10));
    }

    private sealed class FakeRefreshSessionRepository : IRefreshSessionRepository
    {
        public List<RefreshSession> Added { get; } = [];
        public void Add(RefreshSession session) => Added.Add(session);

        public Task<RefreshSession?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> TryRevokeAsync(Guid sessionId, RefreshSessionRevocationReason reason, Guid? replacedBySessionId, DateTimeOffset now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> AnyActiveInFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> RevokeFamilyAsync(Guid familyId, RefreshSessionRevocationReason reason, DateTimeOffset now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> RevokeAllForUserAsync(Guid userId, RefreshSessionRevocationReason reason, DateTimeOffset now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { SaveCount++; return Task.FromResult(1); }
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
