using Crm.Domain.Common;

namespace Crm.Domain.Administration;

/// <summary>
/// ADMIN-001 AD4: the one informational settings row (Id 1, inserted by the migration with all values null = not configured).
/// Informational only: never used by tickets, SLA, KB, AI or messaging. No secrets.
/// </summary>
public sealed class OrganizationSettings
{
    public const int SingletonId = 1;

    private OrganizationSettings() { } // EF Core

    public int Id { get; private set; }
    public string? DisplayName { get; private set; }
    public string? SupportEmail { get; private set; }
    public string? SupportPhone { get; private set; }
    public string? SupportInformation { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    public bool IsConfigured => DisplayName is not null;

    /// <summary>Values must already be trimmed / normalized (null = empty). Returns the changes; empty = nothing was modified.</summary>
    public IReadOnlyList<AdminAuditChange> Update(
        string displayName, string? supportEmail, string? supportPhone, string? supportInformation, Guid actorUserId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new DomainException("The organization display name is required."); // backstop: the validator returns 400 first
        }

        var changes = new List<AdminAuditChange>();
        Track(changes, "displayName", DisplayName, displayName);
        Track(changes, "supportEmail", SupportEmail, supportEmail);
        Track(changes, "supportPhone", SupportPhone, supportPhone);
        Track(changes, "supportInformation", SupportInformation, supportInformation);
        if (changes.Count == 0)
        {
            return changes;
        }

        DisplayName = displayName;
        SupportEmail = supportEmail;
        SupportPhone = supportPhone;
        SupportInformation = supportInformation;
        UpdatedAtUtc = now;
        UpdatedByUserId = actorUserId;
        return changes;
    }

    private static void Track(List<AdminAuditChange> changes, string field, string? oldValue, string? newValue)
    {
        if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            changes.Add(AdminAuditChange.Of(field, oldValue, newValue));
        }
    }
}
