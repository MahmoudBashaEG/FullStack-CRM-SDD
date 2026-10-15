namespace Crm.Infrastructure.Identity;

public sealed class BootstrapSuperAdminOptions
{
    public const string SectionName = "Bootstrap:SuperAdmin";

    public string Email { get; set; } = "admin@admin.com";
    public string FirstName { get; set; } = "System";
    public string LastName { get; set; } = "Administrator";
    /// <summary>User secrets / environment only. Used once, when the account does not exist yet.</summary>
    public string? Password { get; set; }
}
