import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/common.models';
import {
  CreateDwrRequest, CreateJobOrderRequest, CreateJobRequestRequest, CreateSurveyRequest,
  DwrListItem, JobOrderDetail, JobOrderListItem, JobRequestDetail, JobRequestListItem,
  SurveyListItem, UpdateJobOrderRequest,
} from '../models/job-management.models';

function p(filters: Record<string, any>): HttpParams {
  let h = new HttpParams();
  Object.entries(filters).forEach(([k, v]) => {
    if (v !== undefined && v !== null && v !== '') h = h.set(k, String(v));
  });
  return h;
}

@Injectable({ providedIn: 'root' })
export class JobRequestsApi {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/job-requests`;
  list(f: { clientId?: string; status?: number; search?: string; page?: number; pageSize?: number } = {})
    : Observable<PagedResult<JobRequestListItem>> { return this.http.get<PagedResult<JobRequestListItem>>(this.base, { params: p(f) }); }
  get(id: string): Observable<JobRequestDetail> { return this.http.get<JobRequestDetail>(`${this.base}/${id}`); }
  create(body: CreateJobRequestRequest): Observable<JobRequestDetail> { return this.http.post<JobRequestDetail>(this.base, body); }
  accept(id: string): Observable<JobRequestDetail> { return this.http.post<JobRequestDetail>(`${this.base}/${id}/accept`, {}); }
  reject(id: string, reason: string): Observable<JobRequestDetail> { return this.http.post<JobRequestDetail>(`${this.base}/${id}/reject`, { reason }); }
  convert(id: string): Observable<JobOrderDetail> { return this.http.post<JobOrderDetail>(`${this.base}/${id}/convert`, {}); }
}

@Injectable({ providedIn: 'root' })
export class JobOrdersApi {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/job-orders`;
  list(f: { clientId?: string; status?: number; search?: string; page?: number; pageSize?: number } = {})
    : Observable<PagedResult<JobOrderListItem>> { return this.http.get<PagedResult<JobOrderListItem>>(this.base, { params: p(f) }); }
  get(id: string): Observable<JobOrderDetail> { return this.http.get<JobOrderDetail>(`${this.base}/${id}`); }
  create(body: CreateJobOrderRequest): Observable<JobOrderDetail> { return this.http.post<JobOrderDetail>(this.base, body); }
  update(id: string, body: UpdateJobOrderRequest): Observable<JobOrderDetail> { return this.http.put<JobOrderDetail>(`${this.base}/${id}`, body); }
}

@Injectable({ providedIn: 'root' })
export class DwrApi {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/dwr`;
  list(f: { jobOrderId?: string; inspectorId?: string; status?: number;
    dateFrom?: string; dateTo?: string; search?: string; page?: number; pageSize?: number } = {})
    : Observable<PagedResult<DwrListItem>> { return this.http.get<PagedResult<DwrListItem>>(this.base, { params: p(f) }); }
  create(body: CreateDwrRequest): Observable<any> { return this.http.post(this.base, body); }
  submit(id: string): Observable<any> { return this.http.post(`${this.base}/${id}/submit`, {}); }
  approve(id: string): Observable<any> { return this.http.post(`${this.base}/${id}/approve`, {}); }
  reject(id: string, reason: string): Observable<any> { return this.http.post(`${this.base}/${id}/reject`, { reason }); }
}

@Injectable({ providedIn: 'root' })
export class SurveysApi {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/surveys`;
  list(f: { clientId?: string; status?: number; search?: string; page?: number; pageSize?: number } = {})
    : Observable<PagedResult<SurveyListItem>> { return this.http.get<PagedResult<SurveyListItem>>(this.base, { params: p(f) }); }
  create(body: CreateSurveyRequest): Observable<any> { return this.http.post(this.base, body); }
  submit(id: string): Observable<any> { return this.http.post(`${this.base}/${id}/submit`, {}); }
}
