namespace Crm.Api.Authentication;

public static class AuthorizationPolicies
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Staff = "Staff";       // SuperAdmin or Agent
    public const string Customer = "Customer"; // Customer role only
}

public static class RateLimitPolicies
{
    public const string Credentials = "credentials";
}
