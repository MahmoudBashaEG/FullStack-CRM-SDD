namespace Crm.Domain.KnowledgeBase;

/// <summary>
/// K2. Independent of status. Public + Published = readable by signed-in Customers in the Customer Help Center
/// (PORTAL-001, /api/v1/portal/help-center). Internal = staff only. No anonymous access.
/// </summary>
public enum KnowledgeArticleVisibility
{
    Internal = 0,
    Public = 1,
}
