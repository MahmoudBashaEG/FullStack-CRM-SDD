using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using Mediator;

namespace Crm.Application.QuickReplies.GetQuickReply;

public sealed record GetQuickReplyQuery(StaffActor Actor, Guid Id) : IQuery<QuickReplyDetails>;

public sealed class GetQuickReplyQueryHandler(IQuickReplyRepository templates) : IQueryHandler<GetQuickReplyQuery, QuickReplyDetails>
{
    public async ValueTask<QuickReplyDetails> Handle(GetQuickReplyQuery query, CancellationToken cancellationToken) =>
        await templates.FindDetailsByIdAsync(query.Id, query.Actor, cancellationToken)
            ?? throw new NotFoundException($"Quick reply '{query.Id}' was not found.");
}
