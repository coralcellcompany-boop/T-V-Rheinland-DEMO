import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/common.models';
import {
  StickerListItem,
  StickerPublicView,
  StickerStockSummary,
} from '../models/sticker.models';

@Injectable({ providedIn: 'root' })
export class StickersApi {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/stickers`;
  private requestsBase = `${environment.apiBaseUrl}/api/sticker-requests`;

  list(filters: {
    state?: number; color?: number; assignedToInspectorId?: string;
    search?: string; page?: number; pageSize?: number;
  } = {}): Observable<PagedResult<StickerListItem>> {
    let p = new HttpParams();
    if (filters.state != null) p = p.set('state', String(filters.state));
    if (filters.color != null) p = p.set('color', String(filters.color));
    if (filters.assignedToInspectorId) p = p.set('assignedToInspectorId', filters.assignedToInspectorId);
    if (filters.search) p = p.set('search', filters.search);
    if (filters.page) p = p.set('page', String(filters.page));
    if (filters.pageSize) p = p.set('pageSize', String(filters.pageSize));
    return this.http.get<PagedResult<StickerListItem>>(this.base, { params: p });
  }

  stockSummary(): Observable<StickerStockSummary> {
    return this.http.get<StickerStockSummary>(`${this.base}/stock-summary`);
  }

  procure(count: number, color: number): Observable<{ added: number }> {
    return this.http.post<{ added: number }>(`${this.base}/procure`, { count, color });
  }

  void(id: string, reason: string): Observable<StickerListItem> {
    return this.http.post<StickerListItem>(`${this.base}/${id}/void`, { reason });
  }

  assign(inspectorUserId: string, color: number, count: number): Observable<{ assigned: number }> {
    return this.http.post<{ assigned: number }>(`${this.base}/assign`,
      { inspectorUserId, color, count });
  }

  printBatch(state: number, color?: number, max = 24): Observable<Blob> {
    let p = new HttpParams().set('state', String(state)).set('max', String(max));
    if (color != null) p = p.set('color', String(color));
    return this.http.get(`${this.base}/print-batch`, { params: p, responseType: 'blob' });
  }

  // ─── Requests ───
  listRequests(state?: number, inspectorUserId?: string, page = 1, pageSize = 25)
    : Observable<PagedResult<StickerRequest>> {
    let p = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    if (state != null) p = p.set('state', String(state));
    if (inspectorUserId) p = p.set('inspectorUserId', inspectorUserId);
    return this.http.get<PagedResult<StickerRequest>>(this.requestsBase, { params: p });
  }
  createRequest(body: { color: number; quantity: number; justification?: string | null })
    : Observable<StickerRequest> {
    return this.http.post<StickerRequest>(this.requestsBase, body);
  }
  approveRequest(id: string, comments?: string | null): Observable<StickerRequest> {
    return this.http.post<StickerRequest>(`${this.requestsBase}/${id}/approve`, { comments });
  }
  rejectRequest(id: string, reason: string): Observable<StickerRequest> {
    return this.http.post<StickerRequest>(`${this.requestsBase}/${id}/reject`, { reason });
  }
  cancelRequest(id: string): Observable<StickerRequest> {
    return this.http.post<StickerRequest>(`${this.requestsBase}/${id}/cancel`, {});
  }
}

export interface StickerRequest {
  id: string;
  requestNo: string;
  inspectorUserId: string;
  inspectorName: string | null;
  color: number;
  quantity: number;
  justification: string | null;
  state: number;
  decidedByUserId: string | null;
  decidedByName: string | null;
  decidedAtUtc: string | null;
  decisionComments: string | null;
  allocatedCount: number;
  createdAtUtc: string;
}

@Injectable({ providedIn: 'root' })
export class PublicStickerApi {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/public`;

  verify(stickerNo: string): Observable<StickerPublicView> {
    return this.http.get<StickerPublicView>(`${this.base}/stickers/${encodeURIComponent(stickerNo)}`);
  }

  qrUrl(stickerNo: string): string {
    return `${this.base}/qr/${encodeURIComponent(stickerNo)}.png`;
  }

  publicPdfUrl(stickerNo: string): string {
    return `${this.base}/stickers/${encodeURIComponent(stickerNo)}.pdf`;
  }
}
