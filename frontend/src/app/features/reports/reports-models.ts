import type { TicketCategory, TicketPriority, TicketStatus } from '../tickets/ticket-models';

export const REPORT_PRESETS = ['Last7Days', 'Last30Days', 'Last90Days', 'Custom'] as const;
export type ReportPreset = (typeof REPORT_PRESETS)[number];
export const DEFAULT_PRESET: ReportPreset = 'Last30Days';

/** Dictionary keys of the preset labels (reports.preset.*). */
export const PRESET_LABEL_KEYS: Readonly<Record<ReportPreset, string>> = {
  Last7Days: 'reports.preset.Last7Days',
  Last30Days: 'reports.preset.Last30Days',
  Last90Days: 'reports.preset.Last90Days',
  Custom: 'reports.preset.Custom',
};

/** What travels in the URL and to the API. from/to (yyyy-MM-dd) are only meaningful for Custom. */
export interface ReportPeriodParams {
  preset: ReportPreset;
  from: string | null;
  to: string | null;
}

/** Matches Crm.Api ReportPeriodResponse. UTC calendar days, from..to inclusive (from/to are date-only yyyy-MM-dd). */
export interface ReportPeriod {
  preset: ReportPreset;
  from: string;
  to: string;
  startUtc: string;
  endExclusiveUtc: string;
  bucketDays: number;
  days: number;
}

export interface ReportCount<T> {
  value: T;
  count: number;
}

export interface ReportTrendPoint {
  start: string;
  end: string;
  count: number;
}

/** Matches Crm.Api TicketReportResponse. */
export interface TicketReport {
  period: ReportPeriod;
  createdTickets: number;
  byStatus: ReportCount<TicketStatus>[];
  byCategory: ReportCount<TicketCategory>[];
  byPriority: ReportCount<TicketPriority>[];
  trend: ReportTrendPoint[];
  generatedAtUtc: string;
}

/** compliancePercent null = no decided target (N/A). */
export interface SlaTargetSummary {
  cohortTickets: number;
  notApplicable: number;
  applicable: number;
  met: number;
  breached: number;
  pending: number;
  compliancePercent: number | null;
}

export interface SlaTrendBucket {
  start: string;
  end: string;
  responseMet: number;
  responseBreached: number;
  responseCompliancePercent: number | null;
  resolutionMet: number;
  resolutionBreached: number;
  resolutionCompliancePercent: number | null;
}

/** Live — not affected by the period. */
export interface SlaLiveStatus {
  activeTicketsWithBreach: number;
  responseAtRisk: number;
  resolutionAtRisk: number;
  scannedActiveTickets: number;
  scanLimitReached: boolean;
  enabledWarningRules: number;
  calendarConfigured: boolean;
}

export interface SlaReport {
  period: ReportPeriod;
  firstResponse: SlaTargetSummary;
  resolution: SlaTargetSummary;
  trend: SlaTrendBucket[];
  live: SlaLiveStatus;
  generatedAtUtc: string;
}

export interface AgentPerformanceRow {
  agentUserId: string;
  name: string;
  isActive: boolean;
  open: number;
  inProgress: number;
  activeAssigned: number;
  overdueActive: number;
  resolved: number;
  closed: number;
  activeByPriority: ReportCount<TicketPriority>[];
  resolutionActions: number;
  resolvedTicketsDistinct: number;
}

export interface AgentPerformanceTotals {
  openTickets: number;
  inProgressTickets: number;
  activeAssignedToAgents: number;
  overdueActiveAssignedToAgents: number;
  unassignedActive: number;
  resolutionActionsByAgents: number;
  resolutionActionsByOthers: number;
}

export interface AgentPerformanceReport {
  period: ReportPeriod;
  agents: AgentPerformanceRow[];
  totals: AgentPerformanceTotals;
  generatedAtUtc: string;
}

export interface RecentFeedback {
  ticketId: string;
  ticketSubject: string;
  rating: number;
  comment: string | null;
  submittedAtUtc: string;
  updatedAtUtc: string;
}

export interface CustomerSatisfactionReport {
  period: ReportPeriod;
  feedbackCount: number;
  averageRating: number | null;
  positiveCount: number;
  positivePercent: number | null;
  distribution: ReportCount<number>[];
  participation: { eligibleTickets: number; ticketsWithFeedback: number; percent: number | null };
  recent: RecentFeedback[];
  recentLimit: number;
  generatedAtUtc: string;
}
