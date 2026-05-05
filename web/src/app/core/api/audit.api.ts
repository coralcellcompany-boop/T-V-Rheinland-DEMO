import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/common.models';

export interface AuditLogRow {
  id: string;
  entityName: string;
  entityId: string;
  action: string;
  actorUserId: string | null;
  actorUserName: string | null;
  actorRole: string | null;
  atUtc: string;
  ip: string | null;
  beforeJson: string | null;
  afterJson: string | null;
  previousHash: string;
  currentHash: string;
}

export interface AuditFilters {
  entityName?: string;
  entityId?: string;
  actorUserId?: string;
  fromUtc?: string;
  toUtc?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class AuditApi {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/audit`;

  list(f: AuditFilters = {}): Observable<PagedResult<AuditLogRow>> {
    let p = new HttpParams();
    Object.entries(f).forEach(([k, v]) => {
      if (v !== undefined && v !== null && v !== '') p = p.set(k, String(v));
    });
    return this.http.get<PagedResult<AuditLogRow>>(this.base, { params: p });
  }

  equipmentHistory(equipmentId: string, page = 1, pageSize = 50): Observable<PagedResult<AuditLogRow>> {
    const p = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    return this.http.get<PagedResult<AuditLogRow>>(
      `${environment.apiBaseUrl}/api/equipment/${equipmentId}/history`, { params: p });
  }
}
