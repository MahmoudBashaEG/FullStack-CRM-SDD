import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from '../../core/api/problem-details';

export type AiErrorKind =
  | 'UsageLimit'
  | 'QuotaExceeded'
  | 'NotConfigured'
  | 'InvalidCredentials'
  | 'ModelUnavailable'
  | 'Timeout'
  | 'ProviderUnavailable'
  | 'Blocked'
  | 'InvalidResponse';

/** AI calls only. Unlike describeHttpError, keeps the server's 429 detail (usage limit vs provider quota differ). */
export function describeAiError(error: HttpErrorResponse, fallback: string): string {
  if (error.status === 0) {
    return 'The API is not reachable. Start the backend and try again.';
  }
  const problem = error.error as (ProblemDetails & { aiError?: AiErrorKind }) | null;
  return problem?.detail ?? problem?.title ?? fallback;
}

export function aiErrorKind(error: HttpErrorResponse): AiErrorKind | null {
  return (error.error as { aiError?: AiErrorKind } | null)?.aiError ?? null;
}
