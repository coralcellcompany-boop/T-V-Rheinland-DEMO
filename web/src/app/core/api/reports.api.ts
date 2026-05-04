import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface MonthlyStatsRow {
  period: string;
  totalCertificates: number;
  approved: number;
  rejected: number;
  inProgress: number;
}

export interface InspectorProductivityRow {
  inspectorId: string;
  inspectorName: string;
  certificatesCreated: number;
  certificatesApproved: number;
  dwrEntries: number;
  totalHours: number;
}

export interface DueSoonRow {
  certificateNo: string;
  clientId: string;
  clientName: string;
  equipmentIdNo: string;
  equipmentTypeName: string;
  nextDueDate: string;
  daysUntilDue: number;
}

export interface OverdueRow {
  certificateNo: string;
  clientId: string;
  clientName: string;
  equipmentIdNo: string;
  nextDueDate: string;
  daysOverdue: number;
}

@Injectable({ providedIn: 'root' })
export class ReportsApi {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/reports`;

  monthly(months = 6): Observable<MonthlyStatsRow[]> {
    return this.http.get<MonthlyStatsRow[]>(`${this.base}/monthly-stats?months=${months}`);
  }
  productivity(days = 30): Observable<InspectorProductivityRow[]> {
    return this.http.get<InspectorProductivityRow[]>(`${this.base}/inspector-productivity?days=${days}`);
  }
  dueSoon(days = 30): Observable<DueSoonRow[]> {
    return this.http.get<DueSoonRow[]>(`${this.base}/due-soon?days=${days}`);
  }
  overdue(): Observable<OverdueRow[]> {
    return this.http.get<OverdueRow[]>(`${this.base}/overdue`);
  }
  aramcoWeekly(cutoff?: string, clientId?: string): Observable<Blob> {
    let p = new HttpParams();
    if (cutoff) p = p.set('cutoff', cutoff);
    if (clientId) p = p.set('clientId', clientId);
    return this.http.get(`${this.base}/aramco-weekly`, { params: p, responseType: 'blob' });
  }
}
