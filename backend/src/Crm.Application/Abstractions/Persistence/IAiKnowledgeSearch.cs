using Crm.Application.Ai;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>
/// AI-001 retrieval (D7): Published articles only, for every role (Internal + Public are both staff-readable). Never drafts,
/// submitted or archived articles. Read-only; parameterized LIKE on title/summary/body; no index, no stored AI data.
/// </summary>
public interface IAiKnowledgeSearch
{
    /// <summary>Best matches first (number of matched terms DESC, UpdatedAtUtc DESC, Id DESC). Empty terms ⇒ empty list.</summary>
    Task<IReadOnlyList<AiKnowledgeSource>> FindPublishedAsync(
        IReadOnlyList<string> terms, int maxResults, int excerptLength, CancellationToken cancellationToken = default);
}
