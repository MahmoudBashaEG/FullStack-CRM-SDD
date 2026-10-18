using Crm.Api.Authentication;
using Crm.Api.Contracts.Ai;
using Crm.Application.Ai.SuggestCategory;
using Crm.Application.Ai.SuggestReply;
using Crm.Application.Ai.SuggestSolutions;
using Crm.Application.Ai.SummarizeTicket;
using Crm.Application.Common;
using Crm.Domain.Identity;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/tickets/{ticketId:guid}/ai")]
[Authorize(Policy = AuthorizationPolicies.Staff)]
[EnableRateLimiting(RateLimitPolicies.Ai)]
public sealed class TicketAiController(ISender sender) : ControllerBase
{
    private const string Boundary = "Ticket access re-checked on every call: SuperAdmin any ticket; Agent only tickets currently assigned to them (others 404 — collaborator membership does not grant access). Context is rebuilt on the server; nothing is stored, nothing is sent to the customer, the ticket is never changed. AI errors: 429 (aiError UsageLimit | QuotaExceeded), 502, 503, 504, 422 ProblemDetails.";

    [EndpointDescription("Requires role: SuperAdmin or Agent with access to the ticket. On-demand summary of the ticket and its recent activity (plain text). " + Boundary)]
    [HttpPost("summary")]
    public async Task<ActionResult<AiTextResponse>> Summary(Guid ticketId, CancellationToken cancellationToken) =>
        Ok(AiTextResponse.From(await sender.Send(new SummarizeTicketQuery(Actor, ticketId), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin or Agent with access to the ticket. Customer-reply DRAFT in the chosen tone (Professional | Friendly | Empathetic | Concise) with an optional staff instruction (max 500). Not sent; does not record a first response. " + Boundary)]
    [HttpPost("reply-draft")]
    public async Task<ActionResult<AiTextResponse>> ReplyDraft(Guid ticketId, SuggestReplyRequest request, CancellationToken cancellationToken) =>
        Ok(AiTextResponse.From(await sender.Send(new SuggestTicketReplyQuery(Actor, ticketId, request.Tone, request.Instructions), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin or Agent with access to the ticket. Recommends one existing ticket category with an explanation. Never applies it — use PUT /api/v1/tickets/{id}/category. " + Boundary)]
    [HttpPost("category-suggestion")]
    public async Task<ActionResult<AiCategorySuggestionResponse>> CategorySuggestion(Guid ticketId, CancellationToken cancellationToken) =>
        Ok(AiCategorySuggestionResponse.From(await sender.Send(new SuggestTicketCategoryQuery(Actor, ticketId), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin or Agent with access to the ticket. Troubleshooting suggestions: Knowledge Base–supported steps (only real Published article ids/titles) and separately labelled general suggestions not verified against the Knowledge Base. " + Boundary)]
    [HttpPost("solutions")]
    public async Task<ActionResult<AiSolutionsResponse>> Solutions(Guid ticketId, CancellationToken cancellationToken) =>
        Ok(AiSolutionsResponse.From(await sender.Send(new SuggestTicketSolutionsQuery(Actor, ticketId), cancellationToken)));

    private StaffActor Actor => new(User.GetUserId(), User.IsInRole(UserRoles.SuperAdmin));
}
