import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { LoginRequest, LoginResponse, UserProfile } from '../models/auth.models';
import { environment } from '../../../environments/environment';

const ACCESS_KEY = 'tuv.accessToken';
const REFRESH_KEY = 'tuv.refreshToken';
const USER_KEY = 'tuv.user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);

  private readonly _accessToken = signal<string | null>(localStorage.getItem(ACCESS_KEY));
  private readonly _user = signal<UserProfile | null>(this.readStoredUser());

  readonly accessToken = this._accessToken.asReadonly();
  readonly user = this._user.asReadonly();
  readonly isAuthenticated = computed(() => !!this._accessToken() && !!this._user());
  readonly roles = computed(() => this._user()?.roles ?? []);

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>(`${environment.apiBaseUrl}/api/auth/login`, request)
      .pipe(tap((res) => this.persist(res)));
  }

  refresh(): Observable<LoginResponse> {
    const token = localStorage.getItem(REFRESH_KEY) ?? '';
    return this.http
      .post<LoginResponse>(`${environment.apiBaseUrl}/api/auth/refresh`, { refreshToken: token })
      .pipe(tap((res) => this.persist(res)));
  }

  logout(): void {
    localStorage.removeItem(ACCESS_KEY);
    localStorage.removeItem(REFRESH_KEY);
    localStorage.removeItem(USER_KEY);
    this._accessToken.set(null);
    this._user.set(null);
  }

  hasRole(role: string): boolean {
    return this.roles().includes(role);
  }

  hasAnyRole(roles: readonly string[]): boolean {
    return roles.some((r) => this.hasRole(r));
  }

  private persist(res: LoginResponse): void {
    localStorage.setItem(ACCESS_KEY, res.accessToken);
    localStorage.setItem(REFRESH_KEY, res.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify(res.user));
    this._accessToken.set(res.accessToken);
    this._user.set(res.user);
  }

  private readStoredUser(): UserProfile | null {
    const raw = localStorage.getItem(USER_KEY);
    return raw ? (JSON.parse(raw) as UserProfile) : null;
  }
}
