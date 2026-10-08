using Crm.Api.Authorization;
using Crm.Application.Tickets.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

[ApiController, Authorize(Policy = SupportQueuePolicy.Name), Route("api/ticket-reference-data")]
public sealed class TicketReferenceDataController(ISender sender) : ControllerBase
{
    [HttpGet("categories")]
    public async Task<IActionResult> Categories(CancellationToken cancellationToken) => Ok(await sender.Send(new GetCategoriesQuery(), cancellationToken));
    [HttpGet("agents")]
    public async Task<IActionResult> Agents(CancellationToken cancellationToken) => Ok(await sender.Send(new GetEligibleAgentsQuery(), cancellationToken));
}
