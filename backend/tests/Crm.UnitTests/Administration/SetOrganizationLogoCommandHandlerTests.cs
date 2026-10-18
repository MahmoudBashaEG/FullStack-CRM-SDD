using Crm.Application.Abstractions.Persistence;
using Crm.Application.Administration;
using Crm.Application.Administration.SetOrganizationLogo;
using Crm.Application.Common.Paging;
using Crm.Domain.Administration;

namespace Crm.UnitTests.Administration;

public sealed class SetOrganizationLogoCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidPngWithCurrentVersion_StoresLogoUpdatesSettingsMetadataAndWritesOneAuditEntryInOneSave()
    {
        var actorId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 18, 14, 5, 0, TimeSpan.Zero);
        var settings = (OrganizationSettings)Activator.CreateInstance(typeof(OrganizationSettings), nonPublic: true)!;
        typeof(OrganizationSettings).GetProperty(nameof(OrganizationSettings.Id))!.SetValue(settings, OrganizationSettings.SingletonId);
        settings.Update("Acme Support", null, null, null, null, actorId, now.AddDays(-1));
        byte[] version = [1, 2, 3, 4, 5, 6, 7, 8];
        var repository = new FakeOrganizationSettingsRepository(settings, version);
        var audit = new FakeAdminAuditRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new SetOrganizationLogoCommandHandler(repository, audit, unitOfWork, new FixedTimeProvider(now));
        var content = new byte[2048];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(content, 0);

        var result = await handler.Handle(new SetOrganizationLogoCommand(actorId, version, @"C:\logos\brand.PNG", content), CancellationToken.None);

        var logo = Assert.Single(repository.AddedLogos);
        Assert.Equal("image/png", logo.ContentType);
        Assert.Equal(content, logo.Content);
        Assert.True(result.HasLogo);
        Assert.Equal("image/png", result.LogoContentType);
        Assert.Equal(2048, result.LogoSizeBytes);
        Assert.Equal(now, result.LogoUpdatedAtUtc);
        Assert.Equal("Acme Support", result.DisplayName);
        var entry = Assert.Single(audit.Added);
        Assert.Equal(AdminAuditAction.OrganizationSettingsUpdated, entry.Action);
        Assert.Equal(AdminAuditTargetType.OrganizationSettings, entry.TargetType);
        Assert.Equal(new AdminAuditChange("logo", null, "image/png, 2 KB"), Assert.Single(entry.Changes));
        Assert.Equal(1, unitOfWork.SaveCount);

        // PLAT-001 C5: a second replacement in the very same instant still gets a new public version.
        var firstVersion = result.LogoVersion;
        repository.Existing = logo;
        var replaced = await handler.Handle(new SetOrganizationLogoCommand(actorId, version, "brand.png", content), CancellationToken.None);
        Assert.Equal(1, firstVersion);
        Assert.Equal(2, replaced.LogoVersion);
        Assert.Equal(result.LogoUpdatedAtUtc, replaced.LogoUpdatedAtUtc);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeOrganizationSettingsRepository(OrganizationSettings settings, byte[] version) : IOrganizationSettingsRepository
    {
        public List<OrganizationLogo> AddedLogos { get; } = [];
        public OrganizationLogo? Existing { get; set; }

        public Task<OrganizationSettings?> FindAsync(CancellationToken cancellationToken = default) => Task.FromResult<OrganizationSettings?>(settings);
        public byte[] GetVersion(OrganizationSettings s) => version;
        public Task<string?> FindUserNameAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<string?>("Admin User");
        public Task<OrganizationLogo?> FindLogoAsync(CancellationToken cancellationToken = default) => Task.FromResult(Existing);
        public void AddLogo(OrganizationLogo logo) => AddedLogos.Add(logo);
        public void RemoveLogo(OrganizationLogo logo) => throw new NotSupportedException();
        public Task<PublicBrandingDetails?> GetPublicBrandingAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OrganizationLogoFile?> GetLogoFileAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
