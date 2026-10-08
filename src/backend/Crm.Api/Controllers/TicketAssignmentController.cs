using Crm.Api.Authorization;
using Crm.Api.Binding;
using Crm.Api.Errors;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Crm.Api.Controllers;

[ApiController, Authorize(Policy = SupportQueuePolicy.Name), Route("api/tickets/{reference}/assignment")]
public sealed class TicketAssignmentController(ISender sender) : ControllerBase
{
    [HttpPut, Consumes("application/json")]
    public async Task<IActionResult> Assign(string reference, TicketAssignmentRequest request, CancellationToken token)
    {
        var failure = request.Bind(reference, out var command); if (failure is not null) return ProblemDetailsMapping.Result(failure, HttpContext);
        var result = await sender.Send(command, token); return result.Error is not null ? ProblemDetailsMapping.Result(result.Error, HttpContext) : Ok(result.Value);
    }
}
