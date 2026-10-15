using Microsoft.AspNetCore.Identity;

namespace Crm.Infrastructure.Identity;

/// <summary>Identity persistence model. Never leaves Infrastructure. UserName == Email.</summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
