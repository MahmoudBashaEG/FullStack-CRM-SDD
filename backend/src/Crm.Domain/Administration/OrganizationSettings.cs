using Crm.Domain.Common;

namespace Crm.Domain.Administration;

/// <summary>
/// ADMIN-001 AD4: the one informational settings row (Id 1, inserted by the migration with all values null = not configured).
/// Informational only: never used by tickets, SLA, KB, AI or messaging. No secrets.
/// PLAT-001 adds the accent preset and the logo metadata (bytes live in OrganizationLogo).
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
    public string? AccentColor { get; private set; }           // BrandingRules.AccentPresets id; null = default (violet)
    public string? LogoContentType { get; private set; }       // image/png | image/jpeg | image/webp; null = no logo
    public int? LogoSizeBytes { get; private set; }
    public DateTimeOffset? LogoUpdatedAtUtc { get; private set; }

    /// <summary>Increments on every successful logo set/replace and never resets, so each replacement has a distinct public version (PLAT-001 C5).</summary>
    public int LogoVersion { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    public bool IsConfigured => DisplayName is not null;
    public bool HasLogo => LogoContentType is not null;

    /// <summary>Values must already be trimmed / normalized (null = empty). Returns the changes; empty = nothing was modified.</summary>
    public IReadOnlyList<AdminAuditChange> Update(
        string displayName, string? supportEmail, string? supportPhone, string? supportInformation, string? accentColor,
        Guid actorUserId, DateTimeOffset now)
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
        Track(changes, "accentColor", AccentColor, accentColor);
        if (changes.Count == 0)
        {
            return changes;
        }

        DisplayName = displayName;
        SupportEmail = supportEmail;
        SupportPhone = supportPhone;
        SupportInformation = supportInformation;
        AccentColor = accentColor;
        UpdatedAtUtc = now;
        UpdatedByUserId = actorUserId;
        return changes;
    }

    /// <summary>Logo set or replaced. Always a change (a replace with identical bytes is still an explicit admin action) and always a new LogoVersion.</summary>
    public IReadOnlyList<AdminAuditChange> SetLogo(string contentType, int sizeBytes, Guid actorUserId, DateTimeOffset now)
    {
        var changes = new List<AdminAuditChange> { AdminAuditChange.Of("logo", DescribeLogo(), Describe(contentType, sizeBytes)) };
        LogoContentType = contentType;
        LogoSizeBytes = sizeBytes;
        LogoUpdatedAtUtc = now;
        LogoVersion++;
        UpdatedAtUtc = now;
        UpdatedByUserId = actorUserId;
        return changes;
    }

    /// <summary>Empty when there was no logo (no-op, no audit).</summary>
    public IReadOnlyList<AdminAuditChange> RemoveLogo(Guid actorUserId, DateTimeOffset now)
    {
        if (!HasLogo)
        {
            return [];
        }

        var changes = new List<AdminAuditChange> { AdminAuditChange.Of("logo", DescribeLogo(), null) };
        LogoContentType = null;
        LogoSizeBytes = null;
        LogoUpdatedAtUtc = null;
        UpdatedAtUtc = now;
        UpdatedByUserId = actorUserId;
        return changes;
    }

    private string? DescribeLogo() => HasLogo ? Describe(LogoContentType!, LogoSizeBytes ?? 0) : null;

    private static string Describe(string contentType, int sizeBytes) => $"{contentType}, {Math.Max(1, sizeBytes / 1024)} KB";

    private static void Track(List<AdminAuditChange> changes, string field, string? oldValue, string? newValue)
    {
        if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            changes.Add(AdminAuditChange.Of(field, oldValue, newValue));
        }
    }
}
