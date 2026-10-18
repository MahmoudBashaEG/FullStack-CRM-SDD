using Crm.Api.Authentication;
using Crm.Api.Contracts.AgentDashboard;
using Crm.Api.Contracts.AgentTasks;
using Crm.Application.AgentDashboard.GetAgentDashboard;
using Crm.Application.AgentTasks.GetAgentTaskSummary;
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

    [EndpointDescription("Requires role: Agent (SuperAdmin and Customer: 403). Caller's Pending, unarchived tasks: pending, overdue (due < now) and dueSoon (now ≤ due ≤ now + 7 days) counts over ALL such tasks, plus the next 5 (no-due-date last, due ascending, id). Server UTC. Separate from the ticket snapshot so either can fail independently.")]
    [HttpGet("tasks")]
    public async Task<ActionResult<AgentTaskSummaryResponse>> Tasks(CancellationToken cancellationToken) =>
        Ok(AgentTaskSummaryResponse.From(await sender.Send(new GetAgentTaskSummaryQuery(User.GetUserId()), cancellationToken)));
}
