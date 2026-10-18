using Crm.Application.Abstractions.Persistence;
using Crm.Application.Ai;
using Crm.Domain.KnowledgeBase;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

/// <summary>AI-001 D7: read-only, Published articles for every role. Parameterized LIKE; the full body is never materialized.</summary>
internal sealed class AiKnowledgeSearch(CrmDbContext dbContext) : IAiKnowledgeSearch
{
    private const int MaxTerms = 8;
    private const int CandidatesPerTerm = 50;

    public async Task<IReadOnlyList<AiKnowledgeSource>> FindPublishedAsync(
        IReadOnlyList<string> terms, int maxResults, int excerptLength, CancellationToken cancellationToken = default)
    {
        if (terms.Count == 0 || maxResults <= 0)
        {
            return [];
        }

        var matches = new Dictionary<Guid, (int Count, DateTimeOffset UpdatedAtUtc)>();
        foreach (var term in terms.Take(MaxTerms))
        {
            var found = await dbContext.KnowledgeArticles.AsNoTracking()
                .Where(a => a.Status == KnowledgeArticleStatus.Published
                    && (a.Title.Contains(term) || (a.Summary != null && a.Summary.Contains(term)) || a.Body.Contains(term)))
                .Select(a => new { a.Id, a.UpdatedAtUtc })
                .Take(CandidatesPerTerm)
                .ToListAsync(cancellationToken);

            foreach (var item in found)
            {
                matches[item.Id] = matches.TryGetValue(item.Id, out var current) ? (current.Count + 1, item.UpdatedAtUtc) : (1, item.UpdatedAtUtc);
            }
        }

        var ranked = matches
            .OrderByDescending(m => m.Value.Count).ThenByDescending(m => m.Value.UpdatedAtUtc).ThenByDescending(m => m.Key)
            .Take(maxResults)
            .Select(m => m.Key)
            .ToList();
        if (ranked.Count == 0)
        {
            return [];
        }

        var sources = await dbContext.KnowledgeArticles.AsNoTracking()
            .Where(a => ranked.Contains(a.Id) && a.Status == KnowledgeArticleStatus.Published)
            .Select(a => new AiKnowledgeSource(
                a.Id, a.Title, a.Summary, a.Body.Length > excerptLength ? a.Body.Substring(0, excerptLength) : a.Body))
            .ToListAsync(cancellationToken);

        return sources.OrderBy(s => ranked.IndexOf(s.ArticleId)).ToList();
    }
}
