using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using FluentValidation;
using Mediator;

namespace Crm.Application.Customers.ListAttachments;

public sealed record ListCustomerAttachmentsQuery(Guid CustomerId, int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IQuery<PagedResult<CustomerAttachmentDetails>>;

public sealed class ListCustomerAttachmentsQueryHandler(ICustomerRecordRepository records)
    : IQueryHandler<ListCustomerAttachmentsQuery, PagedResult<CustomerAttachmentDetails>>
{
    public async ValueTask<PagedResult<CustomerAttachmentDetails>> Handle(ListCustomerAttachmentsQuery query, CancellationToken cancellationToken)
    {
        if (!await records.CustomerExistsAsync(query.CustomerId, cancellationToken))
        {
            throw new NotFoundException($"Customer '{query.CustomerId}' was not found.");
        }

        return await records.ListAttachmentsAsync(query.CustomerId, query.Page, Math.Min(query.PageSize, Paging.MaxPageSize), cancellationToken);
    }
}

public sealed class ListCustomerAttachmentsValidator : AbstractValidator<ListCustomerAttachmentsQuery>
{
    public ListCustomerAttachmentsValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, Paging.MaxPage);
        RuleFor(q => q.PageSize).GreaterThanOrEqualTo(1);
    }
}
