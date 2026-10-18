namespace Crm.Application.Common;

/// <summary>
/// The authenticated staff member (SuperAdmin or Agent), built by the API from the validated JWT only (sub + role).
/// Used by DASH-002 features (tasks, quick replies, collaboration). TicketActor stays the ticket module's own actor.
/// </summary>
public sealed record StaffActor(Guid UserId, bool IsSuperAdmin)
{
    /// <summary>null = unrestricted (SuperAdmin); otherwise the Agent's own user id.</summary>
    public Guid? AgentScope => IsSuperAdmin ? null : UserId;
}
