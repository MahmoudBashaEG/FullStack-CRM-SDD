using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using FluentValidation;
using Mediator;

namespace Crm.Application.Customers.ListInteractions;

public sealed record ListCustomerInteractionsQuery(Guid CustomerId, int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IQuery<PagedResult<CustomerInteractionDetails>>;

public sealed class ListCustomerInteractionsQueryHandler(ICustomerRecordRepository records)
    : IQueryHandler<ListCustomerInteractionsQuery, PagedResult<CustomerInteractionDetails>>
{
    public async ValueTask<PagedResult<CustomerInteractionDetails>> Handle(ListCustomerInteractionsQuery query, CancellationToken cancellationToken)
    {
        if (!await records.CustomerExistsAsync(query.CustomerId, cancellationToken))
        {
            throw new NotFoundException($"Customer '{query.CustomerId}' was not found.");
        }

        return await records.ListInteractionsAsync(query.CustomerId, query.Page, Math.Min(query.PageSize, Paging.MaxPageSize), cancellationToken);
    }
}

public sealed class ListCustomerInteractionsValidator : AbstractValidator<ListCustomerInteractionsQuery>
{
    public ListCustomerInteractionsValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, Paging.MaxPage);
        RuleFor(q => q.PageSize).GreaterThanOrEqualTo(1);
    }
}
