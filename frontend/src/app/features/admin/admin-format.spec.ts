import {
  auditActionLabel,
  canDeactivate,
  describeAuditChange,
  formatUtcTimestamp,
  statusConfirmation,
} from './admin-format';

describe('administration formatting', () => {
  it('labels audit actions, describes safe changes, builds confirmations and never offers self-deactivation', () => {
    expect(auditActionLabel('AccountDeactivated')).toBe('Account deactivated');
    expect(describeAuditChange({ field: 'isActive', oldValue: 'true', newValue: 'false' })).toBe('Active: Yes → No');
    expect(describeAuditChange({ field: 'displayName', oldValue: null, newValue: 'Acme Support' })).toBe(
      'Display name: (empty) → Acme Support',
    );

    const account = { firstName: 'Ada', lastName: 'Lovelace', email: 'ada@example.com', role: 'Agent' };
    expect(statusConfirmation(account, false)).toContain('signed out everywhere');
    expect(statusConfirmation(account, true)).toContain('Reactivate Ada Lovelace (ada@example.com)');

    expect(canDeactivate({ id: 'a', isActive: true }, 'a')).toBe(false);
    expect(canDeactivate({ id: 'b', isActive: true }, 'a')).toBe(true);
    expect(canDeactivate({ id: 'b', isActive: false }, 'a')).toBe(false);

    expect(formatUtcTimestamp('2026-10-18T14:05:00Z')).toBe('Oct 18, 2026, 2:05 PM UTC');
  });
});
