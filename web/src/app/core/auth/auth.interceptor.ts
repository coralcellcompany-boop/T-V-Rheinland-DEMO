import {
  HttpErrorResponse,
  HttpEvent,
  HttpHandlerFn,
  HttpInterceptorFn,
  HttpRequest,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/**
 * Attaches the JWT to outbound requests. On 401, attempts a one-shot refresh and replays the
 * original request; if refresh fails or the retry also returns 401, logs out and redirects to
 * /login. The X-Retry header guards against infinite refresh loops when the backend rejects
 * the freshly-minted token (which would otherwise trap the page in an endless spinner).
 */
const RETRY_HEADER = 'X-Auth-Retry';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const token = auth.accessToken();
  const authed = token ? withBearer(req, token) : req;

  return next(authed).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status !== 401 || isAuthEndpoint(req) || req.headers.has(RETRY_HEADER)) {
        // 401 on a retried request means the new token was also rejected — bail out.
        if (err.status === 401 && req.headers.has(RETRY_HEADER)) {
          auth.logout();
          router.navigate(['/login']);
        }
        return throwError(() => err);
      }
      return refreshAndRetry(auth, router, req, next);
    })
  );
};

function refreshAndRetry(
  auth: AuthService,
  router: Router,
  req: HttpRequest<unknown>,
  next: HttpHandlerFn
): Observable<HttpEvent<unknown>> {
  return auth.refresh().pipe(
    switchMap((res) => {
      const retried = req.clone({
        setHeaders: { Authorization: `Bearer ${res.accessToken}`, [RETRY_HEADER]: '1' },
      });
      return next(retried);
    }),
    catchError((refreshErr) => {
      auth.logout();
      router.navigate(['/login']);
      return throwError(() => refreshErr);
    })
  );
}

function withBearer<T>(req: HttpRequest<T>, token: string): HttpRequest<T> {
  return req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}

function isAuthEndpoint(req: HttpRequest<unknown>): boolean {
  return req.url.endsWith('/api/auth/login') || req.url.endsWith('/api/auth/refresh');
}
