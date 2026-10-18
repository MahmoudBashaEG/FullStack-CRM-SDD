using Crm.Application.Abstractions.Persistence;
using Mediator;

namespace Crm.Application.Portal.ListHelpCenterCategories;

public sealed record ListHelpCenterCategoriesQuery(Guid UserId) : IQuery<IReadOnlyList<HelpCenterCategory>>;

public sealed class ListHelpCenterCategoriesQueryHandler(ICustomerRepository customers, IHelpCenterRepository helpCenter)
    : IQueryHandler<ListHelpCenterCategoriesQuery, IReadOnlyList<HelpCenterCategory>>
{
    public async ValueTask<IReadOnlyList<HelpCenterCategory>> Handle(ListHelpCenterCategoriesQuery query, CancellationToken cancellationToken)
    {
        await customers.ResolveIdAsync(query.UserId, cancellationToken);
        return await helpCenter.ListCategoriesAsync(cancellationToken);
    }
}
