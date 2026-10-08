using Crm.Api.Authorization;
using Crm.Api.Binding;
using Crm.Api.Errors;
using Crm.Application.Common;
using Crm.Application.Tickets.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Crm.Api.Controllers;

[ApiController, Authorize(Policy = SupportQueuePolicy.Name), Route("api/tickets")]
public sealed class TicketsController(ISender sender) : ControllerBase
{
    [HttpPost, Consumes("application/json")]
    public async Task<IActionResult> Create(CreateTicketRequest request, CancellationToken cancellationToken)
    {
        if (!request.TryBind(out var command)) return ProblemDetailsMapping.Result(new Failure("validation_failed", 400), HttpContext);
        var result = await sender.Send(command, cancellationToken);
        return result.Error is not null ? ProblemDetailsMapping.Result(result.Error, HttpContext) : CreatedAtAction(nameof(Get), new { reference = result.Value!.Reference }, result.Value);
    }
    [HttpGet("{reference}")]
    public async Task<IActionResult> Get(string reference, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTicketQuery(reference), cancellationToken);
        return result.Error is not null ? ProblemDetailsMapping.Result(result.Error, HttpContext) : Ok(result.Value);
    }
}
