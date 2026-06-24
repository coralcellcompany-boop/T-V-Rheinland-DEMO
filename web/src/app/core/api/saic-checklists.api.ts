import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface SaicChecklistItem {
  itemNo: string;
  acceptanceCriteria: string;
  referenceStandard: string;
  sectionNo: string | null;
  sectionTitle: string | null;
}

export interface SaicChecklist {
  saicNumber: string;
  title: string;
  items: SaicChecklistItem[];
}

@Injectable({ providedIn: 'root' })
export class SaicChecklistsApi {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/saic-checklists`;

  /** Resolve the checklist for a category + equipment type. Emits null (204) when unmapped. */
  resolve(category: string, equipmentType: string): Observable<SaicChecklist | null> {
    const p = new HttpParams().set('category', category).set('equipmentType', equipmentType);
    return this.http.get<SaicChecklist | null>(`${this.base}/resolve`, { params: p });
  }
}
