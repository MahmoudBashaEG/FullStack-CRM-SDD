namespace Crm.Domain.Identity;

/// <summary>
/// One issued refresh token. Only the SHA-256 hash of the token is stored.
/// All rotations of one login share a FamilyId; reuse of a rotated token revokes the family.
/// </summary>
public sealed class RefreshSession
{
    private RefreshSession() { } // EF Core

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public RefreshSessionRevocationReason? RevocationReason { get; private set; }
    public Guid? ReplacedBySessionId { get; private set; }

    public static RefreshSession Create(
        Guid id, Guid userId, Guid familyId, string tokenHash, DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc) =>
        new()
        {
            Id = id,
            UserId = userId,
            FamilyId = familyId,
            TokenHash = tokenHash,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = expiresAtUtc,
        };
}
