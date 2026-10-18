using Crm.Application.Abstractions.Identity;
using Crm.Domain.Administration;

namespace Crm.Application.Administration;

public sealed record AccountStatusChange(UserAccount Account, bool Changed);

public sealed record AdminAuditEntryDetails(
    long Id, DateTimeOffset OccurredAtUtc, AdminAuditAction Action, Guid ActorUserId, string? ActorName, string? ActorEmail,
    AdminAuditTargetType TargetType, string TargetId, string TargetLabel, IReadOnlyList<AdminAuditChange> Changes);

public sealed record OrganizationSettingsDetails(
    bool IsConfigured, string? DisplayName, string? SupportEmail, string? SupportPhone, string? SupportInformation,
    DateTimeOffset? UpdatedAtUtc, string? UpdatedByName, byte[] Version);
