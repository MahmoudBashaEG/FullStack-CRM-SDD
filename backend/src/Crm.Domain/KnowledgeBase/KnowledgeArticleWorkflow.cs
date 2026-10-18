namespace Crm.Domain.KnowledgeBase;

/// <summary>KB-001 transition matrix — the single source of truth. Every pair not listed is illegal. Who may trigger each one is an application rule.</summary>
public static class KnowledgeArticleWorkflow
{
    public static bool IsAllowed(KnowledgeArticleStatus from, KnowledgeArticleStatus to) => (from, to) switch
    {
        (KnowledgeArticleStatus.Draft, KnowledgeArticleStatus.ReadyForPublication) => true,     // submit
        (KnowledgeArticleStatus.ReadyForPublication, KnowledgeArticleStatus.Draft) => true,     // return for revision
        (KnowledgeArticleStatus.ReadyForPublication, KnowledgeArticleStatus.Published) => true, // publish (SuperAdmin)
        (KnowledgeArticleStatus.Draft, KnowledgeArticleStatus.Archived) => true,                // archive (SuperAdmin)
        (KnowledgeArticleStatus.ReadyForPublication, KnowledgeArticleStatus.Archived) => true,
        (KnowledgeArticleStatus.Published, KnowledgeArticleStatus.Archived) => true,
        (KnowledgeArticleStatus.Archived, KnowledgeArticleStatus.Draft) => true,                // restore (SuperAdmin)
        _ => false,
    };
}
