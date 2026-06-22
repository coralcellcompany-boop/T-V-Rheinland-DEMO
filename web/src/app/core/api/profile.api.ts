import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Profile } from '../models/user.models';

@Injectable({ providedIn: 'root' })
export class ProfileApi {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/profile`;

  me(): Observable<Profile> {
    return this.http.get<Profile>(`${this.base}/me`);
  }

  updateSignature(signaturePng: string): Observable<Profile> {
    return this.http.put<Profile>(`${this.base}/signature`, { signaturePng });
  }
}
