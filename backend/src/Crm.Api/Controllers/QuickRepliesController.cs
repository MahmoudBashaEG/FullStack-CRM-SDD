using Crm.Api.Authentication;
using Crm.Api.Contracts.Common;
using Crm.Api.Contracts.QuickReplies;
using Crm.Application.Common;
using Crm.Application.Common.Paging;
using Crm.Application.QuickReplies.CreateQuickReply;
using Crm.Application.QuickReplies.GetQuickReply;
using Crm.Application.QuickReplies.ListQuickReplies;
using Crm.Application.QuickReplies.SetQuickReplyArchived;
using Crm.Application.QuickReplies.UpdateQuickReply;
using Crm.Domain.Identity;
using Crm.Domain.QuickReplies;
using Crm.Domain.Tickets;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/quick-replies")]
[Authorize(Policy = AuthorizationPolicies.Staff)]
public sealed class QuickRepliesController(ISender sender) : ControllerBase
{
    private const string Visibility = "Agent: active Shared (read/copy) + own Personal; SuperAdmin: Shared only (Personal templates are never visible). Scope is derived from the caller's role. Copying is a client action; nothing is sent.";

    [EndpointDescription("Requires role: SuperAdmin or Agent. " + Visibility + " One-based page, pageSize (default 20, clamped to 100), search on title or body, category, scope, archived (default false). Ordered by title.")]
    [HttpGet]
    public async Task<ActionResult<PagedResponse<QuickReplyResponse>>> List(
        [FromQuery] string? search,
        [FromQuery] TicketCategory? category,
        [FromQuery] QuickReplyScope? scope,
        [FromQuery] bool archived = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new ListQuickRepliesQuery(Actor, search, category, scope, archived, page, pageSize), cancellationToken);
        return Ok(new PagedResponse<QuickReplyResponse>(
            result.Items.Select(QuickReplyResponse.From).ToList(), result.Page, result.PageSize, result.TotalCount));
    }

    [EndpointDescription("Requires role: SuperAdmin or Agent. " + Visibility + " Anything not visible to the caller: 404.")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<QuickReplyResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(QuickReplyResponse.From(await sender.Send(new GetQuickReplyQuery(Actor, id), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin (creates a Shared template) or Agent (creates a Personal template). The request has no scope or owner field.")]
    [HttpPost]
    [ProducesResponseType<QuickReplyResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<QuickReplyResponse>> Create(CreateQuickReplyRequest request, CancellationToken cancellationToken)
    {
        var template = await sender.Send(new CreateQuickReplyCommand(Actor, request.Title, request.Body, request.Category), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = template.Id }, QuickReplyResponse.From(template));
    }

    [EndpointDescription("Requires role: SuperAdmin (Shared) or Agent (own Personal). An Agent editing a Shared template: 403. Archived: 409. Scope and owner never change.")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<QuickReplyResponse>> Update(Guid id, UpdateQuickReplyRequest request, CancellationToken cancellationToken) =>
        Ok(QuickReplyResponse.From(await sender.Send(
            new UpdateQuickReplyCommand(Actor, id, request.Title, request.Body, request.Category), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin (Shared) or Agent (own Personal). Archived Shared templates disappear for Agents. Repeating is a no-op. No hard delete.")]
    [HttpPost("{id:guid}/archive")]
    public async Task<ActionResult<QuickReplyResponse>> Archive(Guid id, CancellationToken cancellationToken) =>
        Ok(QuickReplyResponse.From(await sender.Send(new SetQuickReplyArchivedCommand(Actor, id, true), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin (Shared) or Agent (own Personal). Restores an archived template. Repeating is a no-op.")]
    [HttpDelete("{id:guid}/archive")]
    public async Task<ActionResult<QuickReplyResponse>> Restore(Guid id, CancellationToken cancellationToken) =>
        Ok(QuickReplyResponse.From(await sender.Send(new SetQuickReplyArchivedCommand(Actor, id, false), cancellationToken)));

    private StaffActor Actor => new(User.GetUserId(), User.IsInRole(UserRoles.SuperAdmin));
}
