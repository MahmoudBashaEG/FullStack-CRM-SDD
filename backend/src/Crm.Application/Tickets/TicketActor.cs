namespace Crm.Application.Tickets;

/// <summary>
/// The authenticated staff member, built by the API from the validated JWT only (sub + role claims) —
/// never from a request body or query. Staff = SuperAdmin or Agent, so !IsSuperAdmin means Agent.
/// </summary>
public sealed record TicketActor(Guid UserId, bool IsSuperAdmin)
{
    /// <summary>null = every ticket (SuperAdmin); otherwise only tickets assigned to this Agent.</summary>
    public Guid? AgentScope => IsSuperAdmin ? null : UserId;
}
