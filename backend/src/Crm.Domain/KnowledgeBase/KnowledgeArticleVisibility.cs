namespace Crm.Domain.KnowledgeBase;

/// <summary>
/// K2. Independent of status. Public = eligible for the future Customer Portal (Published + Public only).
/// KB-001 exposes no customer/anonymous endpoint, so today both values are staff-only.
/// </summary>
public enum KnowledgeArticleVisibility
{
    Internal = 0,
    Public = 1,
}
