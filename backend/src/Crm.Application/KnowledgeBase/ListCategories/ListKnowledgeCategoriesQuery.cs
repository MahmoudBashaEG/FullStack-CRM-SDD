using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Mediator;

namespace Crm.Application.KnowledgeBase.ListCategories;

/// <summary>Agents always get active categories only; includeInactive is honoured for SuperAdmin.</summary>
public sealed record ListKnowledgeCategoriesQuery(StaffActor Actor, bool IncludeInactive)
    : IQuery<IReadOnlyList<KnowledgeCategoryDetails>>;

public sealed class ListKnowledgeCategoriesQueryHandler(IKnowledgeBaseRepository knowledgeBase)
    : IQueryHandler<ListKnowledgeCategoriesQuery, IReadOnlyList<KnowledgeCategoryDetails>>
{
    public async ValueTask<IReadOnlyList<KnowledgeCategoryDetails>> Handle(
        ListKnowledgeCategoriesQuery query, CancellationToken cancellationToken) =>
        await knowledgeBase.ListCategoriesAsync(query.IncludeInactive && query.Actor.IsSuperAdmin, cancellationToken);
}
