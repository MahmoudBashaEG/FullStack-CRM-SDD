using Crm.Domain.Accounts;
using Microsoft.AspNetCore.Identity;
namespace Crm.Infrastructure.Identity;

public sealed class LocalUser : IdentityUser<Guid>
{
    public LocalUser() { Id = Guid.NewGuid(); SecurityStamp = Guid.NewGuid().ToString("N"); }
    public string DisplayName { get; set; } = "";
    public AccountRole AccountRole { get; set; }
    public bool IsActive { get; set; } = true;
    public long AuthVersion { get; set; } = 1;
    public byte[] RowVersion { get; set; } = [];
}
