using Crm.Api.Authentication;
using Crm.Api.Contracts.Common;
using Crm.Api.Contracts.Portal;
using Crm.Application.Common.Paging;
using Crm.Application.Portal.GetTicket;
using Crm.Application.Portal.GetTimeline;
using Crm.Application.Portal.ListTickets;
using Crm.Application.Portal.SaveFeedback;
using Crm.Application.Portal.SubmitTicket;
using Crm.Domain.Tickets;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/portal/tickets")]
[Authorize(Policy = AuthorizationPolicies.Customer)]
public sealed class PortalTicketsController(ISender sender) : ControllerBase
{
    private const string Scope = "Requires role: Customer (only tickets of the caller's own linked CRM profile; others 404; no linked profile: 404). Staff use /api/v1/tickets.";

    [EndpointDescription(Scope + " Every ticket of the customer, whoever created it. One-based page (default 1), pageSize (default 20, clamped to 100), optional search on subject or exact ticket id, status filter. Newest first. No priority, assignee, escalation or SLA fields.")]
    [HttpGet]
    public async Task<ActionResult<PagedResponse<PortalTicketSummaryResponse>>> List(
        [FromQuery] string? search, [FromQuery] TicketStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = Paging.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new ListPortalTicketsQuery(User.GetUserId(), search, status, page, pageSize), cancellationToken);
        return Ok(new PagedResponse<PortalTicketSummaryResponse>(
            result.Items.Select(PortalTicketSummaryResponse.From).ToList(), result.Page, result.PageSize, result.TotalCount));
    }

    [EndpointDescription(Scope)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PortalTicketResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(PortalTicketResponse.From(await sender.Send(new GetPortalTicketQuery(User.GetUserId(), id), cancellationToken)));

    [EndpointDescription("Requires role: Customer (own linked CRM profile). Subject, description and category only; the ticket is always Open with Medium priority and the customer is taken from the caller. SLA and automatic assignment rules apply as for staff-created tickets. Nothing is sent to anyone.")]
    [HttpPost]
    [ProducesResponseType<PortalTicketResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PortalTicketResponse>> Submit(SubmitPortalTicketRequest request, CancellationToken cancellationToken)
    {
        var ticket = await sender.Send(
            new SubmitPortalTicketCommand(User.GetUserId(), request.Subject, request.Description, request.Category), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = ticket.Id }, PortalTicketResponse.From(ticket));
    }

    [EndpointDescription(Scope + " Customer-safe history: ticket creation and status changes only (incl. reopening), oldest first, bounded to the newest 200 events. Tickets created before history tracking show one creation entry with isRecorded = false.")]
    [HttpGet("{id:guid}/timeline")]
    public async Task<ActionResult<IReadOnlyList<PortalTimelineEntryResponse>>> Timeline(Guid id, CancellationToken cancellationToken) =>
        Ok((await sender.Send(new GetPortalTicketTimelineQuery(User.GetUserId(), id), cancellationToken))
            .Select(PortalTimelineEntryResponse.From).ToList());

    [EndpointDescription(Scope + " Creates or updates the single satisfaction rating (1-5, optional comment max 1000) while the ticket is Resolved or Closed (otherwise 409). Never changes the ticket. Concurrent first submissions or overlapping edits: 409.")]
    [HttpPut("{id:guid}/feedback")]
    public async Task<ActionResult<PortalTicketResponse>> SaveFeedback(Guid id, SavePortalFeedbackRequest request, CancellationToken cancellationToken) =>
        Ok(PortalTicketResponse.From(await sender.Send(
            new SavePortalTicketFeedbackCommand(User.GetUserId(), id, request.Rating, request.Comment), cancellationToken)));
}
