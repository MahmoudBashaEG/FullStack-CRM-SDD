import { TicketPriority, TicketStatus } from '../tickets/ticket-models';

export const TASK_STATUSES = ['Pending', 'Completed'] as const;
export type AgentTaskStatus = (typeof TASK_STATUSES)[number];
export type TaskDueFilter = 'Overdue' | 'DueSoon' | 'NoDueDate';
export type LinkedTicketState = 'None' | 'Accessible' | 'Inaccessible';

/** Matches Crm.Api AgentTaskResponse. linkedTicketId/Subject are null for an Agent when the link is Inaccessible. */
export interface AgentTask {
  id: string;
  title: string;
  description: string | null;
  status: AgentTaskStatus;
  assignedAgentUserId: string;
  assignedAgentName: string;
  createdByUserId: string;
  createdByName: string;
  dueAtUtc: string | null;
  remindAtUtc: string | null;
  reminderSentAtUtc: string | null;
  linkedTicketState: LinkedTicketState;
  linkedTicketId: string | null;
  linkedTicketSubject: string | null;
  isOverdue: boolean;
  isDueSoon: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  completedAtUtc: string | null;
  isArchived: boolean;
  archivedAtUtc: string | null;
  /** Optimistic-concurrency token (base64 rowversion). Sent back as If-Match: "<version>" on every mutation. */
  version: string;
}

/** Matches Crm.Api LinkableTicketResponse (picker only). */
export interface LinkableTicket {
  id: string;
  subject: string;
  status: TicketStatus;
  priority: TicketPriority;
}

export interface TaskListParams {
  page: number;
  pageSize: number;
  search: string | null;
  status: AgentTaskStatus | null;
  dueState: TaskDueFilter | null;
  archived: boolean;
  hasReminder: boolean | null;
  assignedAgentUserId: string | null; // SuperAdmin filter
}

/** assignedAgentUserId: SuperAdmin only (required there); Agents send null. */
export interface CreateTaskRequest {
  title: string;
  description: string | null;
  assignedAgentUserId: string | null;
  ticketId: string | null;
  dueAtUtc: string | null;
  remindAtUtc: string | null;
}

export interface UpdateTaskRequest {
  title: string;
  description: string | null;
  dueAtUtc: string | null;
}

/** Matches Crm.Api AgentTaskSummaryResponse. */
export interface AgentTaskSummary {
  pending: number;
  overdue: number;
  dueSoon: number;
  nextTasks: AgentTask[];
  previewLimit: number;
  generatedAtUtc: string;
}

/** @deprecated PLAT-001: use shared/date/date-only.ts. <input type="datetime-local"> value (viewer's local zone) ⇒ ISO UTC; '' ⇒ null. */
export function fromLocalInput(value: string): string | null {
  return value ? new Date(value).toISOString() : null;
}

/** @deprecated PLAT-001: use shared/date/date-only.ts. ISO UTC ⇒ 'yyyy-MM-ddTHH:mm' in the viewer's local zone for datetime-local inputs; null ⇒ ''. */
export function toLocalInput(iso: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}
