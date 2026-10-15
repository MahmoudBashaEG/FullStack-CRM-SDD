import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from './problem-details';

/** User-facing message for a failed API call; prefers the server's ProblemDetails detail. */
export function describeHttpError(error: HttpErrorResponse, fallback: string): string {
  if (error.status === 0) {
    return 'The API is not reachable. Start the backend and try again.';
  }
  if (error.status === 429) {
    return 'Too many attempts. Wait a minute and try again.';
  }
  const problem = error.error as ProblemDetails | null;
  return problem?.detail ?? problem?.title ?? fallback;
}
