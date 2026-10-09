using Crm.Application.Authentication.Ports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace Crm.Api.Authentication;

public sealed class AuthCookieWriter(TimeProvider clock) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context,ResultExecutionDelegate next)
    {
        if(context.HttpContext.Items[typeof(SessionIssue)] is SessionIssue issue)Write(context.HttpContext.Response,issue.RefreshCredential,issue.SessionExpiresAtUtc);
        else if(context.HttpContext.Request.Path=="/api/auth/signout"&&context.Result is NoContentResult
            ||context.HttpContext.Request.Path=="/api/auth/refresh"&&context.Result is ObjectResult {StatusCode:401})Clear(context.HttpContext.Response);
        await next();
    }
    public const string RefreshName="__Host-crm-refresh";
    private static CookieOptions Options()=>new(){Secure=true,HttpOnly=true,SameSite=SameSiteMode.Strict,Path="/",IsEssential=true};
    public void Write(HttpResponse response,string credential,DateTimeOffset expires)
    {var options=Options();options.Expires=expires;options.MaxAge=expires-clock.GetUtcNow();response.Cookies.Append(RefreshName,credential,options);}
    public void Clear(HttpResponse response)=>response.Cookies.Delete(RefreshName,Options());
}
