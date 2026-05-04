import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/common.models';
import {
  CreateEquipmentRequest,
  EquipmentDetail,
  EquipmentImportResult,
  EquipmentListItem,
  EquipmentType,
  UpdateEquipmentRequest,
} from '../models/equipment.models';

@Injectable({ providedIn: 'root' })
export class EquipmentTypesApi {
  private http = inject(HttpClient);
  list(): Observable<EquipmentType[]> {
    return this.http.get<EquipmentType[]>(`${environment.apiBaseUrl}/api/equipment-types`);
  }
}

@Injectable({ providedIn: 'root' })
export class EquipmentApi {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/equipment`;

  list(filters: {
    clientId?: string;
    equipmentTypeId?: string;
    aramcoCategory?: number;
    status?: number;
    search?: string;
    page?: number;
    pageSize?: number;
  } = {}): Observable<PagedResult<EquipmentListItem>> {
    let p = new HttpParams();
    if (filters.clientId) p = p.set('clientId', filters.clientId);
    if (filters.equipmentTypeId) p = p.set('equipmentTypeId', filters.equipmentTypeId);
    if (filters.aramcoCategory != null) p = p.set('aramcoCategory', String(filters.aramcoCategory));
    if (filters.status != null) p = p.set('status', String(filters.status));
    if (filters.search) p = p.set('search', filters.search);
    if (filters.page) p = p.set('page', String(filters.page));
    if (filters.pageSize) p = p.set('pageSize', String(filters.pageSize));
    return this.http.get<PagedResult<EquipmentListItem>>(this.base, { params: p });
  }

  get(id: string): Observable<EquipmentDetail> {
    return this.http.get<EquipmentDetail>(`${this.base}/${id}`);
  }

  create(body: CreateEquipmentRequest): Observable<EquipmentDetail> {
    return this.http.post<EquipmentDetail>(this.base, body);
  }

  update(id: string, body: UpdateEquipmentRequest): Observable<EquipmentDetail> {
    return this.http.put<EquipmentDetail>(`${this.base}/${id}`, body);
  }

  import(clientId: string, file: File): Observable<EquipmentImportResult> {
    const fd = new FormData();
    fd.append('file', file);
    return this.http.post<EquipmentImportResult>(
      `${this.base}/import?clientId=${encodeURIComponent(clientId)}`, fd);
  }
}
