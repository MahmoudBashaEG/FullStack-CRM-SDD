import { Translate, englishT } from '../../core/i18n/translate-fn';
import { StatusTone } from '../../shared/ui/status-badge/status-badge';

export type KnowledgeArticleType = 'Faq' | 'HelpArticle' | 'Solution' | 'Guide';
export type KnowledgeArticleStatus = 'Draft' | 'ReadyForPublication' | 'Published' | 'Archived';
export type KnowledgeArticleVisibility = 'Internal' | 'Public';

export const ARTICLE_TYPES: readonly KnowledgeArticleType[] = ['Faq', 'HelpArticle', 'Solution', 'Guide'];
export const ARTICLE_VISIBILITIES: readonly KnowledgeArticleVisibility[] = ['Internal', 'Public'];

const STATUS_TONES: Record<KnowledgeArticleStatus, StatusTone> = {
  Draft: 'neutral', ReadyForPublication: 'pending', Published: 'success', Archived: 'neutral',
};

export const articleTypeLabel = (type: KnowledgeArticleType, t: Translate = englishT) => t(`kb.types.${type}`);
export const articleStatusLabel = (status: KnowledgeArticleStatus, t: Translate = englishT) => t(`kb.status.${status}`);
export const articleStatusTone = (s: KnowledgeArticleStatus) => STATUS_TONES[s];

/** Matches Crm.Api KnowledgeCategoryResponse. */
export interface KnowledgeCategory { id: string; name: string; isActive: boolean; updatedAtUtc: string; }

/** Matches Crm.Api KnowledgeArticleSummaryResponse (no body). */
export interface KnowledgeArticleSummary {
  id: string; title: string; summary: string | null; type: KnowledgeArticleType; categoryId: string; categoryName: string;
  visibility: KnowledgeArticleVisibility; status: KnowledgeArticleStatus; authorUserId: string; authorName: string;
  updatedAtUtc: string; publishedAtUtc: string | null;
}

/** Matches Crm.Api KnowledgeArticleResponse. canEdit / canSubmit are display hints; the API re-checks. */
export interface KnowledgeArticle extends KnowledgeArticleSummary {
  body: string; categoryIsActive: boolean; updatedByName: string; createdAtUtc: string; submittedAtUtc: string | null;
  returnReason: string | null; publishedByName: string | null; archivedAtUtc: string | null; canEdit: boolean; canSubmit: boolean;
}

/** Matches Crm.Api TicketKnowledgeLinkResponse — metadata only, never the body. */
export interface TicketKnowledgeLink {
  id: number; ticketId: string; articleId: string; articleTitle: string; articleType: KnowledgeArticleType; categoryName: string;
  articleStatus: KnowledgeArticleStatus; linkedByName: string; linkedAtUtc: string;
}

export interface KnowledgeArticleListParams {
  page: number; pageSize: number; search: string | null; type: KnowledgeArticleType | null; categoryId: string | null;
  status: KnowledgeArticleStatus | null; mine: boolean;
}

/** No status / author fields: those come from workflow endpoints and the token. */
export interface SaveKnowledgeArticleRequest {
  title: string; summary: string | null; body: string; type: KnowledgeArticleType; categoryId: string; visibility: KnowledgeArticleVisibility;
}

export const KB_COPY_HINT = 'Copies the article’s Markdown text only — nothing is sent to the customer.';
export const PUBLIC_VISIBILITY_HINT = 'Published + Public articles appear in the Customer Help Center for signed-in customers.';
export const KB_LIMITS = { title: 200, summary: 500, body: 20000, categoryName: 100, returnReason: 500 } as const;
