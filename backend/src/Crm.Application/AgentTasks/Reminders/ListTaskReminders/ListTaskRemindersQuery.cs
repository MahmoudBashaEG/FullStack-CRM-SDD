using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Paging;
using FluentValidation;
using Mediator;

namespace Crm.Application.AgentTasks.Reminders.ListTaskReminders;

public sealed record ListTaskRemindersQuery(
    Guid AgentUserId, bool UnreadOnly = false, int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IQuery<PagedResult<TaskReminderDetails>>;

public sealed class ListTaskRemindersQueryHandler(ITaskReminderRepository reminders)
    : IQueryHandler<ListTaskRemindersQuery, PagedResult<TaskReminderDetails>>
{
    public async ValueTask<PagedResult<TaskReminderDetails>> Handle(ListTaskRemindersQuery query, CancellationToken cancellationToken) =>
        await reminders.ListAsync(query.AgentUserId, query.UnreadOnly, query.Page, Math.Min(query.PageSize, Paging.MaxPageSize), cancellationToken);
}

public sealed class ListTaskRemindersValidator : AbstractValidator<ListTaskRemindersQuery>
{
    public ListTaskRemindersValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, Paging.MaxPage);
        RuleFor(q => q.PageSize).GreaterThanOrEqualTo(1);
    }
}
