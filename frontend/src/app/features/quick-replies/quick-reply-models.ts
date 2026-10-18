import { TicketCategory } from '../tickets/ticket-models';

export type QuickReplyScope = 'Personal' | 'Shared';

/** Matches Crm.Api QuickReplyResponse. */
export interface QuickReply {
  id: string;
  title: string;
  body: string;
  category: TicketCategory;
  scope: QuickReplyScope;
  ownerUserId: string | null;
  createdByName: string;
  updatedByName: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  isArchived: boolean;
  canEdit: boolean;
}

export interface QuickReplyListParams {
  page: number;
  pageSize: number;
  search: string | null;
  category: TicketCategory | null;
  scope: QuickReplyScope | null;
  archived: boolean;
}

/** No scope or owner: both come from the caller's role on the server. */
export interface SaveQuickReplyRequest {
  title: string;
  body: string;
  category: TicketCategory;
}

export const QUICK_REPLY_COPY_HINT = 'Copies text only — does not send a message.';
