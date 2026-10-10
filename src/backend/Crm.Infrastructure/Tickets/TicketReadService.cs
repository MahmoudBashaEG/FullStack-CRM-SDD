using System.Globalization;
using Crm.Application.Common;
using Crm.Application.Tickets.Contracts;
using Crm.Application.Tickets.Ports;
using Crm.Application.Tickets.Queries;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Crm.Domain.Accounts;
namespace Crm.Infrastructure.Tickets;

public sealed class TicketReadService(CrmDbContext database) : ITicketReadService
{
    public Task<TicketPageDto<TicketSummaryDto>> ListAsync(TicketListCriteria criteria, CancellationToken cancellationToken) => DatabaseOperation.RunAsync(() => ReadListAsync(criteria, cancellationToken));
    private async Task<TicketPageDto<TicketSummaryDto>> ReadListAsync(TicketListCriteria criteria, CancellationToken cancellationToken)
    {
        var term = criteria.Q; var status = criteria.Status?.ToString(); var priority = criteria.Priority?.ToString();
        var offset = ((long)criteria.Page - 1) * criteria.PageSize;
        // Short needles use CHARINDEX; longer needles use the migration-managed literal matcher.
        // Both respect the subject's CI/AS/SC collation and treat %_[] as text, not patterns.
        // Reference equality retains the configured binary collation. All values are SQL parameters.
        var tickets = database.Tickets.FromSqlInterpolated($"""
            SELECT * FROM dbo.Tickets
            WHERE ({term} IS NULL OR [Reference]={term}
              OR CHARINDEX(CASE WHEN DATALENGTH({term})<=8000 THEN {term} ELSE NULL END,[Subject])>0
              OR (DATALENGTH({term})>8000 AND dbo.TicketSubjectContainsLiteral([Subject],{term})=1))
              AND ({status} IS NULL OR [Status]={status})
              AND ({criteria.CategoryId} IS NULL OR [CategoryId]={criteria.CategoryId})
              AND ({priority} IS NULL OR [Priority]={priority})
              AND ({criteria.Unassigned}=0 OR [AssignedAgentId] IS NULL)
              AND ({criteria.AgentId} IS NULL OR [AssignedAgentId]={criteria.AgentId})
            ORDER BY [UpdatedAtUtc] DESC,[Reference] ASC
            OFFSET {offset} ROWS FETCH NEXT {criteria.PageSize + 1} ROWS ONLY
            """);
        var rows = await (from ticket in tickets.AsNoTracking()
                          join category in database.Categories.AsNoTracking() on ticket.CategoryId equals category.Id
                          join agent in database.Agents.AsNoTracking() on ticket.AssignedAgentId equals agent.Id into assignments
                          from agent in assignments.DefaultIfEmpty()
                          orderby ticket.UpdatedAtUtc descending, ticket.Reference
                          select new TicketSummaryDto(ticket.Reference, ticket.Subject, new(category.Id, category.DisplayName, category.IsAvailable),
                              ticket.Priority.ToString(), ticket.Status.ToString(), agent == null ? null : new TicketAgentDto(agent.Id, agent.DisplayName, agent.IsEligible&&database.Users.Any(user=>user.Id==agent.LocalUserId&&user.IsActive&&user.AccountRole==AccountRole.Agent)), ticket.UpdatedAtUtc)).ToListAsync(cancellationToken);
        return new(rows.Take(criteria.PageSize).ToArray(), criteria.Page, criteria.PageSize, rows.Count > criteria.PageSize);
    }
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
                          agent == null ? null : new TicketAgentDto(agent.Id, agent.DisplayName, agent.IsEligible&&database.Users.Any(user=>user.Id==agent.LocalUserId&&user.IsActive&&user.AccountRole==AccountRole.Agent)), ticket.CreatedAtUtc, ticket.UpdatedAtUtc,
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
            TicketHistoryValues.Wire(x.PreviousValue), TicketHistoryValues.Wire(x.NewValue), new Actor(x.ActorIssuer, x.ActorSubject, x.ActorDisplayName), x.OccurredAtUtc)).ToArray();
        return new(items, page, pageSize, rows.Count > pageSize);
    }
    internal static TicketDetailDto CreatedDetail(Ticket ticket, CategoryReference category, byte[] rowVersion) =>
        new(ticket.Reference, ticket.Subject, ticket.Description, new(category.Id, category.DisplayName, category.IsAvailable), ticket.Priority.ToString(), ticket.Status.ToString(), null, ticket.CreatedAtUtc, ticket.UpdatedAtUtc, Convert.ToBase64String(rowVersion));
}
