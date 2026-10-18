import { UserRole } from '../auth/auth-models';

export type NavGroup = 'portal' | 'workspace' | 'knowledge' | 'management' | 'account';

export interface NavItem {
  /** Key under `nav.*` in the translation dictionary. */
  readonly labelKey: string;
  readonly icon: string; // Material Icons ligature name
  readonly path: string;
  readonly exact: boolean;
  readonly group: NavGroup;
  /** Omitted = every signed-in role. Display only; the API enforces access. */
  readonly roles?: readonly UserRole[];
}

export const NAV_GROUP_ORDER: readonly NavGroup[] = ['portal', 'workspace', 'knowledge', 'management', 'account'];

/** Feature stories append their entry here when their first screen ships. Paths and roles are unchanged by PLAT-001. */
export const NAV_ITEMS: readonly NavItem[] = [
  { labelKey: 'nav.home', icon: 'home', path: '/', exact: true, group: 'workspace' },
  { labelKey: 'nav.supportPortal', icon: 'support_agent', path: '/portal', exact: true, group: 'portal', roles: ['Customer'] },
  { labelKey: 'nav.myTickets', icon: 'confirmation_number', path: '/portal/tickets', exact: false, group: 'portal', roles: ['Customer'] },
  { labelKey: 'nav.newTicket', icon: 'add_circle', path: '/portal/new-ticket', exact: true, group: 'portal', roles: ['Customer'] },
  { labelKey: 'nav.helpCenter', icon: 'help_center', path: '/portal/help-center', exact: false, group: 'portal', roles: ['Customer'] },
  { labelKey: 'nav.myDashboard', icon: 'dashboard', path: '/agent-dashboard', exact: false, group: 'workspace', roles: ['Agent'] },
  { labelKey: 'nav.tickets', icon: 'confirmation_number', path: '/tickets', exact: false, group: 'workspace', roles: ['SuperAdmin', 'Agent'] },
  { labelKey: 'nav.customers', icon: 'groups', path: '/customers', exact: false, group: 'workspace', roles: ['SuperAdmin', 'Agent'] },
  { labelKey: 'nav.tasks', icon: 'task_alt', path: '/tasks', exact: false, group: 'workspace', roles: ['SuperAdmin', 'Agent'] },
  { labelKey: 'nav.myCollaborations', icon: 'forum', path: '/collaboration', exact: false, group: 'workspace', roles: ['Agent'] },
  { labelKey: 'nav.knowledgeBase', icon: 'menu_book', path: '/knowledge-base', exact: false, group: 'knowledge', roles: ['SuperAdmin', 'Agent'] },
  { labelKey: 'nav.quickReplies', icon: 'quickreply', path: '/quick-replies', exact: false, group: 'knowledge', roles: ['SuperAdmin', 'Agent'] },
  { labelKey: 'nav.aiAssistant', icon: 'smart_toy', path: '/ai-assistant', exact: false, group: 'knowledge', roles: ['SuperAdmin', 'Agent'] },
  { labelKey: 'nav.agents', icon: 'badge', path: '/agents', exact: false, group: 'management', roles: ['SuperAdmin'] },
  { labelKey: 'nav.slaAndAutomation', icon: 'timer', path: '/sla', exact: false, group: 'management', roles: ['SuperAdmin'] },
  { labelKey: 'nav.reports', icon: 'insights', path: '/reports', exact: false, group: 'management', roles: ['SuperAdmin'] },
  { labelKey: 'nav.administration', icon: 'admin_panel_settings', path: '/admin', exact: false, group: 'management', roles: ['SuperAdmin'] },
  { labelKey: 'nav.profile', icon: 'account_circle', path: '/profile', exact: false, group: 'account' },
];
