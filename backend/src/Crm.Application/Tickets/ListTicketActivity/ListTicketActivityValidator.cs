using Crm.Application.Common.Paging;
using FluentValidation;

namespace Crm.Application.Tickets.ListTicketActivity;

public sealed class ListTicketActivityValidator : AbstractValidator<ListTicketActivityQuery>
{
    public ListTicketActivityValidator()
    {
        RuleFor(q => q.TicketId).NotEmpty();
        RuleFor(q => q.Page).InclusiveBetween(1, Paging.MaxPage);
        RuleFor(q => q.PageSize).GreaterThanOrEqualTo(1);
    }
}
