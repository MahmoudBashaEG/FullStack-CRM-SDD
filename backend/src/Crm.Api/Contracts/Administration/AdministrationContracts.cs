using Crm.Application.Abstractions.Identity;
using Crm.Application.Administration;
using Crm.Domain.Administration;

namespace Crm.Api.Contracts.Administration;

/// <summary>Safe account fields only: never the security stamp, hash, lockout data or tokens.</summary>
public sealed record AdminAccountResponse(
    Guid Id, string Email, string FirstName, string LastName, string? PhoneNumber, string Role, bool IsActive, DateTimeOffset CreatedAtUtc)
{
    public static AdminAccountResponse From(UserAccount a) =>
        new(a.Id, a.Email, a.FirstName, a.LastName, a.PhoneNumber, a.Role, a.IsActive, a.CreatedAtUtc);
}

/// <summary>IsActive nullable so a missing field is 400, not "deactivate".</summary>
public sealed record SetAccountStatusRequest(bool? IsActive);

public sealed record AccountStatusResponse(AdminAccountResponse Account, bool Changed)
{
    public static AccountStatusResponse From(AccountStatusChange c) => new(AdminAccountResponse.From(c.Account), c.Changed);
}

public sealed record AdminAuditChangeResponse(string Field, string? OldValue, string? NewValue);

public sealed record AdminAuditLogResponse(
    long Id, DateTimeOffset OccurredAtUtc, AdminAuditAction Action, Guid ActorUserId, string? ActorName, string? ActorEmail,
    AdminAuditTargetType TargetType, string TargetId, string TargetLabel, IReadOnlyList<AdminAuditChangeResponse> Changes)
{
    public static AdminAuditLogResponse From(AdminAuditEntryDetails d) =>
        new(d.Id, d.OccurredAtUtc, d.Action, d.ActorUserId, d.ActorName, d.ActorEmail, d.TargetType, d.TargetId, d.TargetLabel,
            d.Changes.Select(c => new AdminAuditChangeResponse(c.Field, c.OldValue, c.NewValue)).ToList());
}

/// <summary>Version = base64 rowversion (the ETag value without quotes).</summary>
public sealed record OrganizationSettingsResponse(
    bool IsConfigured, string? DisplayName, string? SupportEmail, string? SupportPhone, string? SupportInformation,
    DateTimeOffset? UpdatedAtUtc, string? UpdatedByName, string Version)
{
    public static OrganizationSettingsResponse From(OrganizationSettingsDetails d) =>
        new(d.IsConfigured, d.DisplayName, d.SupportEmail, d.SupportPhone, d.SupportInformation, d.UpdatedAtUtc, d.UpdatedByName,
            Convert.ToBase64String(d.Version));
}

public sealed record UpdateOrganizationSettingsRequest(string? DisplayName, string? SupportEmail, string? SupportPhone, string? SupportInformation);
