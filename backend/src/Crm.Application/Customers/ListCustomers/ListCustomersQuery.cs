using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Paging;
using Mediator;

namespace Crm.Application.Customers.ListCustomers;

public sealed record ListCustomersQuery(string? Search, int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IQuery<PagedResult<CustomerDetails>>;

public sealed class ListCustomersQueryHandler(ICustomerRepository customers)
    : IQueryHandler<ListCustomersQuery, PagedResult<CustomerDetails>>
{
    public async ValueTask<PagedResult<CustomerDetails>> Handle(ListCustomersQuery query, CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var pageSize = Math.Min(query.PageSize, Paging.MaxPageSize); // documented cap: larger values are clamped
        return await customers.ListAsync(search, query.Page, pageSize, cancellationToken);
    }
}
