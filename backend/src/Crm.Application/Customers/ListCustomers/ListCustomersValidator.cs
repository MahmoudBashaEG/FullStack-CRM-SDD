using Crm.Application.Common.Paging;
using Crm.Application.Common.Validation;
using FluentValidation;

namespace Crm.Application.Customers.ListCustomers;

public sealed class ListCustomersValidator : AbstractValidator<ListCustomersQuery>
{
    public ListCustomersValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, Paging.MaxPage);
        RuleFor(q => q.PageSize).GreaterThanOrEqualTo(1);
        RuleFor(q => q.Search).MaximumLength(CustomerRules.SearchMaxLength);
    }
}
