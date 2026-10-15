export interface NavItem {
  readonly label: string;
  readonly icon: string; // Material Icons ligature name
  readonly path: string;
  readonly exact: boolean;
}

/** Feature stories append their entry here when their first screen ships. */
export const NAV_ITEMS: readonly NavItem[] = [
  { label: 'Home', icon: 'home', path: '/', exact: true },
];
