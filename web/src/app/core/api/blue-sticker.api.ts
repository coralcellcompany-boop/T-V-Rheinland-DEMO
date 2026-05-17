import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/common.models';
import {
  BlueStickerReportDetail, BlueStickerReportListItem, BlueStickerTrigger,
  CreateBlueStickerReportsRequest, UpdateBlueStickerInspectionRequest,
} from '../models/blue-sticker.models';

@Injectable({ providedIn: 'root' })
export class BlueStickerApi {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/blue-sticker-reports`;

  list(filters: { jobOrderId?: string; state?: number; search?: string;
    page?: number; pageSize?: number } = {}): Observable<PagedResult<BlueStickerReportListItem>> {
    let p = new HttpParams();
    Object.entries(filters).forEach(([k, v]) => {
      if (v !== undefined && v !== null && v !== '') p = p.set(k, String(v));
    });
    return this.http.get<PagedResult<BlueStickerReportListItem>>(this.base, { params: p });
  }
  get(id: string): Observable<BlueStickerReportDetail> {
    return this.http.get<BlueStickerReportDetail>(`${this.base}/${id}`);
  }
  create(body: CreateBlueStickerReportsRequest): Observable<BlueStickerReportDetail[]> {
    return this.http.post<BlueStickerReportDetail[]>(this.base, body);
  }
  updateInspection(id: string, body: UpdateBlueStickerInspectionRequest): Observable<BlueStickerReportDetail> {
    return this.http.put<BlueStickerReportDetail>(`${this.base}/${id}/inspection`, body);
  }
  transition(id: string, trigger: BlueStickerTrigger, comments?: string,
    inspectorSignaturePng?: string, technicalReviewerSignaturePng?: string): Observable<BlueStickerReportDetail> {
    return this.http.post<BlueStickerReportDetail>(
      `${this.base}/${id}/transitions/${trigger}`,
      { comments: comments ?? null,
        inspectorSignaturePng: inspectorSignaturePng ?? null,
        technicalReviewerSignaturePng: technicalReviewerSignaturePng ?? null });
  }
  requestOtp(id: string): Observable<BlueStickerReportDetail> {
    return this.http.post<BlueStickerReportDetail>(`${this.base}/${id}/request-otp`, {});
  }
  verifyAndSign(id: string, otp: string, receiverSignaturePng: string): Observable<BlueStickerReportDetail> {
    return this.http.post<BlueStickerReportDetail>(
      `${this.base}/${id}/verify-and-sign`, { otp, receiverSignaturePng });
  }
  pdf(id: string): Observable<Blob> {
    return this.http.get(`${this.base}/${id}/report.pdf`, { responseType: 'blob' });
  }
}
