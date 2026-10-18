using Crm.Application.Abstractions.Persistence;
using Crm.Domain.Sla;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

internal sealed class SlaConfigurationRepository(CrmDbContext dbContext) : ISlaConfigurationRepository
{
    // Owned targets / holidays load with their owner automatically.
    public Task<SlaPolicy?> FindPolicyAsync(CancellationToken cancellationToken = default) =>
        dbContext.SlaPolicies.SingleOrDefaultAsync(p => p.Id == SlaPolicy.SingletonId, cancellationToken);

    public Task<BusinessCalendar?> FindCalendarAsync(CancellationToken cancellationToken = default) =>
        dbContext.BusinessCalendars.SingleOrDefaultAsync(c => c.Id == BusinessCalendar.SingletonId, cancellationToken);

    public void Add(SlaPolicy policy) => dbContext.SlaPolicies.Add(policy);

    public void Add(BusinessCalendar calendar) => dbContext.BusinessCalendars.Add(calendar);

    public Task<string?> FindUserNameAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => (string?)(u.FirstName + " " + u.LastName))
            .SingleOrDefaultAsync(cancellationToken);
}
