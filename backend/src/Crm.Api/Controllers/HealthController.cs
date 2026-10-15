using Crm.Api.Contracts.Health;
using Crm.Application.Diagnostics.GetSystemStatus;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Health)]
public sealed class HealthController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken cancellationToken)
    {
        var status = await sender.Send(new GetSystemStatusQuery(), cancellationToken);
        return Ok(new HealthResponse(status.Status, "Crm.Api", status.CheckedAtUtc));
    }
}
