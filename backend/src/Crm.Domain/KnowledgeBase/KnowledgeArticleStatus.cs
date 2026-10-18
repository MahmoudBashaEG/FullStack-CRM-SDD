namespace Crm.Domain.KnowledgeBase;

/// <summary>Lifecycle (K1). Stored as the member name. Only Published articles appear in staff search and can be linked to tickets.</summary>
public enum KnowledgeArticleStatus
{
    Draft = 0,
    ReadyForPublication = 1,
    Published = 2,
    Archived = 3,
}
