using Crm.Api.Authentication;
using Crm.Api.Contracts.Common;
using Crm.Api.Contracts.Tickets;
using Crm.Application.Common.Paging;
using Crm.Application.Tickets;
using Crm.Application.Tickets.ChangeAssignee;
using Crm.Application.Tickets.ChangeCategory;
using Crm.Application.Tickets.ClearTicketEscalation;
using Crm.Application.Tickets.CreateTicket;
using Crm.Application.Tickets.EscalateTicket;
using Crm.Application.Tickets.GetTicket;
using Crm.Application.Tickets.ListTicketActivity;
using Crm.Application.Tickets.ListTickets;
using Crm.Application.Tickets.RecordFirstResponse;
using Crm.Application.Tickets.TransitionTicket;
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
    [EndpointDescription("Requires role: SuperAdmin (all tickets) or Agent (only tickets assigned to them). One-based page (default 1), pageSize (default 20, values above 100 are clamped to 100), optional search on subject or exact ticket id, filters customerId/status/category/priority/escalated (true/false)/slaBreached (true = at least one breached SLA target, false = none). Newest first.")]
    [HttpGet]
    public async Task<ActionResult<PagedResponse<TicketResponse>>> List(
        [FromQuery] string? search,
        [FromQuery] Guid? customerId,
        [FromQuery] TicketStatus? status,
        [FromQuery] TicketCategory? category,
        [FromQuery] TicketPriority? priority,
        [FromQuery] bool? escalated,
        [FromQuery] bool? slaBreached,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new ListTicketsQuery(Actor, search, customerId, status, category, priority, escalated, slaBreached, page, pageSize), cancellationToken);
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

    [EndpointDescription("Requires role: SuperAdmin (any ticket) or Agent (only tickets assigned to them). Updates subject, description, category and priority only; each changed field is recorded in the ticket activity. Customer, creator, assignment, status and escalation never change here.")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TicketResponse>> Update(Guid id, UpdateTicketRequest request, CancellationToken cancellationToken)
    {
        var ticket = await sender.Send(
            new UpdateTicketCommand(Actor, id, request.Subject, request.Description, request.Category, request.Priority),
            cancellationToken);
        return Ok(TicketResponse.From(ticket));
    }

    [EndpointDescription("Requires role: SuperAdmin (any ticket) or Agent (only tickets assigned to them; others 404). Changes the category only — e.g. to apply an AI suggestion after review. expectedCategory must equal the current category (409 otherwise). Same value = no change. Recorded in the ticket activity. Subject, description, priority, status, assignment and SLA never change here.")]
    [HttpPut("{id:guid}/category")]
    public async Task<ActionResult<TicketResponse>> ChangeCategory(Guid id, ChangeTicketCategoryRequest request, CancellationToken cancellationToken) =>
        Ok(TicketResponse.From(await sender.Send(new ChangeTicketCategoryCommand(Actor, id, request.ExpectedCategory, request.Category), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin. Assigns, reassigns or unassigns (assignedAgentUserId = null) an existing ticket. The assignee must be an active Agent (400 otherwise). Same assignee = no change. Recorded in the ticket activity.")]
    [HttpPut("{id:guid}/assignee")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    public async Task<ActionResult<TicketResponse>> ChangeAssignee(Guid id, ChangeTicketAssigneeRequest request, CancellationToken cancellationToken) =>
        Ok(TicketResponse.From(await sender.Send(new ChangeTicketAssigneeCommand(Actor, id, request.AssignedAgentUserId), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin (any ticket) or Agent (only tickets assigned to them; others 404). Allowed: Open→InProgress, InProgress→Resolved, Resolved→Closed, Resolved→InProgress, Closed→Open. fromStatus must equal the current status (409 otherwise); other transitions 409. Resolving clears an active escalation.")]
    [HttpPost("{id:guid}/transitions")]
    public async Task<ActionResult<TicketResponse>> Transition(Guid id, TransitionTicketRequest request, CancellationToken cancellationToken) =>
        Ok(TicketResponse.From(await sender.Send(new TransitionTicketStatusCommand(Actor, id, request.FromStatus, request.ToStatus), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin (any ticket) or Agent (only tickets assigned to them; others 404). Marks an Open or InProgress ticket as escalated with a required reason (max 500). Already escalated or Resolved/Closed: 409.")]
    [HttpPost("{id:guid}/escalation")]
    public async Task<ActionResult<TicketResponse>> Escalate(Guid id, EscalateTicketRequest request, CancellationToken cancellationToken) =>
        Ok(TicketResponse.From(await sender.Send(new EscalateTicketCommand(Actor, id, request.Reason), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin (any ticket) or Agent (only tickets assigned to them; others 404). Records the first customer response for SLA (server time, caller as actor) on an Open/In Progress SLA-tracked ticket. Internal tracking only — nothing is sent to the customer. Already recorded or not SLA-tracked: 409.")]
    [HttpPost("{id:guid}/first-response")]
    public async Task<ActionResult<TicketResponse>> RecordFirstResponse(Guid id, CancellationToken cancellationToken) =>
        Ok(TicketResponse.From(await sender.Send(new RecordFirstResponseCommand(Actor, id), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin. Clears an active escalation (409 if not escalated). Recorded in the ticket activity.")]
    [HttpDelete("{id:guid}/escalation")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    public async Task<ActionResult<TicketResponse>> ClearEscalation(Guid id, CancellationToken cancellationToken) =>
        Ok(TicketResponse.From(await sender.Send(new ClearTicketEscalationCommand(Actor, id), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin (any ticket) or Agent (only tickets currently assigned to them; others 404). Read-only, append-only history, newest first. One-based page (default 1), pageSize (default 20, clamped to 100).")]
    [HttpGet("{id:guid}/activity")]
    public async Task<ActionResult<PagedResponse<TicketActivityResponse>>> Activity(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = Paging.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new ListTicketActivityQuery(Actor, id, page, pageSize), cancellationToken);
        return Ok(new PagedResponse<TicketActivityResponse>(
            result.Items.Select(TicketActivityResponse.From).ToList(), result.Page, result.PageSize, result.TotalCount));
    }

    // From the validated JWT only (sub + role claims; RoleClaimType = "role"). Never from the request.
    private TicketActor Actor => new(User.GetUserId(), User.IsInRole(UserRoles.SuperAdmin));
}
