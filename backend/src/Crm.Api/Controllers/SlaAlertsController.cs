using Crm.Api.Authentication;
using Crm.Api.Contracts.Common;
using Crm.Api.Contracts.SlaAlerts;
using Crm.Application.Common;
using Crm.Application.Common.Paging;
using Crm.Application.SlaAlerts.CountUnreadSlaAlerts;
using Crm.Application.SlaAlerts.ListSlaAlerts;
using Crm.Application.SlaAlerts.MarkSlaAlertRead;
using Crm.Domain.Identity;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/sla-alerts")]
[Authorize(Policy = AuthorizationPolicies.Staff)]
public sealed class SlaAlertsController(ISender sender) : ControllerBase
{
    [EndpointDescription("Requires role: SuperAdmin or Agent. The caller's SLA alerts, newest first; an Agent sees only alerts for tickets currently assigned to them. Optional unreadOnly. One-based page (default 1), pageSize (default 20, clamped to 100).")]
    [HttpGet]
    public async Task<ActionResult<PagedResponse<SlaAlertResponse>>> List(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new ListSlaAlertsQuery(Actor, unreadOnly, page, pageSize), cancellationToken);
        return Ok(new PagedResponse<SlaAlertResponse>(
            result.Items.Select(SlaAlertResponse.From).ToList(), result.Page, result.PageSize, result.TotalCount));
    }

    [EndpointDescription("Requires role: SuperAdmin or Agent. Number of the caller's unread, currently visible SLA alerts.")]
    [HttpGet("unread-count")]
    public async Task<ActionResult<SlaAlertCountResponse>> UnreadCount(CancellationToken cancellationToken) =>
        Ok(new SlaAlertCountResponse(await sender.Send(new CountUnreadSlaAlertsQuery(Actor), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin or Agent. Marks one of the caller's alerts as read (already read = success). Never changes the ticket. An alert that is missing or no longer visible: 404.")]
    [HttpPut("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken cancellationToken)
    {
        await sender.Send(new MarkSlaAlertReadCommand(Actor, id), cancellationToken);
        return NoContent();
    }

    private StaffActor Actor => new(User.GetUserId(), User.IsInRole(UserRoles.SuperAdmin));
}
