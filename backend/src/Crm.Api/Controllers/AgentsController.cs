using Crm.Api.Authentication;
using Crm.Api.Contracts.Agents;
using Crm.Application.Administration.SetAccountStatus;
using Crm.Application.Agents.CreateAgent;
using Crm.Application.Agents.GetAgent;
using Crm.Application.Agents.ListAgents;
using Crm.Application.Agents.SetAgentPassword;
using Crm.Application.Agents.UpdateAgent;
using Crm.Domain.Identity;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/agents")]
[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
public sealed class AgentsController(ISender sender) : ControllerBase
{
    [EndpointDescription("Requires role: SuperAdmin.")]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AgentResponse>>> List(CancellationToken cancellationToken)
    {
        var agents = await sender.Send(new ListAgentsQuery(), cancellationToken);
        return Ok(agents.Select(AgentResponse.From).ToList());
    }

    [EndpointDescription("Requires role: SuperAdmin.")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AgentResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(AgentResponse.From(await sender.Send(new GetAgentQuery(id), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin.")]
    [HttpPost]
    [ProducesResponseType<AgentResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AgentResponse>> Create(CreateAgentRequest request, CancellationToken cancellationToken)
    {
        var agent = await sender.Send(
            new CreateAgentCommand(User.GetUserId(), request.FirstName, request.LastName, request.Email, request.PhoneNumber, request.Password),
            cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = agent.Id }, AgentResponse.From(agent));
    }

    [EndpointDescription("Requires role: SuperAdmin.")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AgentResponse>> Update(Guid id, UpdateAgentRequest request, CancellationToken cancellationToken)
    {
        var agent = await sender.Send(
            new UpdateAgentCommand(id, request.FirstName, request.LastName, request.PhoneNumber), cancellationToken);
        return Ok(AgentResponse.From(agent));
    }

    [EndpointDescription("Requires role: SuperAdmin. Agent accounts only. Same rules and audit as PUT /api/v1/admin/users/{id}/status.")]
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetStatus(Guid id, SetAgentStatusRequest request, CancellationToken cancellationToken)
    {
        // Same audited command as /admin/users/{id}/status, restricted to Agent accounts (others 404). Response stays 204.
        await sender.Send(new SetAccountStatusCommand(User.GetUserId(), id, request.IsActive, UserRoles.Agent), cancellationToken);
        return NoContent();
    }

    [EndpointDescription("Requires role: SuperAdmin.")]
    [HttpPut("{id:guid}/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetPassword(Guid id, SetAgentPasswordRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new SetAgentPasswordCommand(id, request.NewPassword), cancellationToken);
        return NoContent();
    }
}
