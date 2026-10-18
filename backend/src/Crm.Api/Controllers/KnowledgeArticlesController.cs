using Crm.Api.Authentication;
using Crm.Api.Contracts.Common;
using Crm.Api.Contracts.KnowledgeBase;
using Crm.Application.Common;
using Crm.Application.Common.Paging;
using Crm.Application.KnowledgeBase.ChangeArticleStatus;
using Crm.Application.KnowledgeBase.CreateArticle;
using Crm.Application.KnowledgeBase.GetArticle;
using Crm.Application.KnowledgeBase.ListArticles;
using Crm.Application.KnowledgeBase.UpdateArticle;
using Crm.Domain.Identity;
using Crm.Domain.KnowledgeBase;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/knowledge-base/articles")]
[Authorize(Policy = AuthorizationPolicies.Staff)]
public sealed class KnowledgeArticlesController(ISender sender) : ControllerBase
{
    private const string Visibility = "Staff only — customers read Published + Public articles through /api/v1/portal/help-center; no anonymous access. Agent: Published (Internal + Public) and own Draft/Ready for Publication; SuperAdmin: all including Archived. Not visible: 404.";

    [EndpointDescription("Requires role: SuperAdmin or Agent. " + Visibility + " One-based page, pageSize (default 20, clamped to 100), search on title/summary/body, type, categoryId, status, mine (own articles). Ordered by last update, newest first. Rows omit the body.")]
    [HttpGet]
    public async Task<ActionResult<PagedResponse<KnowledgeArticleSummaryResponse>>> List(
        [FromQuery] string? search,
        [FromQuery] KnowledgeArticleType? type,
        [FromQuery] Guid? categoryId,
        [FromQuery] KnowledgeArticleStatus? status,
        [FromQuery] bool mine = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new ListKnowledgeArticlesQuery(Actor, search, type, categoryId, status, mine, page, pageSize), cancellationToken);
        return Ok(new PagedResponse<KnowledgeArticleSummaryResponse>(
            result.Items.Select(KnowledgeArticleSummaryResponse.From).ToList(), result.Page, result.PageSize, result.TotalCount));
    }

    [EndpointDescription("Requires role: SuperAdmin or Agent. " + Visibility)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<KnowledgeArticleResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(KnowledgeArticleResponse.From(await sender.Send(new GetKnowledgeArticleQuery(Actor, id), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin or Agent. Always created as Draft owned by the caller; the request has no status or author field. The category must be active (400).")]
    [HttpPost]
    [ProducesResponseType<KnowledgeArticleResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<KnowledgeArticleResponse>> Create(SaveKnowledgeArticleRequest request, CancellationToken cancellationToken)
    {
        var article = await sender.Send(
            new CreateKnowledgeArticleCommand(Actor, request.Title, request.Summary, request.Body, request.Type, request.CategoryId, request.Visibility),
            cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = article.Id }, KnowledgeArticleResponse.From(article));
    }

    [EndpointDescription("Requires role: SuperAdmin or Agent. Agent: own Draft only (own submitted article: 409, Published: 403). SuperAdmin: Draft, Ready for Publication, Published (status unchanged). Archived: 409. Status never changes here.")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<KnowledgeArticleResponse>> Update(Guid id, SaveKnowledgeArticleRequest request, CancellationToken cancellationToken) =>
        Ok(KnowledgeArticleResponse.From(await sender.Send(
            new UpdateKnowledgeArticleCommand(Actor, id, request.Title, request.Summary, request.Body, request.Type, request.CategoryId, request.Visibility),
            cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin or Agent. Draft → Ready for Publication. Agent: own Draft; SuperAdmin: any Draft. Other status: 409.")]
    [HttpPost("{id:guid}/submit")]
    public Task<ActionResult<KnowledgeArticleResponse>> Submit(Guid id, CancellationToken cancellationToken) =>
        Change(id, KnowledgeArticleAction.Submit, null, cancellationToken);

    [EndpointDescription("Requires role: SuperAdmin (Agents: 403). Ready for Publication → Draft with an optional reason (max 500).")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    [HttpPost("{id:guid}/return")]
    public Task<ActionResult<KnowledgeArticleResponse>> Return(Guid id, ReturnKnowledgeArticleRequest? request, CancellationToken cancellationToken) =>
        Change(id, KnowledgeArticleAction.Return, request?.Reason, cancellationToken);

    [EndpointDescription("Requires role: SuperAdmin (Agents: 403). Ready for Publication → Published; records publisher and time. Other status: 409.")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    [HttpPost("{id:guid}/publish")]
    public Task<ActionResult<KnowledgeArticleResponse>> Publish(Guid id, CancellationToken cancellationToken) =>
        Change(id, KnowledgeArticleAction.Publish, null, cancellationToken);

    [EndpointDescription("Requires role: SuperAdmin (Agents: 403). Draft/Ready for Publication/Published → Archived. Removed from search; existing ticket links are kept. No hard delete.")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    [HttpPost("{id:guid}/archive")]
    public Task<ActionResult<KnowledgeArticleResponse>> Archive(Guid id, CancellationToken cancellationToken) =>
        Change(id, KnowledgeArticleAction.Archive, null, cancellationToken);

    [EndpointDescription("Requires role: SuperAdmin (Agents: 403). Archived → Draft (submit and publish again after review). Same article id.")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    [HttpPost("{id:guid}/restore")]
    public Task<ActionResult<KnowledgeArticleResponse>> Restore(Guid id, CancellationToken cancellationToken) =>
        Change(id, KnowledgeArticleAction.Restore, null, cancellationToken);

    private async Task<ActionResult<KnowledgeArticleResponse>> Change(
        Guid id, KnowledgeArticleAction action, string? reason, CancellationToken cancellationToken) =>
        Ok(KnowledgeArticleResponse.From(await sender.Send(new ChangeKnowledgeArticleStatusCommand(Actor, id, action, reason), cancellationToken)));

    private StaffActor Actor => new(User.GetUserId(), User.IsInRole(UserRoles.SuperAdmin));
}
