using Crm.Application.AgentTasks;
using Crm.Application.Common;
using Crm.Application.Common.Paging;
using Crm.Domain.AgentTasks;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>
/// Add only stages; the caller saves through IUnitOfWork. Every read applies actor.AgentScope
/// (AssignedAgentUserId == the Agent) in SQL first; SuperAdmin is unrestricted. now = server UTC for due-state columns.
/// </summary>
public interface IAgentTaskRepository
{
    void Add(AgentTask task);

    /// <summary>Tracked, for updates. Null when missing or outside agentScope.</summary>
    Task<AgentTask?> GetByIdAsync(Guid id, Guid? agentScope, CancellationToken cancellationToken = default);

    Task<AgentTaskDetails?> FindDetailsByIdAsync(Guid id, StaffActor actor, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Ordered DueAtUtc null last, DueAtUtc ASC, Id ASC. Filtered, counted and paged in SQL.</summary>
    Task<PagedResult<AgentTaskDetails>> ListAsync(
        AgentTaskListFilter filter, StaffActor actor, DateTimeOffset now, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>One aggregate statement over the Agent's Pending, unarchived tasks; zeros when none.</summary>
    Task<AgentTaskCounts> GetPendingCountsAsync(Guid agentUserId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Picker data (F5): tickets whose CURRENT AssignedAgentUserId is ownerUserId, optional subject search,
    /// newest first, at most limit. Display aid only — linking re-checks under a lock.</summary>
    Task<IReadOnlyList<LinkableTicket>> ListLinkableTicketsAsync(
        Guid ownerUserId, string? search, int limit, CancellationToken cancellationToken = default);
}

/// <summary>
/// In-app reminders. Every read/write is restricted to notifications whose RecipientUserId is agentUserId AND whose task is
/// CURRENTLY owned by agentUserId (except TransferUnreadAsync, called by the reassignment). All writes are conditional
/// set-based statements (no load-then-save).
/// </summary>
public interface ITaskReminderRepository
{
    /// <summary>
    /// Idempotent, concurrency-safe single statement: inserts one notification per due, not-yet-issued reminder generation of the
    /// Agent's Pending, unarchived tasks, reading the task rows under UPDLOCK. Returns the number inserted (0 on repeat calls).
    /// </summary>
    Task<int> IssueDueAsync(Guid agentUserId, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(Guid agentUserId, CancellationToken cancellationToken = default);

    /// <summary>Newest first (IssuedAtUtc DESC, Id DESC).</summary>
    Task<PagedResult<TaskReminderDetails>> ListAsync(
        Guid agentUserId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Sets ReadAtUtc only while the row is unread, addressed to agentUserId and its task is owned by agentUserId at execution time.</summary>
    Task<MarkReadOutcome> MarkReadAsync(long id, Guid agentUserId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>
    /// F2: moves every UNREAD notification of taskId to toUserId (same rows; TransferredFromUserId/TransferredAtUtc set; still unread).
    /// Read rows are untouched. Must run inside the reassignment transaction, AFTER the task row was updated. Returns rows moved.
    /// </summary>
    Task<int> TransferUnreadAsync(Guid taskId, Guid toUserId, DateTimeOffset now, CancellationToken cancellationToken = default);
}
