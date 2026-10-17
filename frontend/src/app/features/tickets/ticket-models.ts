import { StatusTone } from '../../shared/ui/status-badge/status-badge';

/** Fixed sets — must match Crm.Domain.Tickets enums (serialized by name). */
export const TICKET_CATEGORIES = ['General', 'Technical', 'Billing', 'Other'] as const;
export const TICKET_PRIORITIES = ['Low', 'Medium', 'High', 'Urgent'] as const;
/** TICK-001 has only Open; TICK-002 adds the other lifecycle states. */
export const TICKET_STATUSES = ['Open'] as const;

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
}

export interface TicketListParams {
  page: number;
  pageSize: number;
  search: string | null;
  customerId: string | null;
  status: TicketStatus | null;
  category: TicketCategory | null;
  priority: TicketPriority | null;
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
