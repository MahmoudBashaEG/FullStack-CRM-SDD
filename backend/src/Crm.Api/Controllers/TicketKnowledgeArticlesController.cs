using Crm.Api.Authentication;
using Crm.Api.Contracts.KnowledgeBase;
using Crm.Application.Common;
using Crm.Application.KnowledgeBase.TicketLinks;
using Crm.Domain.Identity;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/tickets/{ticketId:guid}/knowledge-articles")]
[Authorize(Policy = AuthorizationPolicies.Staff)]
public sealed class TicketKnowledgeArticlesController(ISender sender) : ControllerBase
{
    private const string Boundary = "Ticket access: SuperAdmin any ticket; Agent only tickets currently assigned to them (others 404 — collaborator membership does not grant access). Linking never changes the ticket (status, assignment, SLA, timestamps, activity). Nothing is sent to the customer.";

    [EndpointDescription("Requires role: SuperAdmin or Agent with access to the ticket. Linked articles, newest first. Metadata only (never the body); archived articles stay listed with their status. " + Boundary)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TicketKnowledgeLinkResponse>>> List(Guid ticketId, CancellationToken cancellationToken) =>
        Ok((await sender.Send(new ListTicketKnowledgeArticlesQuery(Actor, ticketId), cancellationToken))
            .Select(TicketKnowledgeLinkResponse.From).ToList());

    [EndpointDescription("Requires role: SuperAdmin or Agent with access to the ticket. Published, visible articles only (not published: 409, not visible: 404). Duplicate: 409. At most 25 per ticket (409). " + Boundary)]
    [HttpPost]
    [ProducesResponseType<TicketKnowledgeLinkResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<TicketKnowledgeLinkResponse>> Link(Guid ticketId, LinkKnowledgeArticleRequest request, CancellationToken cancellationToken)
    {
        var link = await sender.Send(new LinkKnowledgeArticleCommand(Actor, ticketId, request.ArticleId), cancellationToken);
        return CreatedAtAction(nameof(List), new { ticketId }, TicketKnowledgeLinkResponse.From(link));
    }

    [EndpointDescription("Requires role: SuperAdmin or Agent with access to the ticket. Works for any article status. Not linked: 404. " + Boundary)]
    [HttpDelete("{articleId:guid}")]
    public async Task<IActionResult> Unlink(Guid ticketId, Guid articleId, CancellationToken cancellationToken)
    {
        await sender.Send(new UnlinkKnowledgeArticleCommand(Actor, ticketId, articleId), cancellationToken);
        return NoContent();
    }

    private StaffActor Actor => new(User.GetUserId(), User.IsInRole(UserRoles.SuperAdmin));
}
