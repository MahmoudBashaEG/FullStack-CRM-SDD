using Crm.Application.Abstractions.Persistence;
using Crm.Application.AgentTasks;
using Crm.Application.Common.Paging;
using Crm.Domain.AgentTasks;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

internal sealed class TaskReminderRepository(CrmDbContext dbContext) : ITaskReminderRepository
{
    // One atomic, parameterized statement:
    //  - AgentTasks WITH (UPDLOCK, ROWLOCK): waits for an uncommitted reassignment / completion / archive of the same task and then
    //    evaluates the COMMITTED row. Required because CrmDev runs READ_COMMITTED_SNAPSHOT ON, where a plain read would use the
    //    old row version and could address a reminder to an owner who is losing the task.
    //  - NOT EXISTS … WITH (UPDLOCK, HOLDLOCK): serializes concurrent checks (two tabs, retries) on (TaskId, ReminderGeneration);
    //    UX_TaskReminderNotifications_TaskGeneration is the final guarantee.
    //  Retries (R2): ExecuteSqlInterpolatedAsync is NOT assumed to use the execution strategy, so it is wrapped explicitly. The
    //  statement is idempotent: a replay after a transient error (deadlock victim 1205, broken connection) — even if the first
    //  attempt's commit actually succeeded — inserts nothing twice; issuedNow may then report 0, the unread count stays correct.
    //  If every retry fails, the check returns 500 and the next 60-second check is the final retry.
    public Task<int> IssueDueAsync(Guid agentUserId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            ct => dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [TaskReminderNotifications] ([TaskId], [RecipientUserId], [ReminderGeneration], [ScheduledForUtc], [IssuedAtUtc])
            SELECT t.[Id], t.[AssignedAgentUserId], t.[ReminderGeneration], t.[RemindAtUtc], {now}
            FROM [AgentTasks] AS t WITH (UPDLOCK, ROWLOCK)
            WHERE t.[AssignedAgentUserId] = {agentUserId}
              AND t.[Status] = N'Pending'
              AND t.[IsArchived] = CAST(0 AS bit)
              AND t.[RemindAtUtc] IS NOT NULL
              AND t.[RemindAtUtc] <= {now}
              AND NOT EXISTS (
                  SELECT 1 FROM [TaskReminderNotifications] AS n WITH (UPDLOCK, HOLDLOCK)
                  WHERE n.[TaskId] = t.[Id] AND n.[ReminderGeneration] = t.[ReminderGeneration]);
            """, ct),
            cancellationToken);

    public Task<int> CountUnreadAsync(Guid agentUserId, CancellationToken cancellationToken = default) =>
        Owned(agentUserId).CountAsync(n => n.ReadAtUtc == null, cancellationToken);

    public async Task<PagedResult<TaskReminderDetails>> ListAsync(
        Guid agentUserId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = Owned(agentUserId);
        if (unreadOnly)
        {
            query = query.Where(n => n.ReadAtUtc == null);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await (
                from n in query
                join t in dbContext.AgentTasks on n.TaskId equals t.Id
                orderby n.IssuedAtUtc descending, n.Id descending
                select new TaskReminderDetails(
                    n.Id, t.Id, t.Title, t.Status, t.IsArchived, n.ScheduledForUtc, n.IssuedAtUtc, n.ReadAtUtc, n.TransferredAtUtc))
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<TaskReminderDetails>(items, page, pageSize, totalCount);
    }

    // Conditional UPDATE evaluated on the row at execution time. If a reassignment has already moved the row (uncommitted), this
    // statement waits on the row lock and then affects 0 rows; if the former owner marks it read first, the transfer skips it
    // (only unread rows move) and it stays as the former owner's read history.
    public async Task<MarkReadOutcome> MarkReadAsync(long id, Guid agentUserId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        // R2: explicit strategy around one idempotent autocommit statement (a replay after a lost acknowledgement affects 0 rows ⇒ AlreadyRead).
        var updated = await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            ct => dbContext.TaskReminderNotifications
                .Where(n => n.Id == id && n.ReadAtUtc == null
                            && n.RecipientUserId == agentUserId
                            && dbContext.AgentTasks.Any(t => t.Id == n.TaskId && t.AssignedAgentUserId == agentUserId))
                .ExecuteUpdateAsync(set => set.SetProperty(n => n.ReadAtUtc, now), ct),
            cancellationToken);
        if (updated == 1)
        {
            return MarkReadOutcome.MarkedRead;
        }

        return await Owned(agentUserId).AnyAsync(n => n.Id == id, cancellationToken) ? MarkReadOutcome.AlreadyRead : MarkReadOutcome.NotFound;
    }

    // F2: same rows, never new ones, so (TaskId, ReminderGeneration) stays unique. SQL Server evaluates every SET expression on the
    // pre-update row, so TransferredFromUserId receives the OLD recipient. Runs after the task UPDATE in the reassignment transaction.
    public Task<int> TransferUnreadAsync(Guid taskId, Guid toUserId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        dbContext.TaskReminderNotifications
            .Where(n => n.TaskId == taskId && n.ReadAtUtc == null && n.RecipientUserId != toUserId)
            .ExecuteUpdateAsync(set => set
                .SetProperty(n => n.TransferredFromUserId, n => n.RecipientUserId)
                .SetProperty(n => n.TransferredAtUtc, now)
                .SetProperty(n => n.RecipientUserId, toUserId), cancellationToken);

    // Recipient AND current owner: a reassigned task's read history disappears for the former owner immediately.
    private IQueryable<TaskReminderNotification> Owned(Guid agentUserId) =>
        dbContext.TaskReminderNotifications.AsNoTracking()
            .Where(n => n.RecipientUserId == agentUserId
                        && dbContext.AgentTasks.Any(t => t.Id == n.TaskId && t.AssignedAgentUserId == agentUserId));
}
