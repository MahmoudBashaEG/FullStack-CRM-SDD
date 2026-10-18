namespace Crm.Domain.Administration;

/// <summary>ADMIN-001 AD3: the only audited administrative actions. Stored by name.</summary>
public enum AdminAuditAction
{
    AgentCreated,
    AccountDeactivated,
    AccountReactivated,
    OrganizationSettingsUpdated,
}

public enum AdminAuditTargetType
{
    UserAccount,
    OrganizationSettings,
}
