using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Crm.Api.Authentication;

/// <summary>Double-submit check for cookie-authenticated endpoints (refresh, logout). 403 on mismatch.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class ValidateCsrfAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        var cookie = request.Cookies[AuthCookies.CsrfCookieName];
        var header = request.Headers[AuthCookies.CsrfHeaderName].ToString();
        if (string.IsNullOrEmpty(cookie) || string.IsNullOrEmpty(header) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(cookie), Encoding.UTF8.GetBytes(header)))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden); // UseStatusCodePages adds ProblemDetails
        }
    }
}
