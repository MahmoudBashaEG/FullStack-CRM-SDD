namespace Crm.Infrastructure.Authentication;

public sealed class RefreshCredential
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public byte[] TokenHash { get; set; } = [];
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? ConsumedAtUtc { get; set; }
    public Guid? ReplacedById { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
