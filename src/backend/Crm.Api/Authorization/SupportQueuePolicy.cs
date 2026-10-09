using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Crm.Api.Authorization;

public static class SupportQueuePolicy
{
    public const string Name = "SupportQueue";
    public static bool HasAccess(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true &&
        !string.IsNullOrWhiteSpace(user.FindFirstValue("iss")) && Guid.TryParse(user.FindFirstValue("sub"), out _) &&
        (user.IsInRole("Agent") || user.IsInRole("Admin"));
    public static void Configure(AuthorizationOptions options) => CrmPolicies.Configure(options);
}
