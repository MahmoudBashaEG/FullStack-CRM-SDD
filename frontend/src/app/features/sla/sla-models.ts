import { StatusTone } from '../../shared/ui/status-badge/status-badge';
import { TicketCategory, TicketPriority, TicketStatus } from '../tickets/ticket-models';

/** Fixed sets — must match the Crm.Domain enums / DayOfWeek (serialized by name). */
export const WEEK_DAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'] as const;
export type WeekDay = (typeof WEEK_DAYS)[number];
export const AUTOMATION_TRIGGERS = ['TicketCreated', 'SlaWarningDue', 'SlaBreached'] as const;
export type AutomationTrigger = (typeof AUTOMATION_TRIGGERS)[number];
export type AutomationAction = 'AssignToAgent' | 'CreateInAppNotification' | 'EscalateTicket';
export type SlaKind = 'FirstResponse' | 'Resolution';
export type SlaIndicator = 'NotTracked' | 'OnTrack' | 'Warning' | 'Breached' | 'Met';
export type SlaTargetState = 'Pending' | 'Met' | 'Breached';
export type AutomationOutcome = 'Assigned' | 'Notified' | 'Escalated' | 'AlreadyEscalated' | 'NotEscalatable';

/** Must match Crm.Domain.Automation.AutomationRule.IsValidCombination. Display only — the server validates. */
export const ACTIONS_BY_TRIGGER: Readonly<Record<AutomationTrigger, readonly AutomationAction[]>> = {
  TicketCreated: ['AssignToAgent'],
  SlaWarningDue: ['CreateInAppNotification'],
  SlaBreached: ['CreateInAppNotification', 'EscalateTicket'],
};

/** Matches Crm.Api SlaTargetDto. */
export interface SlaTarget {
  priority: TicketPriority;
  firstResponseMinutes: number;
  resolutionMinutes: number;
}

/** Matches Crm.Api SlaPolicyResponse. */
export interface SlaPolicy {
  isConfigured: boolean;
  isEnabled: boolean;
  targets: SlaTarget[];
  updatedAtUtc: string | null;
  updatedByName: string | null;
}

export interface UpdateSlaPolicyRequest {
  isEnabled: boolean;
  targets: SlaTarget[];
}

export interface Holiday {
  /** 'yyyy-MM-dd' */
  date: string;
  name: string;
}

/** Matches Crm.Api BusinessCalendarResponse. Times are 'HH:mm:ss'. */
export interface BusinessCalendar {
  isConfigured: boolean;
  timeZoneId: string | null;
  workingDays: WeekDay[];
  opensAt: string | null;
  closesAt: string | null;
  holidays: Holiday[];
  updatedAtUtc: string | null;
  updatedByName: string | null;
}

export interface UpdateBusinessCalendarRequest {
  timeZoneId: string;
  workingDays: WeekDay[];
  opensAt: string;
  closesAt: string;
  holidays: Holiday[];
}

export interface SlaDuePreview {
  startUtc: string;
  minutes: number;
  dueAtUtc: string;
  timeZoneId: string;
}

/** businessMinutesRemaining: positive = left, negative = overdue, null once completed or without a calendar. */
export interface SlaTargetView {
  kind: SlaKind;
  targetMinutes: number;
  dueAtUtc: string;
  completedAtUtc: string | null;
  completedByName: string | null;
  state: SlaTargetState;
  isWarning: boolean;
  businessMinutesRemaining: number | null;
  businessMinutesElapsed: number;
}

/** Matches Crm.Api TicketSlaResponse; null on a ticket = not tracked. */
export interface TicketSla {
  indicator: SlaIndicator;
  timeZoneId: string;
  firstResponse: SlaTargetView;
  resolution: SlaTargetView;
}

/** Matches Crm.Api AutomationRuleResponse. */
export interface AutomationRule {
  id: string;
  name: string;
  isEnabled: boolean;
  enabledAtUtc: string | null;
  trigger: AutomationTrigger;
  action: AutomationAction;
  sortOrder: number;
  categoryCondition: TicketCategory | null;
  priorityCondition: TicketPriority | null;
  assignedCondition: boolean | null;
  slaKindCondition: SlaKind | null;
  targetAgentUserId: string | null;
  targetAgentName: string | null;
  targetAgentIsActive: boolean | null;
  warningLeadMinutes: number | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export type AutomationRuleRequest = Omit<
  AutomationRule,
  'id' | 'enabledAtUtc' | 'targetAgentName' | 'targetAgentIsActive' | 'createdAtUtc' | 'updatedAtUtc'
>;

export interface AutomationExecution {
  id: number;
  ruleId: string;
  ruleName: string;
  ticketId: string;
  ticketSubject: string;
  slaKind: SlaKind | null;
  trigger: AutomationTrigger;
  action: AutomationAction;
  outcome: AutomationOutcome;
  detail: string | null;
  occurredAtUtc: string;
}

export interface SlaOverviewTicket {
  ticketId: string;
  subject: string;
  status: TicketStatus;
  assignedAgentName: string | null;
  sla: TicketSla;
}

export interface SlaOverview {
  warningCount: number;
  breachedCount: number;
  onTrackCount: number;
  warning: SlaOverviewTicket[];
  breached: SlaOverviewTicket[];
  recentExecutions: AutomationExecution[];
  generatedAtUtc: string;
}

export function slaIndicatorLabel(indicator: SlaIndicator): string {
  switch (indicator) {
    case 'NotTracked':
      return 'Not tracked';
    case 'OnTrack':
      return 'On track';
    default:
      return indicator;
  }
}

export function slaIndicatorTone(indicator: SlaIndicator): StatusTone {
  switch (indicator) {
    case 'Breached':
      return 'error';
    case 'Warning':
      return 'pending';
    case 'Met':
      return 'success';
    default:
      return 'neutral';
  }
}

export function slaKindLabel(kind: SlaKind): string {
  return kind === 'FirstResponse' ? 'First response' : 'Resolution';
}

/** 135 → '2 h 15 min'; 45 → '45 min'; 120 → '2 h'. Callers handle the overdue sign. */
export function formatBusinessMinutes(minutes: number): string {
  const total = Math.max(0, Math.round(minutes));
  const hours = Math.floor(total / 60);
  const rest = total % 60;
  if (hours === 0) {
    return `${rest} min`;
  }
  return rest === 0 ? `${hours} h` : `${hours} h ${rest} min`;
}
