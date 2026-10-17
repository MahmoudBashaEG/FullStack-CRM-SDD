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
  { label: 'Customers', icon: 'groups', path: '/customers', exact: false, roles: ['SuperAdmin', 'Agent'] },
  { label: 'Agents', icon: 'badge', path: '/agents', exact: false, roles: ['SuperAdmin'] },
  { label: 'Profile', icon: 'account_circle', path: '/profile', exact: false },
];
