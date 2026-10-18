using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Paging;
using Crm.Application.QuickReplies;
using Crm.Domain.QuickReplies;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

internal sealed class QuickReplyRepository(CrmDbContext dbContext) : IQuickReplyRepository
{
    public void Add(QuickReplyTemplate template) => dbContext.QuickReplyTemplates.Add(template);

    public Task<QuickReplyTemplate?> GetVisibleAsync(Guid id, StaffActor actor, CancellationToken cancellationToken = default) =>
        Visible(dbContext.QuickReplyTemplates, actor).SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<QuickReplyDetails?> FindDetailsByIdAsync(Guid id, StaffActor actor, CancellationToken cancellationToken = default) =>
        Project(Visible(dbContext.QuickReplyTemplates.AsNoTracking(), actor).Where(t => t.Id == id), actor).SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<QuickReplyDetails>> ListAsync(
        QuickReplyListFilter filter, StaffActor actor, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = Visible(dbContext.QuickReplyTemplates.AsNoTracking(), actor).Where(t => t.IsArchived == filter.Archived);

        if (filter.Category is { } category)
        {
            query = query.Where(t => t.Category == category);
        }

        if (filter.Scope is { } scope)
        {
            query = query.Where(t => t.Scope == scope);
        }

        if (!string.IsNullOrEmpty(filter.Search))
        {
            var search = filter.Search;
            query = query.Where(t => t.Title.Contains(search) || t.Body.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await Project(query, actor).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<QuickReplyDetails>(items, page, pageSize, totalCount);
    }

    // Applied first, in SQL. SuperAdmin: Shared only (never Personal). Agent: active Shared + own Personal.
    private static IQueryable<QuickReplyTemplate> Visible(IQueryable<QuickReplyTemplate> templates, StaffActor actor)
    {
        if (actor.IsSuperAdmin)
        {
            return templates.Where(t => t.Scope == QuickReplyScope.Shared);
        }

        var agentId = actor.UserId;
        return templates.Where(t =>
            (t.Scope == QuickReplyScope.Shared && !t.IsArchived)
            || (t.Scope == QuickReplyScope.Personal && t.OwnerUserId == agentId));
    }

    private IQueryable<QuickReplyDetails> Project(IQueryable<QuickReplyTemplate> templates, StaffActor actor)
    {
        var isSuperAdmin = actor.IsSuperAdmin;
        var agentId = actor.UserId;
        return
            from t in templates
            join creator in dbContext.Users on t.CreatedByUserId equals creator.Id
            join updater in dbContext.Users on t.UpdatedByUserId equals updater.Id
            orderby t.Title, t.Id
            select new QuickReplyDetails(
                t.Id, t.Title, t.Body, t.Category, t.Scope, t.OwnerUserId,
                creator.FirstName + " " + creator.LastName, updater.FirstName + " " + updater.LastName,
                t.CreatedAtUtc, t.UpdatedAtUtc, t.IsArchived,
                isSuperAdmin ? t.Scope == QuickReplyScope.Shared : t.Scope == QuickReplyScope.Personal && t.OwnerUserId == agentId);
    }
}
