import { Translate, englishT } from '../../core/i18n/translate-fn';
import { AuditAction, AuditChange } from './admin-models';

export function auditActionLabel(action: AuditAction, t: Translate = englishT): string {
  return t(`admin.audit.action.${action}`);
}

const KNOWN_FIELDS = new Set([
  'displayName',
  'supportEmail',
  'supportPhone',
  'supportInformation',
  'isActive',
  'role',
  'accentColor',
  'logo',
]);

export function auditFieldLabel(field: string, t: Translate = englishT): string {
  return KNOWN_FIELDS.has(field) ? t(`admin.audit.field.${field}`) : field;
}

function auditValue(value: string | null, t: Translate): string {
  if (value === null || value === '') {
    return t('admin.audit.empty');
  }
  return value === 'true' ? t('common.yes') : value === 'false' ? t('common.no') : value;
}

/** e.g. "Display name: (empty) → Acme Support", "Active: Yes → No". Plain text: render with interpolation only. Old/new values are data and never translated. */
export function describeAuditChange(change: AuditChange, t: Translate = englishT): string {
  return `${auditFieldLabel(change.field, t)}: ${auditValue(change.oldValue, t)} → ${auditValue(change.newValue, t)}`;
}

export function statusConfirmation(
  account: { firstName: string; lastName: string; email: string; role: string },
  activate: boolean,
  t: Translate = englishT,
): string {
  const who = `${account.firstName} ${account.lastName} (${account.email})`;
  return t(activate ? 'admin.statusConfirm.reactivate' : 'admin.statusConfirm.deactivate', { who });
}

/** UX only: the server refuses self-deactivation and the last active SuperAdmin regardless. */
export function canDeactivate(account: { id: string; isActive: boolean }, currentUserId: string | null): boolean {
  return account.isActive && account.id !== currentUserId;
}

const UTC_FORMAT = new Intl.DateTimeFormat('en-US', { timeZone: 'UTC', dateStyle: 'medium', timeStyle: 'short' });

export function formatUtcTimestamp(iso: string): string {
  return `${UTC_FORMAT.format(new Date(iso))} UTC`;
}
