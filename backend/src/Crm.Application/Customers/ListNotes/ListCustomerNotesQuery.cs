using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using FluentValidation;
using Mediator;

namespace Crm.Application.Customers.ListNotes;

public sealed record ListCustomerNotesQuery(Guid CustomerId, int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IQuery<PagedResult<CustomerNoteDetails>>;

public sealed class ListCustomerNotesQueryHandler(ICustomerRecordRepository records)
    : IQueryHandler<ListCustomerNotesQuery, PagedResult<CustomerNoteDetails>>
{
    public async ValueTask<PagedResult<CustomerNoteDetails>> Handle(ListCustomerNotesQuery query, CancellationToken cancellationToken)
    {
        if (!await records.CustomerExistsAsync(query.CustomerId, cancellationToken))
        {
            throw new NotFoundException($"Customer '{query.CustomerId}' was not found.");
        }

        return await records.ListNotesAsync(query.CustomerId, query.Page, Math.Min(query.PageSize, Paging.MaxPageSize), cancellationToken);
    }
}

public sealed class ListCustomerNotesValidator : AbstractValidator<ListCustomerNotesQuery>
{
    public ListCustomerNotesValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, Paging.MaxPage);
        RuleFor(q => q.PageSize).GreaterThanOrEqualTo(1);
    }
}
