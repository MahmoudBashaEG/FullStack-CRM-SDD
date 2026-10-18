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
