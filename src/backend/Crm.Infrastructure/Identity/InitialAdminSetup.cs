namespace Crm.Infrastructure.Identity;

// Schema foundation only. No bootstrap handler, password seed or initial account creation.
public sealed class InitialAdminSetup
{
    public int Id { get; set; } = 1;
    public Guid AdminUserId { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
}
