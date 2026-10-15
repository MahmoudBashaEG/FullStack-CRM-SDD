namespace Crm.Domain.Identity;

/// <summary>The only roles in the system. Server-assigned; never accepted from clients.</summary>
public static class UserRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Agent = "Agent";
    public const string Customer = "Customer";

    public static readonly IReadOnlyList<string> All = [SuperAdmin, Agent, Customer];
}
