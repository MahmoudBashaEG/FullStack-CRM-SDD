using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Crm.Api.Authorization;

public static class SupportQueuePolicy
{
    public const string Name = "SupportQueue";
    public static bool HasAccess(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true &&
        !string.IsNullOrWhiteSpace(user.FindFirstValue("iss")) && !string.IsNullOrWhiteSpace(user.FindFirstValue("sub")) &&
        user.HasClaim("crm_support_queue", "shared");
    public static void Configure(AuthorizationOptions options) => options.AddPolicy(Name, policy =>
        policy.RequireAuthenticatedUser().RequireAssertion(context => HasAccess(context.User)));
}
