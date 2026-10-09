namespace Crm.Infrastructure.Identity;

// Completion marker only; credentials are supplied separately by the installation operator.
public sealed class InitialAdminSetup
{
    public int Id { get; set; } = 1;
    public Guid AdminUserId { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
}
