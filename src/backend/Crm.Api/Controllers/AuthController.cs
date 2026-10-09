using Crm.Api.Authentication;
using Crm.Api.Authorization;
using Crm.Api.Errors;
using Crm.Application.Authentication.Commands;
using Crm.Application.Authentication.Contracts;
using Crm.Application.Authentication.Queries;
using Crm.Application.Authentication.Ports;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace Crm.Api.Controllers;

public sealed record SignInRequest(string? Email,string? Password);
public sealed record EmptyAuthRequest;
[ApiController,Route("api/auth"),ServiceFilter(typeof(AuthCsrfProtection)),ServiceFilter(typeof(AuthCookieWriter))]
public sealed class AuthController(ISender sender) : ControllerBase
{
    // Antiforgery transport filter supplies this public transport response.
    [HttpGet("csrf"),AllowAnonymous] public IActionResult Csrf()=>Ok();
    [HttpPost("signin"),AllowAnonymous,EnableRateLimiting("signin")]
    public async Task<IActionResult> SignIn(SignInRequest request,CancellationToken ct)
    {
        var result=await sender.Send(new SignInCommand(request.Email,request.Password,Request.Cookies[AuthCookieWriter.RefreshName]),ct);
        if(!result.Succeeded)return ProblemDetailsMapping.Result(result.Error!,HttpContext);
        HttpContext.Items[typeof(SessionIssue)]=result.Value;return Ok(result.Value!.Access);
    }
    [HttpPost("refresh"),AllowAnonymous,EnableRateLimiting("refresh")]
    public async Task<IActionResult> Refresh(EmptyAuthRequest request,CancellationToken ct)
    {
        var result=await sender.Send(new RefreshAuthenticationCommand(Request.Cookies[AuthCookieWriter.RefreshName]),ct);
        if(!result.Succeeded)return ProblemDetailsMapping.Result(result.Error!,HttpContext);
        HttpContext.Items[typeof(SessionIssue)]=result.Value;return Ok(result.Value!.Access);
    }
    [HttpPost("signout"),AllowAnonymous]
    public async Task<IActionResult> SignOut(EmptyAuthRequest request,CancellationToken ct)
    {
        var authentication=HttpContext.Items[typeof(TrustedAuthentication)] as TrustedAuthentication;
        var result=await sender.Send(new SignOutCommand(Request.Cookies[AuthCookieWriter.RefreshName],authentication),ct);
        if(!result.Succeeded)return ProblemDetailsMapping.Result(result.Error!,HttpContext);
        return NoContent();
    }
    [HttpGet("me"),Authorize(Policy=CrmPolicies.AuthenticatedCrmUser)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var identity=await sender.Send(new CurrentIdentityQuery((TrustedAuthentication)HttpContext.Items[typeof(TrustedAuthentication)]!),ct);
        return Ok(new{identity.UserId,identity.DisplayName,role=identity.Role.ToString()});
    }
}
