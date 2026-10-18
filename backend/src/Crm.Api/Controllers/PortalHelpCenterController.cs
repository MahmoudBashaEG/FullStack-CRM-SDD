using Crm.Api.Authentication;
using Crm.Api.Contracts.Common;
using Crm.Api.Contracts.Portal;
using Crm.Application.Common.Paging;
using Crm.Application.Portal.GetHelpCenterArticle;
using Crm.Application.Portal.ListHelpCenterArticles;
using Crm.Application.Portal.ListHelpCenterCategories;
using Crm.Domain.KnowledgeBase;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/portal/help-center")]
[Authorize(Policy = AuthorizationPolicies.Customer)]
public sealed class PortalHelpCenterController(ISender sender) : ControllerBase
{
    private const string Visibility = "Requires role: Customer (linked profile). Published + Public articles only — Internal, Draft, Ready for Publication and Archived articles return 404 / are never listed. No anonymous access.";

    [EndpointDescription(Visibility + " One-based page, pageSize (default 20, clamped to 100), search on title/summary/body, type, categoryId. Newest published first. Rows omit the body.")]
    [HttpGet("articles")]
    public async Task<ActionResult<PagedResponse<HelpCenterArticleSummaryResponse>>> List(
        [FromQuery] string? search, [FromQuery] KnowledgeArticleType? type, [FromQuery] Guid? categoryId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = Paging.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new ListHelpCenterArticlesQuery(User.GetUserId(), search, type, categoryId, page, pageSize), cancellationToken);
        return Ok(new PagedResponse<HelpCenterArticleSummaryResponse>(
            result.Items.Select(HelpCenterArticleSummaryResponse.From).ToList(), result.Page, result.PageSize, result.TotalCount));
    }

    [EndpointDescription(Visibility)]
    [HttpGet("articles/{id:guid}")]
    public async Task<ActionResult<HelpCenterArticleResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(HelpCenterArticleResponse.From(await sender.Send(new GetHelpCenterArticleQuery(User.GetUserId(), id), cancellationToken)));

    [EndpointDescription(Visibility + " Only categories that contain at least one such article; id and name only. Ordered by name.")]
    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<HelpCenterCategoryResponse>>> Categories(CancellationToken cancellationToken) =>
        Ok((await sender.Send(new ListHelpCenterCategoriesQuery(User.GetUserId()), cancellationToken))
            .Select(HelpCenterCategoryResponse.From).ToList());
}
