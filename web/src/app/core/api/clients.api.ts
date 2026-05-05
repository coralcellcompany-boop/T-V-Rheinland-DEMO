import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/common.models';
import {
  ClientDetail,
  ClientListItem,
  CreateClientRequest,
  UpdateClientRequest,
} from '../models/client.models';

@Injectable({ providedIn: 'root' })
export class ClientsApi {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/clients`;

  list(params: { search?: string; page?: number; pageSize?: number } = {}): Observable<PagedResult<ClientListItem>> {
    let p = new HttpParams();
    if (params.search) p = p.set('search', params.search);
    if (params.page) p = p.set('page', String(params.page));
    if (params.pageSize) p = p.set('pageSize', String(params.pageSize));
    return this.http.get<PagedResult<ClientListItem>>(this.base, { params: p });
  }

  get(id: string): Observable<ClientDetail> {
    return this.http.get<ClientDetail>(`${this.base}/${id}`);
  }

  create(body: CreateClientRequest): Observable<ClientDetail> {
    return this.http.post<ClientDetail>(this.base, body);
  }

  update(id: string, body: UpdateClientRequest): Observable<ClientDetail> {
    return this.http.put<ClientDetail>(`${this.base}/${id}`, body);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  import(file: File): Observable<{ imported: number; skipped: number; errors: string[] }> {
    const fd = new FormData();
    fd.append('file', file);
    return this.http.post<{ imported: number; skipped: number; errors: string[] }>(
      `${this.base}/import`, fd);
  }
}
