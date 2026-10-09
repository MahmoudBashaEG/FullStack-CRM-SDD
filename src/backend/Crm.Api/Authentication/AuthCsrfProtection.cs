using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Crm.Api.Errors;
namespace Crm.Api.Authentication;

public sealed class AuthCsrfProtection(IAntiforgery antiforgery,IConfiguration configuration) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context,ActionExecutionDelegate next)
    {
        var http=context.HttpContext;var signinOrRefresh=http.Request.Path.Value is "/api/auth/signup" or "/api/auth/signin" or "/api/auth/refresh" or "/api/auth/csrf";
        if(http.Request.Query.Count>0 || (signinOrRefresh&&http.Request.Headers.ContainsKey("Authorization")))
        {context.Result=ProblemDetailsMapping.Result(new("validation_failed",400),http);return;}
        if(http.Request.Method=="GET"&&http.Request.Path=="/api/auth/csrf")
        {context.Result=new OkObjectResult(Mint(http));return;}
        if(http.Request.Method=="POST")
        {
            var origins=configuration.GetSection("Auth:AllowedOrigins").Get<string[]>()??[];
            if(http.Request.Headers.Origin.Count!=1||!origins.Contains(http.Request.Headers.Origin.ToString(),StringComparer.Ordinal))
            {context.Result=ProblemDetailsMapping.Result(new("csrf_failed",403),http);return;}
            var principal=http.User;try
            {
                http.User=new ClaimsPrincipal(new ClaimsIdentity());
                await antiforgery.ValidateRequestAsync(http);
            }
            catch(AntiforgeryValidationException){context.Result=ProblemDetailsMapping.Result(new("csrf_failed",403),http);return;}
            finally{http.User=principal;}
        }
        await next();
    }
    public object Mint(HttpContext context)
    {
        var principal=context.User;try{context.User=new ClaimsPrincipal(new ClaimsIdentity());return new {requestToken=antiforgery.GetAndStoreTokens(context).RequestToken};}finally{context.User=principal;}
    }
}
