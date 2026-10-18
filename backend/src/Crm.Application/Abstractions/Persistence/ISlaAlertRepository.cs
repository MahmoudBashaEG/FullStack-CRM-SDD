using Crm.Application.AgentTasks;
using Crm.Application.Common;
using Crm.Application.Common.Paging;
using Crm.Application.SlaAlerts;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>Visible = recipient is the caller AND (caller is SuperAdmin OR ticket.AssignedAgentUserId == caller), evaluated in SQL on every call.</summary>
public interface ISlaAlertRepository
{
    Task<PagedResult<SlaAlertDetails>> ListAsync(
        StaffActor actor, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(StaffActor actor, CancellationToken cancellationToken = default);

    /// <summary>One conditional UPDATE (explicit execution strategy). NotFound when missing or not visible.</summary>
    Task<MarkReadOutcome> MarkReadAsync(long id, StaffActor actor, DateTimeOffset now, CancellationToken cancellationToken = default);
}
