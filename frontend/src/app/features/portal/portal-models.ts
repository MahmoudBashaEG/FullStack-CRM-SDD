import type { KnowledgeArticleType } from '../knowledge-base/knowledge-base-models';
import type { TicketCategory, TicketStatus } from '../tickets/ticket-models';

/** Matches Crm.Api PortalTicketSummaryResponse — customer-safe fields only. */
export interface PortalTicketSummary { id: string; subject: string; category: TicketCategory; status: TicketStatus; createdAtUtc: string; }

export interface PortalFeedback { rating: number; comment: string | null; createdAtUtc: string; updatedAtUtc: string; }

/** Matches Crm.Api PortalTicketResponse. canSubmitFeedback = current status Resolved or Closed (the API re-checks). */
export interface PortalTicket extends PortalTicketSummary { description: string; canSubmitFeedback: boolean; feedback: PortalFeedback | null; }

export type PortalTimelineEntryKind = 'Created' | 'StatusChanged';

/** Matches Crm.Api PortalTimelineEntryResponse. isRecorded false = derived from the ticket's creation time (older ticket). */
export interface PortalTimelineEntry {
  kind: PortalTimelineEntryKind; fromStatus: TicketStatus | null; status: TicketStatus; occurredAtUtc: string; isRecorded: boolean;
}

export interface PortalTicketListParams { page: number; pageSize: number; search: string | null; status: TicketStatus | null; }

/** CP2: exactly these three fields. */
export interface SubmitPortalTicketRequest { subject: string; description: string; category: TicketCategory; }

export interface SavePortalFeedbackRequest { rating: number; comment: string | null; }

export interface HelpCenterCategory { id: string; name: string; }

export interface HelpCenterArticleSummary {
  id: string; title: string; summary: string | null; type: KnowledgeArticleType; categoryId: string; categoryName: string; publishedAtUtc: string;
}

export interface HelpCenterArticle extends HelpCenterArticleSummary { body: string; updatedAtUtc: string; }

export interface HelpCenterListParams {
  page: number; pageSize: number; search: string | null; type: KnowledgeArticleType | null; categoryId: string | null;
}

export const PORTAL_LIMITS = { subject: 200, description: 4000, feedbackComment: 1000 } as const;
export const NOT_LINKED = 'Your account is not linked to a customer profile. Contact support.';
