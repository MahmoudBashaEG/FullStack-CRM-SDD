using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Validation;
using Crm.Domain.AgentTasks;
using FluentValidation;
using Mediator;

namespace Crm.Application.AgentTasks.ListAgentTasks;

public sealed record ListAgentTasksQuery(
    StaffActor Actor,
    string? Search,
    AgentTaskStatus? Status,
    TaskDueFilter? DueState,
    bool Archived = false,
    bool? HasReminder = null,
    Guid? AssignedAgentUserId = null,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize) : IQuery<PagedResult<AgentTaskDetails>>;

public sealed class ListAgentTasksQueryHandler(IAgentTaskRepository tasks, TimeProvider timeProvider)
    : IQueryHandler<ListAgentTasksQuery, PagedResult<AgentTaskDetails>>
{
    public async ValueTask<PagedResult<AgentTaskDetails>> Handle(ListAgentTasksQuery query, CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var pageSize = Math.Min(query.PageSize, Paging.MaxPageSize);
        var filter = new AgentTaskListFilter(search, query.Status, query.DueState, query.Archived, query.HasReminder, query.AssignedAgentUserId);

        // Filters only ever narrow the actor's scope (an Agent's own tasks are applied first, in SQL).
        return await tasks.ListAsync(filter, query.Actor, timeProvider.GetUtcNow(), query.Page, pageSize, cancellationToken);
    }
}

public sealed class ListAgentTasksValidator : AbstractValidator<ListAgentTasksQuery>
{
    public ListAgentTasksValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, Paging.MaxPage);
        RuleFor(q => q.PageSize).GreaterThanOrEqualTo(1);
        RuleFor(q => q.Search).MaximumLength(AgentTaskRules.SearchMaxLength);
        RuleFor(q => q.Status).IsInEnum();
        RuleFor(q => q.DueState).IsInEnum();
        RuleFor(q => q.AssignedAgentUserId).NotEqual(Guid.Empty);
    }
}
