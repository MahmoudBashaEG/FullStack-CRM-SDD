using Crm.Application.Common.Paging;
using Crm.Application.Common.Validation;
using FluentValidation;

namespace Crm.Application.Tickets.ListTickets;

public sealed class ListTicketsValidator : AbstractValidator<ListTicketsQuery>
{
    public ListTicketsValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, Paging.MaxPage);
        RuleFor(q => q.PageSize).GreaterThanOrEqualTo(1);
        RuleFor(q => q.Search).MaximumLength(TicketRules.SearchMaxLength);
        RuleFor(q => q.Status).IsInEnum();   // null passes
        RuleFor(q => q.Category).IsInEnum();
        RuleFor(q => q.Priority).IsInEnum();
    }
}
