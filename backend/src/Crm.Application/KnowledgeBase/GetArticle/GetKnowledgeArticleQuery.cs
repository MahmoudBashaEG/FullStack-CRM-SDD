using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using Mediator;

namespace Crm.Application.KnowledgeBase.GetArticle;

public sealed record GetKnowledgeArticleQuery(StaffActor Actor, Guid Id) : IQuery<KnowledgeArticleDetails>;

public sealed class GetKnowledgeArticleQueryHandler(IKnowledgeBaseRepository knowledgeBase)
    : IQueryHandler<GetKnowledgeArticleQuery, KnowledgeArticleDetails>
{
    public async ValueTask<KnowledgeArticleDetails> Handle(GetKnowledgeArticleQuery query, CancellationToken cancellationToken) =>
        await knowledgeBase.FindArticleDetailsAsync(query.Id, query.Actor, cancellationToken)
            ?? throw new NotFoundException($"Knowledge article '{query.Id}' was not found.");
}
