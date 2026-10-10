using System.Globalization;
using Crm.Api.Authorization;
using Crm.Api.Errors;
using Crm.Application.Accounts.Commands;
using Crm.Application.Accounts.Contracts;
using Crm.Application.Accounts.Queries;
using Crm.Application.Authentication.Contracts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Crm.Api.Controllers;
public sealed record CreateAgentRequest(string? DisplayName,string? Email,string? Password);
public sealed record RenameAgentRequest(string? DisplayName,string? Version);
public sealed record AgentAvailabilityRequest(bool? IsActive,string? Version);
[ApiController,Route("api/agents"),Authorize(Policy=CrmPolicies.AdminAgents)]
public sealed class AgentsController(ISender sender):ControllerBase
{
    private AccountIdentity Actor=>((TrustedAuthentication)HttpContext.Items[typeof(TrustedAuthentication)]!).Identity;
    private IActionResult Invalid()=>ProblemDetailsMapping.Result(new("validation_failed",400),HttpContext);
    private bool NoQuery()=>Request.Query.Count==0;
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct)
    {
        if(Request.Query.Any(x=>x.Key is not ("page" or "pageSize")||x.Value.Count!=1))return Invalid();
        var page=1;var size=25;
        if(Request.Query.TryGetValue("page",out var p)&&!int.TryParse(p.ToString(),NumberStyles.None,CultureInfo.InvariantCulture,out page))return Invalid();
        if(Request.Query.TryGetValue("pageSize",out var s)&&!int.TryParse(s.ToString(),NumberStyles.None,CultureInfo.InvariantCulture,out size))return Invalid();
        var result=await sender.Send(new ListAgentsQuery(Actor,page,size),ct);return result.Succeeded?Ok(result.Value):ProblemDetailsMapping.Result(result.Error!,HttpContext);
    }
    [HttpPost] public async Task<IActionResult> Create(CreateAgentRequest request,CancellationToken ct)
    {
        if(!NoQuery())return Invalid();var result=await sender.Send(new CreateAgentCommand(Actor,request.DisplayName,request.Email,request.Password),ct);
        return result.Succeeded?Created($"/api/agents/{result.Value!.UserId:D}",result.Value):ProblemDetailsMapping.Result(result.Error!,HttpContext);
    }
    [HttpGet("{userId}")] public async Task<IActionResult> Get(string userId,CancellationToken ct)
    {
        if(!NoQuery()||!Guid.TryParseExact(userId,"D",out var id))return Invalid();var result=await sender.Send(new GetAgentQuery(Actor,id),ct);return result.Succeeded?Ok(result.Value):ProblemDetailsMapping.Result(result.Error!,HttpContext);
    }
    [HttpPatch("{userId}")] public async Task<IActionResult> Rename(string userId,RenameAgentRequest request,CancellationToken ct)
    {
        if(!NoQuery()||!Guid.TryParseExact(userId,"D",out var id))return Invalid();var result=await sender.Send(new RenameAgentCommand(Actor,id,request.DisplayName,request.Version),ct);return result.Succeeded?Ok(result.Value):ProblemDetailsMapping.Result(result.Error!,HttpContext);
    }
    [HttpPut("{userId}/availability")] public async Task<IActionResult> Availability(string userId,AgentAvailabilityRequest request,CancellationToken ct)
    {
        if(!NoQuery()||!Guid.TryParseExact(userId,"D",out var id)||!request.IsActive.HasValue)return Invalid();var result=await sender.Send(new SetAgentAvailabilityCommand(Actor,id,request.IsActive.Value,request.Version),ct);return result.Succeeded?Ok(result.Value):ProblemDetailsMapping.Result(result.Error!,HttpContext);
    }
}
