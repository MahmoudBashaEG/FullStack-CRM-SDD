using Crm.Domain.Accounts;
using Microsoft.AspNetCore.Authorization;
namespace Crm.Api.Authorization;

public static class CrmPolicies
{
    public const string AuthenticatedCrmUser = "AuthenticatedCrmUser";
    public const string AdminAgents = "AdminAgents";
    public static void Configure(AuthorizationOptions options)
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        options.AddPolicy(AuthenticatedCrmUser, policy => policy.RequireAuthenticatedUser());
        options.AddPolicy(SupportQueuePolicy.Name, policy => policy.RequireAuthenticatedUser().RequireRole(AccountRole.Agent.ToString(),AccountRole.Admin.ToString()));
        options.AddPolicy(AdminAgents,policy => policy.RequireAuthenticatedUser().RequireRole(AccountRole.Admin.ToString()));
    }
}
