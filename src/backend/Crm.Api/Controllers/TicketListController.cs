using Crm.Api.Authorization;
using Crm.Api.Binding;
using Crm.Api.Errors;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Crm.Api.Controllers;

[ApiController, Authorize(Policy = SupportQueuePolicy.Name), Route("api/tickets")]
public sealed class TicketListController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!TicketListRequest.TryBind(Request.Query, out var query)) return ProblemDetailsMapping.Result(new("validation_failed", 400), HttpContext);
        var result = await sender.Send(query, cancellationToken);
        return result.Error is not null ? ProblemDetailsMapping.Result(result.Error, HttpContext) : Ok(result.Value);
    }
}
