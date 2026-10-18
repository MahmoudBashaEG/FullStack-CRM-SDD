using Crm.Application.Abstractions.Persistence;
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
}
