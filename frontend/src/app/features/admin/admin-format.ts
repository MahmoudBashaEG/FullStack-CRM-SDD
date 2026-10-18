import { AuditAction, AuditChange } from './admin-models';

const ACTION_LABELS: Record<AuditAction, string> = {
  AgentCreated: 'Agent created',
  AccountDeactivated: 'Account deactivated',
  AccountReactivated: 'Account reactivated',
  OrganizationSettingsUpdated: 'Organization settings updated',
};

const FIELD_LABELS: Record<string, string> = {
  displayName: 'Display name',
  supportEmail: 'Support email',
  supportPhone: 'Support phone',
  supportInformation: 'Support information',
  isActive: 'Active',
  role: 'Role',
};

export function auditActionLabel(action: AuditAction): string {
  return ACTION_LABELS[action];
}

export function auditFieldLabel(field: string): string {
  return FIELD_LABELS[field] ?? field;
}

function auditValue(value: string | null): string {
  if (value === null || value === '') {
    return '(empty)';
  }
  return value === 'true' ? 'Yes' : value === 'false' ? 'No' : value;
}

/** e.g. "Display name: (empty) → Acme Support", "Active: Yes → No". Plain text: render with interpolation only. */
export function describeAuditChange(change: AuditChange): string {
  return `${auditFieldLabel(change.field)}: ${auditValue(change.oldValue)} → ${auditValue(change.newValue)}`;
}

export function statusConfirmation(account: { firstName: string; lastName: string; email: string; role: string }, activate: boolean): string {
  const who = `${account.firstName} ${account.lastName} (${account.email})`;
  return activate
    ? `Reactivate ${who}? They can sign in again with their existing password.`
    : `Deactivate ${who}? They are signed out everywhere immediately and cannot sign in until reactivated. Their tickets and history are kept.`;
}

/** UX only: the server refuses self-deactivation and the last active SuperAdmin regardless. */
export function canDeactivate(account: { id: string; isActive: boolean }, currentUserId: string | null): boolean {
  return account.isActive && account.id !== currentUserId;
}

const UTC_FORMAT = new Intl.DateTimeFormat('en-US', { timeZone: 'UTC', dateStyle: 'medium', timeStyle: 'short' });

export function formatUtcTimestamp(iso: string): string {
  return `${UTC_FORMAT.format(new Date(iso))} UTC`;
}
