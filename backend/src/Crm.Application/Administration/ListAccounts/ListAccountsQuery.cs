using Crm.Application.Abstractions.Identity;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Validation;
using Crm.Domain.Identity;
using FluentValidation;
using Mediator;

namespace Crm.Application.Administration.ListAccounts;

public sealed record ListAccountsQuery(string? Search, string? Role, bool? IsActive, int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IQuery<PagedResult<UserAccount>>;

public sealed class ListAccountsQueryHandler(IAccountAdministration accounts) : IQueryHandler<ListAccountsQuery, PagedResult<UserAccount>>
{
    public async ValueTask<PagedResult<UserAccount>> Handle(ListAccountsQuery query, CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var pageSize = Math.Min(query.PageSize, Paging.MaxPageSize); // documented cap, as ListCustomersQuery
        return await accounts.ListAsync(new AccountListFilter(search, query.Role, query.IsActive), query.Page, pageSize, cancellationToken);
    }
}

public sealed class ListAccountsValidator : AbstractValidator<ListAccountsQuery>
{
    public ListAccountsValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, Paging.MaxPage);
        RuleFor(q => q.PageSize).GreaterThanOrEqualTo(1);
        RuleFor(q => q.Search).MaximumLength(AdministrationRules.SearchMaxLength);
        RuleFor(q => q.Role).Must(r => r is null || UserRoles.All.Contains(r)).WithMessage("Unknown role.");
    }
}
