using System.Globalization;
using Crm.Application.Common;
using Crm.Application.Tickets.Contracts;
using Crm.Application.Tickets.Ports;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Crm.Infrastructure.Tickets;

public sealed class TicketReadService(CrmDbContext database) : ITicketReadService
{
    public Task<TicketDetailDto?> GetAsync(string reference, CancellationToken cancellationToken) => DatabaseOperation.RunAsync(() => ReadDetailAsync(reference, cancellationToken));
    private async Task<TicketDetailDto?> ReadDetailAsync(string reference, CancellationToken cancellationToken)
    {
        return await (from ticket in database.Tickets.AsNoTracking()
                      join category in database.Categories.AsNoTracking() on ticket.CategoryId equals category.Id
                      join agent in database.Agents.AsNoTracking() on ticket.AssignedAgentId equals agent.Id into assignments
                      from agent in assignments.DefaultIfEmpty()
                      where ticket.Reference == reference
                      select new TicketDetailDto(ticket.Reference, ticket.Subject, ticket.Description,
                          new(category.Id, category.DisplayName, category.IsAvailable), ticket.Priority.ToString(), ticket.Status.ToString(),
                          agent == null ? null : new TicketAgentDto(agent.Id, agent.DisplayName, agent.IsEligible), ticket.CreatedAtUtc, ticket.UpdatedAtUtc,
                          Convert.ToBase64String(EF.Property<byte[]>(ticket, "RowVersion")))).SingleOrDefaultAsync(cancellationToken);
    }
    public Task<TicketPageDto<TicketHistoryDto>?> GetHistoryAsync(string reference, int page, int pageSize, CancellationToken cancellationToken) => DatabaseOperation.RunAsync(() => ReadHistoryAsync(reference, page, pageSize, cancellationToken));
    private async Task<TicketPageDto<TicketHistoryDto>?> ReadHistoryAsync(string reference, int page, int pageSize, CancellationToken cancellationToken)
    {
        var id = await database.Tickets.AsNoTracking().Where(x => x.Reference == reference).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);
        if (id is null) return null;
        var offset = ((long)page - 1) * pageSize;
        var rows = await database.History.FromSqlInterpolated($"SELECT * FROM dbo.TicketHistoryEntries WHERE TicketId={id.Value} ORDER BY Revision,Sequence OFFSET {offset} ROWS FETCH NEXT {pageSize + 1} ROWS ONLY").AsNoTracking().ToListAsync(cancellationToken);
        var items = rows.Take(pageSize).Select(x => new TicketHistoryDto(x.Id, x.Revision.ToString(CultureInfo.InvariantCulture), x.Sequence, x.Kind, x.Field,
            x.PreviousValue, x.NewValue, new Actor(x.ActorIssuer, x.ActorSubject, x.ActorDisplayName), x.OccurredAtUtc)).ToArray();
        return new(items, page, pageSize, rows.Count > pageSize);
    }
    internal static TicketDetailDto CreatedDetail(Ticket ticket, CategoryReference category, byte[] rowVersion) =>
        new(ticket.Reference, ticket.Subject, ticket.Description, new(category.Id, category.DisplayName, category.IsAvailable), ticket.Priority.ToString(), ticket.Status.ToString(), null, ticket.CreatedAtUtc, ticket.UpdatedAtUtc, Convert.ToBase64String(rowVersion));
}
