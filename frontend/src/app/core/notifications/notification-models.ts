import type { SlaKind } from '../../features/sla/sla-models';
import { AgentTaskStatus } from '../../features/tasks/task-models';

/** Matches Crm.Api TaskReminderResponse. wasMissed = delivered more than 5 minutes after its scheduled time (delayed issuance). */
export interface TaskReminder {
  id: number;
  taskId: string;
  taskTitle: string;
  taskStatus: AgentTaskStatus;
  taskIsArchived: boolean;
  scheduledForUtc: string;
  issuedAtUtc: string;
  readAtUtc: string | null;
  isRead: boolean;
  wasMissed: boolean;
  transferredAtUtc: string | null;
}

/** Matches Crm.Api TaskReminderCheckResponse. */
export interface TaskReminderCheck {
  unreadCount: number;
  issuedNow: number;
  checkedAtUtc: string;
}

/** Matches Crm.Api SlaAlertResponse. */
export interface SlaAlert {
  id: number;
  ticketId: string;
  ticketSubject: string;
  ticketStatus: string;
  type: 'Warning' | 'Breach';
  kind: SlaKind;
  dueAtUtc: string;
  createdAtUtc: string;
  readAtUtc: string | null;
  isRead: boolean;
}

/** Matches Crm.Api SlaAlertCountResponse. */
export interface SlaAlertCount {
  unreadCount: number;
}
