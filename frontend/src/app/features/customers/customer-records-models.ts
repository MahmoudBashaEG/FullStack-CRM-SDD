import { HttpErrorResponse } from '@angular/common/http';
import { describeHttpError } from '../../core/api/describe-http-error';
import { ProblemDetails } from '../../core/api/problem-details';

export const CUSTOMER_INTERACTION_TYPES = ['PhoneCall', 'Meeting', 'Other'] as const;
export type CustomerInteractionType = (typeof CUSTOMER_INTERACTION_TYPES)[number];

export function interactionTypeLabel(type: CustomerInteractionType): string {
  return type === 'PhoneCall' ? 'Phone Call' : type;
}

/** Matches Crm.Api CustomerNoteResponse. */
export interface CustomerNote {
  id: number;
  authorUserId: string;
  authorName: string;
  body: string;
  createdAtUtc: string;
}

/** Matches Crm.Api CustomerInteractionResponse. */
export interface CustomerInteraction {
  id: number;
  type: CustomerInteractionType;
  summary: string;
  occurredAtUtc: string;
  recordedByUserId: string;
  recordedByName: string;
  createdAtUtc: string;
}

/** Matches Crm.Api CustomerAttachmentResponse (metadata only). */
export interface CustomerAttachment {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedByUserId: string;
  uploadedByName: string;
  uploadedAtUtc: string;
}

export interface LogCustomerInteractionRequest {
  type: CustomerInteractionType;
  summary: string;
  /** null = now (server time). */
  occurredAtUtc: string | null;
}

/** Usability pre-checks only; the API enforces the real rules (size, extension, content signature). */
export const ATTACHMENT_MAX_BYTES = 5 * 1024 * 1024;
export const ATTACHMENT_ACCEPT = '.pdf,.png,.jpg,.jpeg,.txt,.docx';

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

/** Prefers the first server validation message for `field` (400 ValidationProblemDetails), else describeHttpError. */
export function describeFieldError(error: HttpErrorResponse, field: string, fallback: string): string {
  const problem = error.error as ProblemDetails | null;
  return problem?.errors?.[field]?.[0] ?? describeHttpError(error, fallback);
}
