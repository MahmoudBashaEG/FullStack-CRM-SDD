using Crm.Application.Abstractions.Persistence;
using Crm.Application.Administration;
using Crm.Domain.Administration;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

internal sealed class OrganizationSettingsRepository(CrmDbContext dbContext) : IOrganizationSettingsRepository
{
    public Task<OrganizationSettings?> FindAsync(CancellationToken cancellationToken = default) =>
        dbContext.OrganizationSettings.SingleOrDefaultAsync(s => s.Id == OrganizationSettings.SingletonId, cancellationToken);

    public byte[] GetVersion(OrganizationSettings settings) =>
        dbContext.Entry(settings).Property<byte[]>("Version").CurrentValue ?? [];

    public Task<string?> FindUserNameAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => (string?)(u.FirstName + " " + u.LastName))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<OrganizationLogo?> FindLogoAsync(CancellationToken cancellationToken = default) =>
        dbContext.OrganizationLogos.SingleOrDefaultAsync(l => l.Id == OrganizationLogo.SingletonId, cancellationToken);

    public void AddLogo(OrganizationLogo logo) => dbContext.OrganizationLogos.Add(logo);

    public void RemoveLogo(OrganizationLogo logo) => dbContext.OrganizationLogos.Remove(logo);

    // Only the three public columns are selected: no support data, no updater, no version.
    public Task<PublicBrandingDetails?> GetPublicBrandingAsync(CancellationToken cancellationToken = default) =>
        dbContext.OrganizationSettings.AsNoTracking()
            .Where(s => s.Id == OrganizationSettings.SingletonId)
            .Select(s => new PublicBrandingDetails(s.DisplayName, s.AccentColor, s.LogoContentType != null ? s.LogoVersion : (int?)null))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<OrganizationLogoFile?> GetLogoFileAsync(CancellationToken cancellationToken = default) =>
        dbContext.OrganizationLogos.AsNoTracking()
            .Where(l => l.Id == OrganizationLogo.SingletonId)
            .Select(l => new OrganizationLogoFile(l.Content, l.ContentType))
            .SingleOrDefaultAsync(cancellationToken);
}
