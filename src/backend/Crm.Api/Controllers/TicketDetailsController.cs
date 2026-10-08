using Crm.Api.Authorization;
using Crm.Api.Binding;
using Crm.Api.Errors;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Crm.Api.Controllers;

[ApiController, Authorize(Policy = SupportQueuePolicy.Name), Route("api/tickets/{reference}/details")]
public sealed class TicketDetailsController(ISender sender) : ControllerBase
{
    [HttpPatch, Consumes("application/json")]
    public async Task<IActionResult> Update(string reference, TicketDetailsRequest request, CancellationToken token)
    {
        Response.Headers["Accept-Patch"] = "application/json";
        var failure = request.Bind(reference, out var command); if (failure is not null) return ProblemDetailsMapping.Result(failure, HttpContext);
        var result = await sender.Send(command, token); return result.Error is not null ? ProblemDetailsMapping.Result(result.Error, HttpContext) : Ok(result.Value);
    }
}
