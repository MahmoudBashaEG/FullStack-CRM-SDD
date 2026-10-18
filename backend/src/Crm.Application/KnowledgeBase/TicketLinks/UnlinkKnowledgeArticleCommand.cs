using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using Mediator;

namespace Crm.Application.KnowledgeBase.TicketLinks;

/// <summary>Works for any article status (including links to archived articles). Never changes the ticket.</summary>
public sealed record UnlinkKnowledgeArticleCommand(StaffActor Actor, Guid TicketId, Guid ArticleId) : ICommand;

public sealed class UnlinkKnowledgeArticleCommandHandler(ITicketKnowledgeLinkRepository links, IUnitOfWork unitOfWork)
    : ICommandHandler<UnlinkKnowledgeArticleCommand>
{
    public async ValueTask<Unit> Handle(UnlinkKnowledgeArticleCommand command, CancellationToken cancellationToken)
    {
        if (!await links.TicketIsVisibleAsync(command.TicketId, command.Actor.AgentScope, cancellationToken))
        {
            throw new NotFoundException($"Ticket '{command.TicketId}' was not found.");
        }

        var link = await links.GetAsync(command.TicketId, command.ArticleId, cancellationToken)
            ?? throw new NotFoundException("This article is not linked to the ticket.");

        links.Remove(link);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
