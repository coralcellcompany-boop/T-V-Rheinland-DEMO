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

  list(filters: { state?: number; search?: string; page?: number; pageSize?: number } = {})
    : Observable<PagedResult<StickerListItem>> {
    let p = new HttpParams();
    if (filters.state != null) p = p.set('state', String(filters.state));
    if (filters.search) p = p.set('search', filters.search);
    if (filters.page) p = p.set('page', String(filters.page));
    if (filters.pageSize) p = p.set('pageSize', String(filters.pageSize));
    return this.http.get<PagedResult<StickerListItem>>(this.base, { params: p });
  }

  stockSummary(): Observable<StickerStockSummary> {
    return this.http.get<StickerStockSummary>(`${this.base}/stock-summary`);
  }

  procure(count: number): Observable<{ added: number }> {
    return this.http.post<{ added: number }>(`${this.base}/procure`, { count });
  }

  void(id: string, reason: string): Observable<StickerListItem> {
    return this.http.post<StickerListItem>(`${this.base}/${id}/void`, { reason });
  }
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
}
