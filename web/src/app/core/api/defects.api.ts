import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface DefectCode {
  id: string;
  equipmentTypeId: string | null;
  equipmentTypeName: string | null;
  code: string;
  description: string;
  severity: string;
  isActive: boolean;
}

export interface CreateDefectCodeRequest {
  equipmentTypeId: string | null;
  code: string;
  description: string;
  severity: string;
}

export interface UpdateDefectCodeRequest {
  code: string;
  description: string;
  severity: string;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class DefectsApi {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/defects`;

  list(equipmentTypeId?: string | null, includeInactive = false): Observable<DefectCode[]> {
    let p = new HttpParams();
    if (equipmentTypeId) p = p.set('equipmentTypeId', equipmentTypeId);
    if (includeInactive) p = p.set('includeInactive', 'true');
    return this.http.get<DefectCode[]>(this.base, { params: p });
  }

  create(body: CreateDefectCodeRequest): Observable<DefectCode> {
    return this.http.post<DefectCode>(this.base, body);
  }

  update(id: string, body: UpdateDefectCodeRequest): Observable<DefectCode> {
    return this.http.put<DefectCode>(`${this.base}/${id}`, body);
  }
}
