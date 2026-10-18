using Crm.Api.Authentication;
using Crm.Api.Contracts.Collaboration;
using Crm.Api.Contracts.Common;
using Crm.Application.Collaboration.GetCollaborationTicket;
using Crm.Application.Collaboration.InviteCollaborator;
using Crm.Application.Collaboration.ListCollaboratorCandidates;
using Crm.Application.Collaboration.ListMyCollaborations;
using Crm.Application.Collaboration.ListTicketCollaborators;
using Crm.Application.Collaboration.ListTicketMessages;
using Crm.Application.Collaboration.PostTicketMessage;
using Crm.Application.Collaboration.RemoveCollaborator;
using Crm.Application.Common;
using Crm.Application.Common.Paging;
using Crm.Domain.Identity;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/collaborations")]
[Authorize(Policy = AuthorizationPolicies.Staff)]
public sealed class CollaborationsController(ISender sender) : ControllerBase
{
    private const string Boundary = " Internal staff discussion — never visible to customers. Access: SuperAdmin; the ticket's current assigned Agent; or an invited active collaborator while the ticket is assigned. Does not grant /api/v1/tickets access.";

    [EndpointDescription("Requires role: Agent (SuperAdmin and Customer: 403). Tickets the caller is an active collaborator on, newest invitation first. isSuspended = the ticket is currently unassigned." + Boundary)]
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.Agent)]
    public async Task<ActionResult<PagedResponse<MyCollaborationResponse>>> Mine(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new ListMyCollaborationsQuery(User.GetUserId(), page, pageSize), cancellationToken);
        return Ok(new PagedResponse<MyCollaborationResponse>(
            result.Items.Select(MyCollaborationResponse.From).ToList(), result.Page, result.PageSize, result.TotalCount));
    }

    [EndpointDescription("Requires role: SuperAdmin or Agent. Minimal ticket context (subject, status, priority, assigned Agent name) and the caller's access level; 404 without access." + Boundary)]
    [HttpGet("{ticketId:guid}")]
    public async Task<ActionResult<CollaborationTicketResponse>> Get(Guid ticketId, CancellationToken cancellationToken) =>
        Ok(CollaborationTicketResponse.From(await sender.Send(new GetCollaborationTicketQuery(Actor, ticketId), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin or Agent with discussion access (else 404). Append-only messages, newest first. One-based page (default 1), pageSize (default 20, clamped to 100)." + Boundary)]
    [HttpGet("{ticketId:guid}/messages")]
    public async Task<ActionResult<PagedResponse<TicketMessageResponse>>> Messages(
        Guid ticketId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new ListTicketMessagesQuery(Actor, ticketId, page, pageSize), cancellationToken);
        return Ok(new PagedResponse<TicketMessageResponse>(
            result.Items.Select(TicketMessageResponse.From).ToList(), result.Page, result.PageSize, result.TotalCount));
    }

    [EndpointDescription("Requires role: SuperAdmin or Agent with discussion access (else 404). Plain text, 1-2000 characters. Messages are never edited or deleted and never change the ticket (status, timestamps or Ticket Activity)." + Boundary)]
    [HttpPost("{ticketId:guid}/messages")]
    [ProducesResponseType<TicketMessageResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<TicketMessageResponse>> Post(Guid ticketId, PostTicketMessageRequest request, CancellationToken cancellationToken)
    {
        var message = await sender.Send(new PostTicketMessageCommand(Actor, ticketId, request.Body), cancellationToken);
        return CreatedAtAction(nameof(Messages), new { ticketId }, TicketMessageResponse.From(message));
    }

    [EndpointDescription("Requires role: SuperAdmin or Agent with discussion access (else 404). Active collaborators." + Boundary)]
    [HttpGet("{ticketId:guid}/collaborators")]
    public async Task<ActionResult<IReadOnlyList<TicketCollaboratorResponse>>> Collaborators(Guid ticketId, CancellationToken cancellationToken) =>
        Ok((await sender.Send(new ListTicketCollaboratorsQuery(Actor, ticketId), cancellationToken)).Select(TicketCollaboratorResponse.From).ToList());

    [EndpointDescription("Requires role: SuperAdmin or the ticket's assigned Agent (collaborator-only: 403). Active Agents who can be invited (excludes the assigned Agent, the caller and current collaborators)." + Boundary)]
    [HttpGet("{ticketId:guid}/eligible-agents")]
    public async Task<ActionResult<IReadOnlyList<CollaboratorCandidateResponse>>> EligibleAgents(Guid ticketId, CancellationToken cancellationToken) =>
        Ok((await sender.Send(new ListCollaboratorCandidatesQuery(Actor, ticketId), cancellationToken)).Select(CollaboratorCandidateResponse.From).ToList());

    [EndpointDescription("Requires role: SuperAdmin (any ticket) or the ticket's assigned Agent (own ticket only; collaborator-only: 403; no access: 404). The invitee must be an active Agent other than the caller and the assigned Agent (400); already a collaborator: 409. Grants discussion access only." + Boundary)]
    [HttpPost("{ticketId:guid}/collaborators")]
    [ProducesResponseType<TicketCollaboratorResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<TicketCollaboratorResponse>> Invite(Guid ticketId, InviteCollaboratorRequest request, CancellationToken cancellationToken)
    {
        var collaborator = await sender.Send(new InviteCollaboratorCommand(Actor, ticketId, request.AgentUserId), cancellationToken);
        return CreatedAtAction(nameof(Collaborators), new { ticketId }, TicketCollaboratorResponse.From(collaborator));
    }

    [EndpointDescription("Requires role: SuperAdmin (any ticket) or the ticket's assigned Agent (own ticket only; collaborator-only: 403). Access ends immediately; the membership history and all messages are kept." + Boundary)]
    [HttpDelete("{ticketId:guid}/collaborators/{agentUserId:guid}")]
    public async Task<IActionResult> Remove(Guid ticketId, Guid agentUserId, CancellationToken cancellationToken)
    {
        await sender.Send(new RemoveCollaboratorCommand(Actor, ticketId, agentUserId), cancellationToken);
        return NoContent();
    }

    private StaffActor Actor => new(User.GetUserId(), User.IsInRole(UserRoles.SuperAdmin));
}
