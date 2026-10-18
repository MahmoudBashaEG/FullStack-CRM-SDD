import { UserRole } from '../auth/auth-models';

export interface NavItem {
  readonly label: string;
  readonly icon: string; // Material Icons ligature name
  readonly path: string;
  readonly exact: boolean;
  /** Omitted = every signed-in role. Display only; the API enforces access. */
  readonly roles?: readonly UserRole[];
}

/** Feature stories append their entry here when their first screen ships. */
export const NAV_ITEMS: readonly NavItem[] = [
  { label: 'Home', icon: 'home', path: '/', exact: true },
  { label: 'My Dashboard', icon: 'dashboard', path: '/agent-dashboard', exact: false, roles: ['Agent'] },
  { label: 'Tickets', icon: 'confirmation_number', path: '/tickets', exact: false, roles: ['SuperAdmin', 'Agent'] },
  { label: 'Customers', icon: 'groups', path: '/customers', exact: false, roles: ['SuperAdmin', 'Agent'] },
  { label: 'Tasks', icon: 'task_alt', path: '/tasks', exact: false, roles: ['SuperAdmin', 'Agent'] },
  { label: 'Quick Replies', icon: 'quickreply', path: '/quick-replies', exact: false, roles: ['SuperAdmin', 'Agent'] },
  { label: 'Knowledge Base', icon: 'menu_book', path: '/knowledge-base', exact: false, roles: ['SuperAdmin', 'Agent'] },
  { label: 'AI Assistant', icon: 'smart_toy', path: '/ai-assistant', exact: false, roles: ['SuperAdmin', 'Agent'] },
  { label: 'My Collaborations', icon: 'forum', path: '/collaboration', exact: false, roles: ['Agent'] },
  { label: 'Agents', icon: 'badge', path: '/agents', exact: false, roles: ['SuperAdmin'] },
  { label: 'SLA & Automation', icon: 'timer', path: '/sla', exact: false, roles: ['SuperAdmin'] },
  { label: 'Profile', icon: 'account_circle', path: '/profile', exact: false },
];
