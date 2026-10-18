using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Paging;
using FluentValidation;
using Mediator;

namespace Crm.Application.Collaboration.ListMyCollaborations;

public sealed record ListMyCollaborationsQuery(Guid AgentUserId, int Page = 1, int PageSize = 20)
    : IQuery<PagedResult<MyCollaborationDetails>>;

public sealed class ListMyCollaborationsQueryHandler(ICollaborationRepository collaboration)
    : IQueryHandler<ListMyCollaborationsQuery, PagedResult<MyCollaborationDetails>>
{
    public async ValueTask<PagedResult<MyCollaborationDetails>> Handle(ListMyCollaborationsQuery query, CancellationToken cancellationToken) =>
        await collaboration.ListMyCollaborationsAsync(
            query.AgentUserId, query.Page, Math.Min(query.PageSize, Paging.MaxPageSize), cancellationToken);
}

public sealed class ListMyCollaborationsValidator : AbstractValidator<ListMyCollaborationsQuery>
{
    public ListMyCollaborationsValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, Paging.MaxPage);
        RuleFor(q => q.PageSize).GreaterThanOrEqualTo(1);
    }
}
