namespace Crm.Domain.QuickReplies;

/// <summary>Stored as the member name. Personal = one Agent's own; Shared = SuperAdmin-managed, read by Agents.</summary>
public enum QuickReplyScope
{
    Personal = 0,
    Shared = 1,
}
