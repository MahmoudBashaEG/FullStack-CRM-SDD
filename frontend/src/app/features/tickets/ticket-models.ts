import { Translate, englishT } from '../../core/i18n/translate-fn';
import { StatusTone } from '../../shared/ui/status-badge/status-badge';
import type { TicketSla } from '../sla/sla-models';

/** Fixed sets — must match Crm.Domain.Tickets enums (serialized by name). */
export const TICKET_CATEGORIES = ['General', 'Technical', 'Billing', 'Other'] as const;
export const TICKET_PRIORITIES = ['Low', 'Medium', 'High', 'Urgent'] as const;
/** Lifecycle (TICK-002). Changed only through workflow actions, never the edit form. */
export const TICKET_STATUSES = ['Open', 'InProgress', 'Resolved', 'Closed'] as const;

export type TicketCategory = (typeof TICKET_CATEGORIES)[number];
export type TicketPriority = (typeof TICKET_PRIORITIES)[number];
export type TicketStatus = (typeof TICKET_STATUSES)[number];

/** Matches Crm.Api TicketResponse. */
export interface Ticket {
  id: string;
  subject: string;
  description: string;
  category: TicketCategory;
  priority: TicketPriority;
  status: TicketStatus;
  customerId: string;
  customerName: string;
  customerEmail: string;
  createdByUserId: string;
  createdByName: string;
  assignedAgentUserId: string | null;
  assignedAgentName: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  isEscalated: boolean;
  escalationReason: string | null;
  escalatedAtUtc: string | null;
  escalatedByUserId: string | null;
  escalatedByName: string | null;
  /** null = not SLA-tracked (created before SLA tracking or while no SLA policy was enabled). */
  sla: TicketSla | null;
}

/** AI-001 D1: category-only edit; expectedCategory = the value on screen (409 if changed). */
export interface ChangeTicketCategoryRequest {
  expectedCategory: TicketCategory;
  category: TicketCategory;
}

export interface TicketListParams {
  page: number;
  pageSize: number;
  search: string | null;
  customerId: string | null;
  status: TicketStatus | null;
  category: TicketCategory | null;
  priority: TicketPriority | null;
  /** null/undefined = any. Optional so existing callers compile. */
  escalated?: boolean | null;
  /** true = at least one SLA target breached, false = none. null/undefined = any. */
  slaBreached?: boolean | null;
}

/** No status/creator/timestamps. assignedAgentUserId is sent only by SuperAdmin (null = unassigned). */
export interface CreateTicketRequest {
  customerId: string;
  subject: string;
  description: string;
  category: TicketCategory;
  priority: TicketPriority;
  assignedAgentUserId: string | null;
}

/** The only editable fields in TICK-001. */
export interface UpdateTicketRequest {
  subject: string;
  description: string;
  category: TicketCategory;
  priority: TicketPriority;
}

export function priorityTone(priority: TicketPriority): StatusTone {
  switch (priority) {
    case 'Urgent':
      return 'error';
    case 'High':
      return 'pending';
    default:
      return 'neutral';
  }
}

export const TICKET_FIELDS = ['Subject', 'Description', 'Category', 'Priority'] as const;
export type TicketField = (typeof TICKET_FIELDS)[number];
export type TicketActivityType =
  | 'Created'
  | 'FieldChanged'
  | 'AssigneeChanged'
  | 'StatusChanged'
  | 'Escalated'
  | 'EscalationCleared'
  | 'EscalationAutoCleared'
  | 'FirstResponseRecorded';

/** Matches Crm.Api TicketActivityResponse (names only, no user ids). */
export interface TicketActivity {
  id: number;
  type: TicketActivityType;
  occurredAtUtc: string;
  actorName: string;
  field: TicketField | null;
  oldValue: string | null;
  newValue: string | null;
  fromAgentName: string | null;
  toAgentName: string | null;
  reason: string | null;
  /** Performed by SLA automation (actorName is then 'Automation'). */
  isAutomatic: boolean;
}

/** null = unassigned. SuperAdmin only. */
export interface ChangeTicketAssigneeRequest {
  assignedAgentUserId: string | null;
}

/** fromStatus = the status shown to the user (stale ⇒ 409). */
export interface TransitionTicketRequest {
  fromStatus: TicketStatus;
  toStatus: TicketStatus;
}

export interface EscalateTicketRequest {
  reason: string;
}

export function statusLabel(status: TicketStatus, t: Translate = englishT): string {
  return t(`enums.ticketStatus.${status}`);
}

export function statusTone(status: TicketStatus): StatusTone {
  switch (status) {
    case 'Resolved':
      return 'success';
    case 'Closed':
      return 'neutral';
    default:
      return 'pending'; // Open, InProgress
  }
}
