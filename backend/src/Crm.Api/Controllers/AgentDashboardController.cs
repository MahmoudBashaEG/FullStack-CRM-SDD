using Crm.Api.Authentication;
using Crm.Api.Contracts.AgentDashboard;
using Crm.Application.AgentDashboard.GetAgentDashboard;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/agent-dashboard")]
[Authorize(Policy = AuthorizationPolicies.Agent)]
public sealed class AgentDashboardController(ISender sender) : ControllerBase
{
    [EndpointDescription("Requires role: Agent (SuperAdmin and Customer: 403). Read-only live snapshot of the tickets currently assigned to the caller (taken from the token; no parameters). Summary counts overlap and are not additive: open, inProgress, active (= open + inProgress), escalated (escalated and Open/InProgress), highOrUrgent (Open/InProgress with priority High or Urgent). attentionQueue: at most 10 Open/InProgress tickets, escalated first, then Urgent, High, Medium, Low, then oldest updatedAtUtc, then id — a display order, not an SLA.")]
    [HttpGet]
    public async Task<ActionResult<AgentDashboardResponse>> Get(CancellationToken cancellationToken) =>
        Ok(AgentDashboardResponse.From(await sender.Send(new GetAgentDashboardQuery(User.GetUserId()), cancellationToken)));
}
