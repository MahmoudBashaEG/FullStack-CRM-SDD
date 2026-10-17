using Crm.Domain.Tickets;

namespace Crm.Api.Contracts.Tickets;

/// <summary>FromStatus = the status the caller saw (stale ⇒ 409); ToStatus must be allowed by the workflow.</summary>
public sealed record TransitionTicketRequest(TicketStatus? FromStatus, TicketStatus? ToStatus);
