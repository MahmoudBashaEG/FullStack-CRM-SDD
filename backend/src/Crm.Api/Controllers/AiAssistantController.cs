using Crm.Api.Authentication;
using Crm.Api.Contracts.Ai;
using Crm.Application.Ai;
using Crm.Application.Ai.Chat;
using Crm.Application.Common;
using Crm.Domain.Identity;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/ai")]
[Authorize(Policy = AuthorizationPolicies.Staff)]
[EnableRateLimiting(RateLimitPolicies.Ai)]
public sealed class AiAssistantController(ISender sender) : ControllerBase
{
    [EndpointDescription("Requires role: SuperAdmin or Agent. Staff AI assistant (advisory, no tools, cannot change data or send messages). Send the capped plain-text conversation (oldest first, last = User; max 50 messages, 4000 chars each; the server keeps the newest N). Optional ticketId adds that ticket's context; access is re-checked on every message (404 when lost). Published Knowledge Base excerpts may be used; sources list only real article ids. Nothing is stored. AI errors: 429 (aiError UsageLimit | QuotaExceeded), 502, 503, 504, 422 ProblemDetails.")]
    [HttpPost("chat")]
    public async Task<ActionResult<AiChatResponse>> Chat(AiChatRequest request, CancellationToken cancellationToken)
    {
        // A missing role maps to an undefined enum value so the validator answers 400 (messages[i].role).
        var messages = (request.Messages ?? [])
            .Select(m => new AiChatTurn(m.Role ?? (AiChatRole)(-1), m.Text ?? ""))
            .ToList();
        var actor = new StaffActor(User.GetUserId(), User.IsInRole(UserRoles.SuperAdmin));
        return Ok(AiChatResponse.From(await sender.Send(new AiChatQuery(actor, request.TicketId, messages), cancellationToken)));
    }
}
