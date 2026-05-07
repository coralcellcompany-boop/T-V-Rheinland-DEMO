import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/common.models';
import {
  ApprovalQueueCounts,
  CertificateDetail,
  CertificateListItem,
  CertificateTrigger,
  CreateCertificateRequest,
  DashboardKpis,
  RecentActivityItem,
  UpdateCertificateRequest,
} from '../models/certificate.models';

@Injectable({ providedIn: 'root' })
export class CertificatesApi {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/certificates`;

  list(filters: {
    clientId?: string;
    equipmentId?: string;
    jobOrderId?: string;
    state?: number;
    inspectionType?: number;
    result?: number;
    search?: string;
    page?: number;
    pageSize?: number;
  } = {}): Observable<PagedResult<CertificateListItem>> {
    let p = new HttpParams();
    Object.entries(filters).forEach(([k, v]) => { if (v !== undefined && v !== null && v !== '') p = p.set(k, String(v)); });
    return this.http.get<PagedResult<CertificateListItem>>(this.base, { params: p });
  }

  get(id: string): Observable<CertificateDetail> {
    return this.http.get<CertificateDetail>(`${this.base}/${id}`);
  }

  create(body: CreateCertificateRequest): Observable<CertificateDetail> {
    return this.http.post<CertificateDetail>(this.base, body);
  }

  update(id: string, body: UpdateCertificateRequest): Observable<CertificateDetail> {
    return this.http.put<CertificateDetail>(`${this.base}/${id}`, body);
  }

  transition(id: string, trigger: CertificateTrigger, comments?: string): Observable<CertificateDetail> {
    return this.http.post<CertificateDetail>(
      `${this.base}/${id}/transitions/${trigger}`,
      { comments: comments ?? null });
  }

  pdf(id: string): Observable<Blob> {
    return this.http.get(`${this.base}/${id}/pdf`, { responseType: 'blob' });
  }
}

@Injectable({ providedIn: 'root' })
export class ApprovalsApi {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/approvals`;

  counts(): Observable<ApprovalQueueCounts> {
    return this.http.get<ApprovalQueueCounts>(`${this.base}/counts`);
  }

  list(bucket: 'pending' | 'rejected' | 'mine', page = 1, pageSize = 25)
    : Observable<PagedResult<CertificateListItem>> {
    const p = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    return this.http.get<PagedResult<CertificateListItem>>(`${this.base}/${bucket}`, { params: p });
  }
}

@Injectable({ providedIn: 'root' })
export class DashboardApi {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/dashboard`;

  kpis(clientId?: string): Observable<DashboardKpis> {
    let p = new HttpParams();
    if (clientId) p = p.set('clientId', clientId);
    return this.http.get<DashboardKpis>(`${this.base}/kpis`, { params: p });
  }

  activity(limit = 12): Observable<RecentActivityItem[]> {
    return this.http.get<RecentActivityItem[]>(`${this.base}/activity?limit=${limit}`);
  }
}
