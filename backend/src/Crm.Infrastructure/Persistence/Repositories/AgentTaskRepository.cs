using Crm.Application.Abstractions.Persistence;
using Crm.Application.AgentTasks;
using Crm.Application.Common;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Validation;
using Crm.Domain.AgentTasks;
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

internal sealed class AgentTaskRepository(CrmDbContext dbContext) : IAgentTaskRepository
{
    public void Add(AgentTask task) => dbContext.AgentTasks.Add(task);

    public Task<AgentTask?> GetByIdAsync(Guid id, Guid? agentScope, CancellationToken cancellationToken = default) =>
        Scope(dbContext.AgentTasks, agentScope).SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<AgentTaskDetails?> FindDetailsByIdAsync(Guid id, StaffActor actor, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        Project(Scope(dbContext.AgentTasks.AsNoTracking(), actor.AgentScope).Where(t => t.Id == id), actor.IsSuperAdmin, now)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<AgentTaskDetails>> ListAsync(
        AgentTaskListFilter filter, StaffActor actor, DateTimeOffset now, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var soon = now + AgentTaskRules.DueSoonWindow;
        var query = Scope(dbContext.AgentTasks.AsNoTracking(), actor.AgentScope).Where(t => t.IsArchived == filter.Archived);

        if (filter.AssignedAgentUserId is { } ownerId)
        {
            query = query.Where(t => t.AssignedAgentUserId == ownerId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(t => t.Status == status);
        }

        if (filter.HasReminder is { } hasReminder)
        {
            query = query.Where(t => (t.RemindAtUtc != null) == hasReminder);
        }

        if (!string.IsNullOrEmpty(filter.Search))
        {
            var search = filter.Search;
            query = query.Where(t => t.Title.Contains(search));
        }

        query = filter.DueState switch
        {
            TaskDueFilter.Overdue => query.Where(t => t.Status == AgentTaskStatus.Pending && !t.IsArchived && t.DueAtUtc < now),
            TaskDueFilter.DueSoon => query.Where(t => t.Status == AgentTaskStatus.Pending && !t.IsArchived && t.DueAtUtc >= now && t.DueAtUtc <= soon),
            TaskDueFilter.NoDueDate => query.Where(t => t.DueAtUtc == null),
            _ => query,
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await Project(query, actor.IsSuperAdmin, now).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<AgentTaskDetails>(items, page, pageSize, totalCount);
    }

    public async Task<AgentTaskCounts> GetPendingCountsAsync(Guid agentUserId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var soon = now + AgentTaskRules.DueSoonWindow;
        return await dbContext.AgentTasks.AsNoTracking()
                .Where(t => t.AssignedAgentUserId == agentUserId && t.Status == AgentTaskStatus.Pending && !t.IsArchived)
                .GroupBy(_ => 1) // one COUNT(CASE WHEN …) statement, as AgentDashboardRepository
                .Select(g => new AgentTaskCounts(
                    g.Count(),
                    g.Count(t => t.DueAtUtc < now),
                    g.Count(t => t.DueAtUtc >= now && t.DueAtUtc <= soon)))
                .SingleOrDefaultAsync(cancellationToken)
            ?? new AgentTaskCounts(0, 0, 0);
    }

    // F5 picker: same ownership filter as ticket reads (TicketRepository.Scope is internal, as used by AgentDashboardRepository).
    public async Task<IReadOnlyList<LinkableTicket>> ListLinkableTicketsAsync(
        Guid ownerUserId, string? search, int limit, CancellationToken cancellationToken = default)
    {
        var tickets = TicketRepository.Scope(dbContext.Tickets.AsNoTracking(), ownerUserId);
        if (!string.IsNullOrEmpty(search))
        {
            tickets = tickets.Where(t => t.Subject.Contains(search));
        }

        return await tickets
            .OrderByDescending(t => t.CreatedAtUtc).ThenByDescending(t => t.Id)
            .Select(t => new LinkableTicket(t.Id, t.Subject, t.Status, t.Priority))
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    // Applied first, in SQL: an Agent never sees another Agent's task.
    private static IQueryable<AgentTask> Scope(IQueryable<AgentTask> tasks, Guid? agentScope) =>
        agentScope is { } agentId ? tasks.Where(t => t.AssignedAgentUserId == agentId) : tasks;

    // Ordered here (EF cannot order by record members). The ticket is LEFT joined; its subject is returned only when the
    // viewer is SuperAdmin or the task owner is still the ticket's assignee. No customer columns are ever selected.
    private IQueryable<AgentTaskDetails> Project(IQueryable<AgentTask> tasks, bool viewerIsSuperAdmin, DateTimeOffset now)
    {
        var soon = now + AgentTaskRules.DueSoonWindow;
        return
            from t in tasks
            join owner in dbContext.Users on t.AssignedAgentUserId equals owner.Id
            join creator in dbContext.Users on t.CreatedByUserId equals creator.Id
            from tk in dbContext.Tickets.Where(x => x.Id == t.TicketId).DefaultIfEmpty()
            let accessible = tk != null && tk.AssignedAgentUserId == t.AssignedAgentUserId
            let pending = t.Status == AgentTaskStatus.Pending && !t.IsArchived
            orderby t.DueAtUtc == null ? 1 : 0, t.DueAtUtc, t.Id
            select new AgentTaskDetails(
                t.Id, t.Title, t.Description, t.Status,
                t.AssignedAgentUserId, owner.FirstName + " " + owner.LastName,
                t.CreatedByUserId, creator.FirstName + " " + creator.LastName,
                t.DueAtUtc, t.RemindAtUtc,
                dbContext.TaskReminderNotifications
                    .Where(n => n.TaskId == t.Id && n.ReminderGeneration == t.ReminderGeneration)
                    .Select(n => (DateTimeOffset?)n.IssuedAtUtc).FirstOrDefault(),
                t.TicketId == null ? LinkedTicketState.None : accessible ? LinkedTicketState.Accessible : LinkedTicketState.Inaccessible,
                viewerIsSuperAdmin || accessible ? t.TicketId : null,
                viewerIsSuperAdmin || accessible ? tk!.Subject : null,
                pending && t.DueAtUtc < now,
                pending && t.DueAtUtc >= now && t.DueAtUtc <= soon,
                t.CreatedAtUtc, t.UpdatedAtUtc, t.CompletedAtUtc, t.IsArchived, t.ArchivedAtUtc, t.Version);
    }
}
