/**
 * Display-only description of the access the server enforces (ADMIN-001 AD2). Transcribed from docs/identity-and-access.md §1 and the
 * controller policies. There is no endpoint or permission store: when a backend permission changes, edit this file and the doc together.
 */
export type Access = { level: 'full' | 'scoped' | 'none'; note?: string };
export interface PermissionRow {
  ability: string;
  superAdmin: Access;
  agent: Access;
  customer: Access;
}
export interface PermissionModule {
  module: string;
  rows: PermissionRow[];
}

const full: Access = { level: 'full' };
const none: Access = { level: 'none' };
const scoped = (note: string): Access => ({ level: 'scoped', note });

export const PERMISSION_MATRIX: readonly PermissionModule[] = [
  {
    module: 'Account & sign-in',
    rows: [
      { ability: 'Sign in; view/update own profile; change own password', superAdmin: full, agent: full, customer: full },
      { ability: 'Public sign-up', superAdmin: none, agent: none, customer: scoped('Creates a Customer account') },
    ],
  },
  {
    module: 'Customer Management',
    rows: [
      {
        ability: 'List/view/create contact-only/edit customers; notes, interactions, attachments (append-only)',
        superAdmin: full,
        agent: full,
        customer: none,
      },
      { ability: 'Own CRM customer profile', superAdmin: none, agent: none, customer: scoped('Own linked profile') },
    ],
  },
  {
    module: 'Tickets',
    rows: [
      {
        ability: 'Create tickets',
        superAdmin: scoped('Assign to an active Agent or leave unassigned'),
        agent: scoped('Assigned to self'),
        customer: none,
      },
      {
        ability: 'List/view/edit, change status, view activity',
        superAdmin: full,
        agent: scoped('Currently assigned to them only (collaboration does not grant access)'),
        customer: none,
      },
      { ability: 'Assign/reassign/unassign; clear escalation', superAdmin: full, agent: none, customer: none },
      { ability: 'Escalate', superAdmin: full, agent: scoped('Assigned to them'), customer: none },
    ],
  },
  {
    module: 'Agent Dashboard & productivity',
    rows: [
      { ability: 'Own Agent Dashboard and task reminder inbox', superAdmin: none, agent: full, customer: none },
      { ability: 'Tasks', superAdmin: scoped('Any active Agent; reassign'), agent: scoped('Own tasks'), customer: none },
      {
        ability: 'Quick replies',
        superAdmin: scoped('Manage Shared'),
        agent: scoped('Own Personal; read/copy Shared'),
        customer: none,
      },
      {
        ability: 'Internal discussion / collaborators',
        superAdmin: full,
        agent: scoped('Primary assignee or active collaborator; invite/remove: primary assignee only'),
        customer: none,
      },
    ],
  },
  {
    module: 'SLA & Automation',
    rows: [
      { ability: 'SLA policy, business calendar, automation rules, SLA overview', superAdmin: full, agent: none, customer: none },
      { ability: 'SLA alerts (notification bell)', superAdmin: full, agent: full, customer: none },
    ],
  },
  {
    module: 'Knowledge Base',
    rows: [
      { ability: 'Read/search Published articles', superAdmin: full, agent: full, customer: none },
      { ability: 'Create/edit Drafts, submit', superAdmin: scoped('Any Draft'), agent: scoped('Own Drafts'), customer: none },
      { ability: 'Return/publish/archive/restore; manage categories', superAdmin: full, agent: none, customer: none },
      { ability: 'Link articles to tickets', superAdmin: full, agent: scoped('Assigned tickets'), customer: none },
    ],
  },
  {
    module: 'AI assistance (staff)',
    rows: [
      {
        ability: 'Ticket summary/reply draft/category/solutions; AI chat',
        superAdmin: full,
        agent: scoped('Assigned tickets only (chat: ticket context assigned only)'),
        customer: none,
      },
    ],
  },
  {
    module: 'Customer Portal',
    rows: [
      {
        ability: 'Submit/list/view own tickets and status history; CSAT feedback',
        superAdmin: none,
        agent: none,
        customer: scoped('Own linked profile; feedback on own Resolved/Closed tickets'),
      },
      { ability: 'Help Center', superAdmin: none, agent: none, customer: scoped('Published + Public articles only') },
    ],
  },
  {
    module: 'Reports & Management',
    rows: [{ ability: 'Management dashboard and reports', superAdmin: full, agent: none, customer: none }],
  },
  {
    module: 'Security & Administration',
    rows: [
      {
        ability: 'Users, account (de)activation, audit logs, organization settings, this matrix',
        superAdmin: full,
        agent: none,
        customer: none,
      },
      { ability: 'Create Agent accounts', superAdmin: full, agent: none, customer: none },
      {
        ability: 'Create SuperAdmin / change any role',
        superAdmin: { level: 'none', note: 'Bootstrap only; roles never change' },
        agent: none,
        customer: none,
      },
    ],
  },
];
