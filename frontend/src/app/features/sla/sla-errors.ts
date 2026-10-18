import { HttpErrorResponse } from '@angular/common/http';
import { describeHttpError } from '../../core/api/describe-http-error';
import { ProblemDetails } from '../../core/api/problem-details';

/** Server validation messages (400 ValidationProblemDetails) joined for display; falls back to the generic description. */
export function describeSaveError(error: HttpErrorResponse, fallback: string): string {
  const problem = error.error as ProblemDetails | null;
  const messages = problem?.errors ? Object.values(problem.errors).flat() : [];
  return messages.length > 0 ? [...new Set(messages)].join(' ') : describeHttpError(error, fallback);
}
