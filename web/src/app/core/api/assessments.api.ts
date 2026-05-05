import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/common.models';
import {
  AssessmentDetail,
  AssessmentListItem,
  AssessmentTrigger,
  CandidateDetail,
  CandidateListItem,
  CompetencyCardListItem,
  CompetencyCardPublicView,
  CreateAssessmentRequest,
  CreateCandidateRequest,
  UpdateAssessmentRequest,
  UpdateCandidateRequest,
} from '../models/assessment.models';

@Injectable({ providedIn: 'root' })
export class CandidatesApi {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/candidates`;

  list(filters: { clientId?: string; search?: string; page?: number; pageSize?: number } = {})
    : Observable<PagedResult<CandidateListItem>> {
    let p = new HttpParams();
    if (filters.clientId) p = p.set('clientId', filters.clientId);
    if (filters.search) p = p.set('search', filters.search);
    if (filters.page) p = p.set('page', String(filters.page));
    if (filters.pageSize) p = p.set('pageSize', String(filters.pageSize));
    return this.http.get<PagedResult<CandidateListItem>>(this.base, { params: p });
  }
  get(id: string): Observable<CandidateDetail> { return this.http.get<CandidateDetail>(`${this.base}/${id}`); }
  create(body: CreateCandidateRequest): Observable<CandidateDetail> { return this.http.post<CandidateDetail>(this.base, body); }
  update(id: string, body: UpdateCandidateRequest): Observable<CandidateDetail> { return this.http.put<CandidateDetail>(`${this.base}/${id}`, body); }

  import(clientId: string, file: File): Observable<{ imported: number; skipped: number; errors: string[] }> {
    const fd = new FormData();
    fd.append('file', file);
    return this.http.post<{ imported: number; skipped: number; errors: string[] }>(
      `${this.base}/import?clientId=${encodeURIComponent(clientId)}`, fd);
  }
}

@Injectable({ providedIn: 'root' })
export class AssessmentsApi {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/assessments`;

  list(filters: {
    candidateId?: string; clientId?: string; state?: number; category?: number;
    search?: string; page?: number; pageSize?: number;
  } = {}): Observable<PagedResult<AssessmentListItem>> {
    let p = new HttpParams();
    Object.entries(filters).forEach(([k, v]) => { if (v !== undefined && v !== null && v !== '') p = p.set(k, String(v)); });
    return this.http.get<PagedResult<AssessmentListItem>>(this.base, { params: p });
  }
  get(id: string): Observable<AssessmentDetail> { return this.http.get<AssessmentDetail>(`${this.base}/${id}`); }
  create(body: CreateAssessmentRequest): Observable<AssessmentDetail> { return this.http.post<AssessmentDetail>(this.base, body); }
  update(id: string, body: UpdateAssessmentRequest): Observable<AssessmentDetail> { return this.http.put<AssessmentDetail>(`${this.base}/${id}`, body); }
  transition(id: string, trigger: AssessmentTrigger, comments?: string): Observable<AssessmentDetail> {
    return this.http.post<AssessmentDetail>(`${this.base}/${id}/transitions/${trigger}`, { comments: comments ?? null });
  }
}

@Injectable({ providedIn: 'root' })
export class CompetencyCardsApi {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/cards`;

  list(filters: { clientId?: string; candidateId?: string; state?: number; search?: string; page?: number; pageSize?: number } = {})
    : Observable<PagedResult<CompetencyCardListItem>> {
    let p = new HttpParams();
    Object.entries(filters).forEach(([k, v]) => { if (v !== undefined && v !== null && v !== '') p = p.set(k, String(v)); });
    return this.http.get<PagedResult<CompetencyCardListItem>>(this.base, { params: p });
  }

  pdf(id: string): Observable<Blob> {
    return this.http.get(`${this.base}/${id}/pdf`, { responseType: 'blob' });
  }
}

@Injectable({ providedIn: 'root' })
export class PublicCardApi {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/public`;

  verify(cardNo: string): Observable<CompetencyCardPublicView> {
    return this.http.get<CompetencyCardPublicView>(`${this.base}/cards/${encodeURIComponent(cardNo)}`);
  }
  qrUrl(cardNo: string): string { return `${this.base}/qr/cards/${encodeURIComponent(cardNo)}.png`; }
  publicPdfUrl(cardNo: string): string { return `${this.base}/cards/${encodeURIComponent(cardNo)}.pdf`; }
}
