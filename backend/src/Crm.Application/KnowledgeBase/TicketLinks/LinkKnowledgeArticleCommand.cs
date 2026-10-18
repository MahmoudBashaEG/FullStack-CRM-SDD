using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Domain.KnowledgeBase;
using FluentValidation;
using Mediator;

namespace Crm.Application.KnowledgeBase.TicketLinks;

/// <summary>K4. Ticket scope = TicketRepository.Scope (collaborators ⇒ 404). Published articles only. Never changes the ticket. Single save (I5).</summary>
public sealed record LinkKnowledgeArticleCommand(StaffActor Actor, Guid TicketId, Guid ArticleId) : ICommand<TicketKnowledgeLinkDetails>;

public sealed class LinkKnowledgeArticleCommandHandler(
    IKnowledgeBaseRepository knowledgeBase,
    ITicketKnowledgeLinkRepository links,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<LinkKnowledgeArticleCommand, TicketKnowledgeLinkDetails>
{
    public async ValueTask<TicketKnowledgeLinkDetails> Handle(LinkKnowledgeArticleCommand command, CancellationToken cancellationToken)
    {
        if (!await links.TicketIsVisibleAsync(command.TicketId, command.Actor.AgentScope, cancellationToken))
        {
            throw new NotFoundException($"Ticket '{command.TicketId}' was not found.");
        }

        var article = await knowledgeBase.FindArticleDetailsAsync(command.ArticleId, command.Actor, cancellationToken)
            ?? throw new NotFoundException($"Knowledge article '{command.ArticleId}' was not found.");

        if (article.Status != KnowledgeArticleStatus.Published)
        {
            throw new ConflictException("Only published articles can be linked to a ticket.");
        }

        if (await links.LinkExistsAsync(command.TicketId, command.ArticleId, cancellationToken))
        {
            throw new ConflictException("This article is already linked to the ticket.");
        }

        if (await links.CountForTicketAsync(command.TicketId, cancellationToken) >= KnowledgeBaseRules.MaxLinksPerTicket)
        {
            throw new ConflictException($"A ticket can have at most {KnowledgeBaseRules.MaxLinksPerTicket} linked articles.");
        }

        var link = TicketKnowledgeArticleLink.Create(command.TicketId, command.ArticleId, command.Actor.UserId, timeProvider.GetUtcNow());
        links.Add(link);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await links.GetDetailsAsync(link.Id, cancellationToken);
    }
}

public sealed class LinkKnowledgeArticleValidator : AbstractValidator<LinkKnowledgeArticleCommand>
{
    public LinkKnowledgeArticleValidator()
    {
        RuleFor(c => c.TicketId).NotEmpty();
        RuleFor(c => c.ArticleId).NotEmpty();
    }
}
