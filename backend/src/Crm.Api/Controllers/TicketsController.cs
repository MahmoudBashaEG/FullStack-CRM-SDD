using Crm.Api.Authentication;
using Crm.Api.Contracts.Common;
using Crm.Api.Contracts.Tickets;
using Crm.Application.Common.Paging;
using Crm.Application.Tickets;
using Crm.Application.Tickets.CreateTicket;
using Crm.Application.Tickets.GetTicket;
using Crm.Application.Tickets.ListTickets;
using Crm.Application.Tickets.UpdateTicket;
using Crm.Domain.Identity;
using Crm.Domain.Tickets;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/tickets")]
[Authorize(Policy = AuthorizationPolicies.Staff)]
public sealed class TicketsController(ISender sender) : ControllerBase
{
    [EndpointDescription("Requires role: SuperAdmin (all tickets) or Agent (only tickets assigned to them). One-based page (default 1), pageSize (default 20, values above 100 are clamped to 100), optional search on subject or exact ticket id, filters customerId/status/category/priority. Newest first.")]
    [HttpGet]
    public async Task<ActionResult<PagedResponse<TicketResponse>>> List(
        [FromQuery] string? search,
        [FromQuery] Guid? customerId,
        [FromQuery] TicketStatus? status,
        [FromQuery] TicketCategory? category,
        [FromQuery] TicketPriority? priority,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new ListTicketsQuery(Actor, search, customerId, status, category, priority, page, pageSize), cancellationToken);
        return Ok(new PagedResponse<TicketResponse>(
            result.Items.Select(TicketResponse.From).ToList(), result.Page, result.PageSize, result.TotalCount));
    }

    [EndpointDescription("Requires role: SuperAdmin (any ticket) or Agent (only tickets assigned to them; others return 404).")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(TicketResponse.From(await sender.Send(new GetTicketQuery(Actor, id), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin or Agent. Status is always Open. Agent: the ticket is assigned to the caller; assignedAgentUserId must be omitted. SuperAdmin: assignedAgentUserId = an active Agent, or omitted/null for unassigned.")]
    [HttpPost]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<TicketResponse>> Create(CreateTicketRequest request, CancellationToken cancellationToken)
    {
        var ticket = await sender.Send(
            new CreateTicketCommand(
                Actor, request.CustomerId, request.Subject, request.Description, request.Category, request.Priority, request.AssignedAgentUserId),
            cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = ticket.Id }, TicketResponse.From(ticket));
    }

    [EndpointDescription("Requires role: SuperAdmin (any ticket) or Agent (only tickets assigned to them). Updates subject, description, category and priority only; customer, creator, assignment and status never change here.")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TicketResponse>> Update(Guid id, UpdateTicketRequest request, CancellationToken cancellationToken)
    {
        var ticket = await sender.Send(
            new UpdateTicketCommand(Actor, id, request.Subject, request.Description, request.Category, request.Priority),
            cancellationToken);
        return Ok(TicketResponse.From(ticket));
    }

    // From the validated JWT only (sub + role claims; RoleClaimType = "role"). Never from the request.
    private TicketActor Actor => new(User.GetUserId(), User.IsInRole(UserRoles.SuperAdmin));
}
