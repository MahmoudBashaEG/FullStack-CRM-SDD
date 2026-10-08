using Crm.Api.Authorization;
using Crm.Api.Errors;
using Crm.Application.Tickets.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Crm.Api.Controllers;

[ApiController, Authorize(Policy = SupportQueuePolicy.Name), Route("api/tickets/{reference}/history")]
public sealed class TicketHistoryController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(string reference, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new GetTicketHistoryQuery(reference, page, pageSize), cancellationToken);
        return result.Error is not null ? ProblemDetailsMapping.Result(result.Error, HttpContext) : Ok(result.Value);
    }
}
