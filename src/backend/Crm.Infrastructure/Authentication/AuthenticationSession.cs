namespace Crm.Infrastructure.Authentication;

public enum SessionRevocationReason { Signout = 0, AccountDisabled = 1, CredentialReplay = 2, Replaced = 3 }
public sealed class AuthenticationSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public long AuthVersionAtCreation { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public SessionRevocationReason? RevocationReason { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
