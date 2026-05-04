import { HttpErrorResponse } from '@angular/common/http';
import { NotifyService } from './notify.service';

/**
 * Maps an HttpErrorResponse to a user-facing toast with the right severity.
 * RFC7807 ProblemDetails-aware: prefers `detail`, falls back to `title`, then status text.
 */
export function showHttpError(notify: NotifyService, err: unknown, fallback = 'Request failed'): void {
  if (!(err instanceof HttpErrorResponse)) {
    notify.error(fallback);
    return;
  }
  const body = err.error;
  const detail =
    (body && typeof body === 'object' && (body.detail ?? body.title)) || err.statusText || fallback;

  if (err.status === 401) notify.warn('You must sign in.', 'Authentication required');
  else if (err.status === 403) notify.warn('You do not have permission for that action.', 'Forbidden');
  else if (err.status === 409) notify.warn(detail, 'Conflict');
  else if (err.status === 422 || err.status === 400) notify.warn(detail, 'Validation');
  else notify.error(detail);
}
