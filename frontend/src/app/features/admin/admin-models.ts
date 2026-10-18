import { UserRole } from '../../core/auth/auth-models';
import { AccentPreset } from '../../core/branding/branding-api';

/** Matches Crm.Api AdminAccountResponse. */
export interface AdminAccount {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber: string | null;
  role: UserRole;
  isActive: boolean;
  createdAtUtc: string;
}

export interface AccountStatusResponse {
  account: AdminAccount;
  changed: boolean;
}

export const AUDIT_ACTIONS = ['AgentCreated', 'AccountDeactivated', 'AccountReactivated', 'OrganizationSettingsUpdated'] as const;
export type AuditAction = (typeof AUDIT_ACTIONS)[number];

export interface AuditChange {
  field: string;
  oldValue: string | null;
  newValue: string | null;
}

/** Matches Crm.Api AdminAuditLogResponse. */
export interface AuditLogEntry {
  id: number;
  occurredAtUtc: string;
  action: AuditAction;
  actorUserId: string;
  actorName: string | null;
  actorEmail: string | null;
  targetType: 'UserAccount' | 'OrganizationSettings';
  targetId: string;
  targetLabel: string;
  changes: AuditChange[];
}

/** Matches Crm.Api OrganizationSettingsResponse. version is sent back as If-Match. */
export interface OrganizationSettings {
  isConfigured: boolean;
  displayName: string | null;
  supportEmail: string | null;
  supportPhone: string | null;
  supportInformation: string | null;
  updatedAtUtc: string | null;
  updatedByName: string | null;
  accentColor: AccentPreset | null;
  hasLogo: boolean;
  logoContentType: string | null;
  logoSizeBytes: number | null;
  logoUpdatedAtUtc: string | null;
  logoVersion: number;
  version: string;
}

export interface UpdateOrganizationSettingsRequest {
  displayName: string;
  supportEmail: string | null;
  supportPhone: string | null;
  supportInformation: string | null;
  /** Always the selected preset id; omitted/null would keep the saved accent on the server. */
  accentColor: AccentPreset | null;
}

export interface AccountListParams {
  page: number;
  pageSize: number;
  search: string;
  role: UserRole | '';
  status: 'active' | 'inactive' | '';
}

export interface AuditListParams {
  page: number;
  pageSize: number;
  action: AuditAction | '';
  from: string;
  to: string;
  search: string;
}
