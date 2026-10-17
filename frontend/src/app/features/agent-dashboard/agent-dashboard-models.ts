import { TicketCategory, TicketPriority, TicketStatus } from '../tickets/ticket-models';

/** Matches Crm.Api AgentWorkloadSummaryResponse. Counts overlap — never add them up. active = open + inProgress. */
export interface AgentWorkloadSummary {
  open: number;
  inProgress: number;
  active: number;
  escalated: number;
  highOrUrgent: number;
}

/** Matches Crm.Api AttentionTicketResponse. */
export interface AttentionTicket {
  id: string;
  subject: string;
  category: TicketCategory;
  priority: TicketPriority;
  status: TicketStatus;
  isEscalated: boolean;
  customerId: string;
  customerName: string;
  customerEmail: string;
  updatedAtUtc: string;
}

/** Matches Crm.Api AgentDashboardResponse. */
export interface AgentDashboard {
  summary: AgentWorkloadSummary;
  attentionQueue: AttentionTicket[];
  attentionQueueLimit: number;
  generatedAtUtc: string;
}
