using Crm.Api.Authentication;
using Crm.Api.Contracts.KnowledgeBase;
using Crm.Application.Common;
using Crm.Application.KnowledgeBase.CreateCategory;
using Crm.Application.KnowledgeBase.ListCategories;
using Crm.Application.KnowledgeBase.UpdateCategory;
using Crm.Domain.Identity;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/knowledge-base/categories")]
[Authorize(Policy = AuthorizationPolicies.Staff)]
public sealed class KnowledgeCategoriesController(ISender sender) : ControllerBase
{
    [EndpointDescription("Requires role: SuperAdmin or Agent. Unpaged, ordered by name. Active categories only; includeInactive is honoured for SuperAdmin only.")]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<KnowledgeCategoryResponse>>> List(
        [FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default) =>
        Ok((await sender.Send(new ListKnowledgeCategoriesQuery(Actor, includeInactive), cancellationToken))
            .Select(KnowledgeCategoryResponse.From).ToList());

    [EndpointDescription("Requires role: SuperAdmin (Agents: 403). Name 1-100 characters, unique ignoring case (409).")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    [HttpPost]
    [ProducesResponseType<KnowledgeCategoryResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<KnowledgeCategoryResponse>> Create(CreateKnowledgeCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await sender.Send(new CreateKnowledgeCategoryCommand(Actor, request.Name), cancellationToken);
        return CreatedAtAction(nameof(List), null, KnowledgeCategoryResponse.From(category));
    }

    [EndpointDescription("Requires role: SuperAdmin (Agents: 403). Rename and/or deactivate/reactivate. Deactivated categories stay on existing articles but cannot be chosen for new or re-categorized ones. Duplicate name: 409. No hard delete.")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<KnowledgeCategoryResponse>> Update(Guid id, UpdateKnowledgeCategoryRequest request, CancellationToken cancellationToken) =>
        Ok(KnowledgeCategoryResponse.From(await sender.Send(
            new UpdateKnowledgeCategoryCommand(Actor, id, request.Name, request.IsActive), cancellationToken)));

    private StaffActor Actor => new(User.GetUserId(), User.IsInRole(UserRoles.SuperAdmin));
}
