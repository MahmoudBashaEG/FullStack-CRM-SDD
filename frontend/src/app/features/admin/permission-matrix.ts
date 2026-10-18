/**
 * Display-only description (ability/module/note values are admin.matrix.* dictionary keys, translated by the template) of the access the server enforces (ADMIN-001 AD2). Transcribed from docs/identity-and-access.md §1 and the
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
    module: 'admin.matrix.module.account',
    rows: [
      { ability: 'admin.matrix.ability.signIn', superAdmin: full, agent: full, customer: full },
      { ability: 'admin.matrix.ability.signUp', superAdmin: none, agent: none, customer: scoped('admin.matrix.note.createsCustomer') },
    ],
  },
  {
    module: 'admin.matrix.module.customers',
    rows: [
      {
        ability: 'admin.matrix.ability.customersManage',
        superAdmin: full,
        agent: full,
        customer: none,
      },
      { ability: 'admin.matrix.ability.ownProfile', superAdmin: none, agent: none, customer: scoped('admin.matrix.note.ownLinkedProfile') },
    ],
  },
  {
    module: 'admin.matrix.module.tickets',
    rows: [
      {
        ability: 'admin.matrix.ability.ticketCreate',
        superAdmin: scoped('admin.matrix.note.assignOrUnassigned'),
        agent: scoped('admin.matrix.note.assignedToSelf'),
        customer: none,
      },
      {
        ability: 'admin.matrix.ability.ticketManage',
        superAdmin: full,
        agent: scoped('admin.matrix.note.currentlyAssigned'),
        customer: none,
      },
      { ability: 'admin.matrix.ability.ticketAssign', superAdmin: full, agent: none, customer: none },
      { ability: 'admin.matrix.ability.ticketEscalate', superAdmin: full, agent: scoped('admin.matrix.note.assignedToThem'), customer: none },
    ],
  },
  {
    module: 'admin.matrix.module.productivity',
    rows: [
      { ability: 'admin.matrix.ability.ownDashboard', superAdmin: none, agent: full, customer: none },
      { ability: 'admin.matrix.ability.tasks', superAdmin: scoped('admin.matrix.note.anyActiveAgent'), agent: scoped('admin.matrix.note.ownTasks'), customer: none },
      {
        ability: 'admin.matrix.ability.quickReplies',
        superAdmin: scoped('admin.matrix.note.manageShared'),
        agent: scoped('admin.matrix.note.ownPersonal'),
        customer: none,
      },
      {
        ability: 'admin.matrix.ability.collaboration',
        superAdmin: full,
        agent: scoped('admin.matrix.note.collaborationScope'),
        customer: none,
      },
    ],
  },
  {
    module: 'admin.matrix.module.sla',
    rows: [
      { ability: 'admin.matrix.ability.slaAdmin', superAdmin: full, agent: none, customer: none },
      { ability: 'admin.matrix.ability.slaAlerts', superAdmin: full, agent: full, customer: none },
    ],
  },
  {
    module: 'admin.matrix.module.kb',
    rows: [
      { ability: 'admin.matrix.ability.kbRead', superAdmin: full, agent: full, customer: none },
      { ability: 'admin.matrix.ability.kbDraft', superAdmin: scoped('admin.matrix.note.anyDraft'), agent: scoped('admin.matrix.note.ownDrafts'), customer: none },
      { ability: 'admin.matrix.ability.kbPublish', superAdmin: full, agent: none, customer: none },
      { ability: 'admin.matrix.ability.kbLink', superAdmin: full, agent: scoped('admin.matrix.note.assignedTickets'), customer: none },
    ],
  },
  {
    module: 'admin.matrix.module.ai',
    rows: [
      {
        ability: 'admin.matrix.ability.aiAssist',
        superAdmin: full,
        agent: scoped('admin.matrix.note.aiScope'),
        customer: none,
      },
    ],
  },
  {
    module: 'admin.matrix.module.portal',
    rows: [
      {
        ability: 'admin.matrix.ability.portalTickets',
        superAdmin: none,
        agent: none,
        customer: scoped('admin.matrix.note.portalFeedback'),
      },
      { ability: 'admin.matrix.ability.helpCenter', superAdmin: none, agent: none, customer: scoped('admin.matrix.note.publishedPublic') },
    ],
  },
  {
    module: 'admin.matrix.module.reports',
    rows: [{ ability: 'admin.matrix.ability.managementReports', superAdmin: full, agent: none, customer: none }],
  },
  {
    module: 'admin.matrix.module.security',
    rows: [
      {
        ability: 'admin.matrix.ability.adminArea',
        superAdmin: full,
        agent: none,
        customer: none,
      },
      { ability: 'admin.matrix.ability.createAgent', superAdmin: full, agent: none, customer: none },
      {
        ability: 'admin.matrix.ability.createSuperAdmin',
        superAdmin: { level: 'none', note: 'admin.matrix.note.bootstrapOnly' },
        agent: none,
        customer: none,
      },
    ],
  },
];
