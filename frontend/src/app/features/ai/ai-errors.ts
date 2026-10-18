import { HttpErrorResponse } from '@angular/common/http';
import { translateServerMessage } from '../../core/i18n/server-messages';
import { Translate, englishT } from '../../core/i18n/translate-fn';
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

const FIXED_KINDS: readonly AiErrorKind[] = [
  'NotConfigured', 'InvalidCredentials', 'ModelUnavailable', 'QuotaExceeded', 'Timeout', 'ProviderUnavailable', 'Blocked', 'InvalidResponse',
];
const USAGE_LIMIT = /^You can make (\d+) AI requests per minute\. Wait about (\d+) seconds and try again\.$/;

/** AI calls only. Unlike describeHttpError, keeps the server's 429 detail (usage limit vs provider quota differ). Known fixed AI messages are translated; anything else is shown verbatim. */
export function describeAiError(error: HttpErrorResponse, fallback: string, t: Translate = englishT): string {
  if (error.status === 0) {
    return t('errors.unreachable');
  }
  const problem = error.error as (ProblemDetails & { aiError?: AiErrorKind }) | null;
  const kind = problem?.aiError;
  if (kind && FIXED_KINDS.includes(kind)) {
    return t(`ai.errors.${kind}`);
  }
  const usage = problem?.detail ? USAGE_LIMIT.exec(problem.detail) : null;
  if (usage) {
    return t('ai.errors.UsageLimit', { limit: usage[1], seconds: usage[2] });
  }
  const text = problem?.detail ?? problem?.title;
  return text ? translateServerMessage(text, t) : fallback;
}

export function aiErrorKind(error: HttpErrorResponse): AiErrorKind | null {
  return (error.error as { aiError?: AiErrorKind } | null)?.aiError ?? null;
}
