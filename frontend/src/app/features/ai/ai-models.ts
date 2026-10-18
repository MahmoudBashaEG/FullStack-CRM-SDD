import { TicketCategory } from '../tickets/ticket-models';

/** Must match Crm.Application.Ai enums (serialized by name). */
export const AI_REPLY_TONES = ['Professional', 'Friendly', 'Empathetic', 'Concise'] as const;
export type AiReplyTone = (typeof AI_REPLY_TONES)[number];
export type AiChatRole = 'User' | 'Assistant';

/** Mirrors the server default Ai:MaxChatMessages; the server trims anyway. */
export const AI_MAX_CHAT_MESSAGES = 20;
export const AI_CHAT_MESSAGE_MAX_LENGTH = 2000; // UI cap for typed messages (server hard cap 4000)
export const AI_REPLY_INSTRUCTION_MAX_LENGTH = 500;
export const AI_REVIEW_LABEL = 'AI-generated — review before use.';

export interface AiText {
  text: string;
  generatedAtUtc: string;
}

export interface AiCategorySuggestion {
  ticketId: string;
  currentCategory: TicketCategory;
  suggestedCategory: TicketCategory;
  explanation: string;
  matchesCurrent: boolean;
  generatedAtUtc: string;
}

export interface AiSource {
  articleId: string;
  title: string;
}

export interface AiKnowledgeRecommendation {
  articleId: string;
  articleTitle: string;
  steps: string[];
}

export interface AiSolutions {
  knowledgeBaseRecommendations: AiKnowledgeRecommendation[];
  generalRecommendations: string[];
  articlesConsidered: number;
  generatedAtUtc: string;
}

export interface AiChatMessage {
  role: AiChatRole;
  text: string;
}

/** A displayed turn; sources only on Assistant turns. */
export interface AiChatTurn extends AiChatMessage {
  sources: AiSource[];
}

export interface AiChatRequest {
  ticketId: string | null;
  messages: AiChatMessage[];
}

export interface AiChatResponse {
  reply: string;
  sources: AiSource[];
  ticketId: string | null;
  generatedAtUtc: string;
}
