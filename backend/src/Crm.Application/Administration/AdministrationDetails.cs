using Crm.Application.Abstractions.Identity;
using Crm.Domain.Administration;

namespace Crm.Application.Administration;

public sealed record AccountStatusChange(UserAccount Account, bool Changed);

public sealed record AdminAuditEntryDetails(
    long Id, DateTimeOffset OccurredAtUtc, AdminAuditAction Action, Guid ActorUserId, string? ActorName, string? ActorEmail,
    AdminAuditTargetType TargetType, string TargetId, string TargetLabel, IReadOnlyList<AdminAuditChange> Changes);

public sealed record OrganizationSettingsDetails(
    bool IsConfigured, string? DisplayName, string? SupportEmail, string? SupportPhone, string? SupportInformation,
    string? AccentColor, bool HasLogo, string? LogoContentType, int? LogoSizeBytes, DateTimeOffset? LogoUpdatedAtUtc, int LogoVersion,
    DateTimeOffset? UpdatedAtUtc, string? UpdatedByName, byte[] Version);

/// <summary>Anonymous branding: nothing but these three public values. LogoVersion null = no logo.</summary>
public sealed record PublicBrandingDetails(string? DisplayName, string? AccentColor, int? LogoVersion);

public sealed record OrganizationLogoFile(byte[] Content, string ContentType);
