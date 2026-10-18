using Crm.Application.Abstractions.Persistence;
using Crm.Application.KnowledgeBase;
using Crm.Domain.KnowledgeBase;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

internal sealed class TicketKnowledgeLinkRepository(CrmDbContext dbContext) : ITicketKnowledgeLinkRepository
{
    // The existing ticket boundary (TICK-002): SuperAdmin any ticket, Agent only currently assigned tickets. Never tracked.
    public Task<bool> TicketIsVisibleAsync(Guid ticketId, Guid? agentScope, CancellationToken cancellationToken = default) =>
        TicketRepository.Scope(dbContext.Tickets.AsNoTracking(), agentScope).AnyAsync(t => t.Id == ticketId, cancellationToken);

    public Task<bool> LinkExistsAsync(Guid ticketId, Guid articleId, CancellationToken cancellationToken = default) =>
        dbContext.TicketKnowledgeArticleLinks.AnyAsync(l => l.TicketId == ticketId && l.ArticleId == articleId, cancellationToken);

    public Task<int> CountForTicketAsync(Guid ticketId, CancellationToken cancellationToken = default) =>
        dbContext.TicketKnowledgeArticleLinks.CountAsync(l => l.TicketId == ticketId, cancellationToken);

    public void Add(TicketKnowledgeArticleLink link) => dbContext.TicketKnowledgeArticleLinks.Add(link);

    public Task<TicketKnowledgeArticleLink?> GetAsync(Guid ticketId, Guid articleId, CancellationToken cancellationToken = default) =>
        dbContext.TicketKnowledgeArticleLinks.SingleOrDefaultAsync(l => l.TicketId == ticketId && l.ArticleId == articleId, cancellationToken);

    public void Remove(TicketKnowledgeArticleLink link) => dbContext.TicketKnowledgeArticleLinks.Remove(link);

    public Task<TicketKnowledgeLinkDetails> GetDetailsAsync(long id, CancellationToken cancellationToken = default) =>
        Project(dbContext.TicketKnowledgeArticleLinks.AsNoTracking().Where(l => l.Id == id)).SingleAsync(cancellationToken);

    public async Task<IReadOnlyList<TicketKnowledgeLinkDetails>> ListForTicketAsync(Guid ticketId, CancellationToken cancellationToken = default) =>
        await Project(dbContext.TicketKnowledgeArticleLinks.AsNoTracking().Where(l => l.TicketId == ticketId)).ToListAsync(cancellationToken);

    // Metadata only: the article body is never selected.
    private IQueryable<TicketKnowledgeLinkDetails> Project(IQueryable<TicketKnowledgeArticleLink> links) =>
        from l in links
        join a in dbContext.KnowledgeArticles on l.ArticleId equals a.Id
        join c in dbContext.KnowledgeCategories on a.CategoryId equals c.Id
        join u in dbContext.Users on l.LinkedByUserId equals u.Id
        orderby l.LinkedAtUtc descending, l.Id descending
        select new TicketKnowledgeLinkDetails(
            l.Id, l.TicketId, l.ArticleId, a.Title, a.Type, c.Name, a.Status, u.FirstName + " " + u.LastName, l.LinkedAtUtc);
}
