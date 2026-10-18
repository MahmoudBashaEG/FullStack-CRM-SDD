namespace Crm.Domain.KnowledgeBase;

/// <summary>KB-001 article kinds, one shared editor/renderer. Stored as the member name. FAQ: Title = question, Body = answer.</summary>
public enum KnowledgeArticleType
{
    Faq = 0,
    HelpArticle = 1,
    Solution = 2,
    Guide = 3,
}
