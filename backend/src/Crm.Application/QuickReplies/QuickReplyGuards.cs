using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using Crm.Domain.QuickReplies;

namespace Crm.Application.QuickReplies;

internal static class QuickReplyGuards
{
    /// <summary>Visible but not modifiable ⇒ 403. SuperAdmin manages Shared; an Agent manages only their own Personal templates.</summary>
    public static void EnsureCanEdit(QuickReplyTemplate template, StaffActor actor)
    {
        var allowed = actor.IsSuperAdmin
            ? template.Scope == QuickReplyScope.Shared
            : template.Scope == QuickReplyScope.Personal && template.OwnerUserId == actor.UserId;
        if (!allowed)
        {
            throw new ForbiddenException("Shared quick replies are managed by SuperAdmin.");
        }
    }
}
