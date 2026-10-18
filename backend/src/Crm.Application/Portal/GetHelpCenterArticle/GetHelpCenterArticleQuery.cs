using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Mediator;

namespace Crm.Application.Portal.GetHelpCenterArticle;

public sealed record GetHelpCenterArticleQuery(Guid UserId, Guid Id) : IQuery<HelpCenterArticleDetails>;

public sealed class GetHelpCenterArticleQueryHandler(ICustomerRepository customers, IHelpCenterRepository helpCenter)
    : IQueryHandler<GetHelpCenterArticleQuery, HelpCenterArticleDetails>
{
    public async ValueTask<HelpCenterArticleDetails> Handle(GetHelpCenterArticleQuery query, CancellationToken cancellationToken)
    {
        await customers.ResolveIdAsync(query.UserId, cancellationToken);
        return await helpCenter.FindArticleAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException($"Help article '{query.Id}' was not found.");
    }
}
