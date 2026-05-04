import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateUserRequest,
  UpdateUserRequest,
  UserListItem,
} from '../models/user.models';

@Injectable({ providedIn: 'root' })
export class UsersApi {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/admin/users`;

  list(search?: string): Observable<UserListItem[]> {
    let p = new HttpParams();
    if (search) p = p.set('search', search);
    return this.http.get<UserListItem[]>(this.base, { params: p });
  }

  get(id: string): Observable<UserListItem> {
    return this.http.get<UserListItem>(`${this.base}/${id}`);
  }

  create(body: CreateUserRequest): Observable<UserListItem> {
    return this.http.post<UserListItem>(this.base, body);
  }

  update(id: string, body: UpdateUserRequest): Observable<UserListItem> {
    return this.http.put<UserListItem>(`${this.base}/${id}`, body);
  }

  resetPassword(id: string, newPassword: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/reset-password`, { newPassword });
  }

  roles(): Observable<string[]> {
    return this.http.get<string[]>(`${this.base}/roles`);
  }
}
