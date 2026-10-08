using Crm.Application.Tickets.Contracts;
using Crm.Application.Tickets.Ports;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Tickets;

public sealed class TicketReferenceData(CrmDbContext database) : ITicketReferenceData
{
    public async Task<IReadOnlyList<ReferenceChoice>> GetCategoriesAsync(CancellationToken cancellationToken) =>
        await DatabaseOperation.RunAsync(async () => await database.Categories.AsNoTracking().Where(x => x.IsAvailable).OrderBy(x => x.DisplayName).ThenBy(x => x.Id).Select(x => new ReferenceChoice(x.Id, x.DisplayName)).ToListAsync(cancellationToken));
    public async Task<IReadOnlyList<ReferenceChoice>> GetAgentsAsync(CancellationToken cancellationToken) =>
        await DatabaseOperation.RunAsync(async () => await database.Agents.AsNoTracking().Where(x => x.IsEligible).OrderBy(x => x.DisplayName).ThenBy(x => x.Id).Select(x => new ReferenceChoice(x.Id, x.DisplayName)).ToListAsync(cancellationToken));
}
