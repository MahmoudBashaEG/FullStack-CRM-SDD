namespace Crm.Domain.Administration;

/// <summary>
/// ADMIN-001 AD3: append-only administrative audit record. Created only by the factories below and inserted in the same
/// transaction as the change it describes. Never updated or deleted by the application (no API exists). Id is a store-generated
/// identity (insertion order = deterministic tie-breaker). ActorUserId / TargetId reference accounts by id; names are joined at read time.
/// </summary>
public sealed class AdminAuditEntry
{
    public const string OrganizationSettingsTargetId = "1";
    public const string OrganizationSettingsLabel = "Organization settings";

    private AdminAuditEntry() { } // EF Core

    public long Id { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public Guid ActorUserId { get; private set; }
    public AdminAuditAction Action { get; private set; }
    public AdminAuditTargetType TargetType { get; private set; }
    public string TargetId { get; private set; } = string.Empty;
    public string TargetLabel { get; private set; } = string.Empty;
    public IReadOnlyList<AdminAuditChange> Changes { get; private set; } = [];

    public static AdminAuditEntry AgentCreated(Guid actorUserId, Guid agentUserId, string email, DateTimeOffset now) =>
        New(actorUserId, AdminAuditAction.AgentCreated, AdminAuditTargetType.UserAccount, agentUserId.ToString(), email, now,
            [AdminAuditChange.Of("role", null, "Agent"), AdminAuditChange.Of("isActive", null, "true")]);

    public static AdminAuditEntry AccountStatusChanged(Guid actorUserId, Guid userId, string email, bool isActive, DateTimeOffset now) =>
        New(actorUserId, isActive ? AdminAuditAction.AccountReactivated : AdminAuditAction.AccountDeactivated,
            AdminAuditTargetType.UserAccount, userId.ToString(), email, now,
            [AdminAuditChange.Of("isActive", isActive ? "false" : "true", isActive ? "true" : "false")]);

    public static AdminAuditEntry OrganizationSettingsUpdated(Guid actorUserId, IReadOnlyList<AdminAuditChange> changes, DateTimeOffset now)
    {
        if (changes.Count == 0)
        {
            throw new Common.DomainException("An unchanged save is not an audited change."); // backstop: the handler skips no-ops
        }

        return New(actorUserId, AdminAuditAction.OrganizationSettingsUpdated, AdminAuditTargetType.OrganizationSettings,
            OrganizationSettingsTargetId, OrganizationSettingsLabel, now, changes);
    }

    private static AdminAuditEntry New(
        Guid actorUserId, AdminAuditAction action, AdminAuditTargetType targetType, string targetId, string targetLabel,
        DateTimeOffset now, IReadOnlyList<AdminAuditChange> changes) =>
        new()
        {
            ActorUserId = actorUserId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            TargetLabel = targetLabel,
            OccurredAtUtc = now,
            Changes = changes,
        };
}
