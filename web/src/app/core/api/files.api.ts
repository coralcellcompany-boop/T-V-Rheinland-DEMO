import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface UploadedFile {
  key: string;
  fileName: string;
  contentType: string;
  size: number;
}

@Injectable({ providedIn: 'root' })
export class FilesApi {
  private http = inject(HttpClient);
  private base = `${environment.apiBaseUrl}/api/files`;

  upload(file: File): Observable<UploadedFile> {
    const fd = new FormData();
    fd.append('file', file);
    return this.http.post<UploadedFile>(`${this.base}/upload`, fd);
  }

  /**
   * URL for the stored file. The endpoint requires auth so we cannot use this
   * directly as an `<img src>` — use fetchAsObjectUrl() to fetch with the bearer
   * token then create an object URL.
   */
  url(key: string): string { return `${this.base}/${encodeURIComponent(key)}`; }

  fetchAsObjectUrl(key: string): Promise<string> {
    return this.http.get(this.url(key), { responseType: 'blob' }).toPromise().then((blob) => {
      if (!blob) throw new Error('Empty response');
      return URL.createObjectURL(blob);
    });
  }
}
