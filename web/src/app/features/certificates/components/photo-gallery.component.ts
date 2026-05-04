import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, OnChanges, SimpleChanges, inject, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { FilesApi } from '../../../core/api/files.api';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';

export interface CertPhoto {
  key: string;
  fileName: string;
  contentType: string;
  caption?: string;
}

/**
 * Multi-photo gallery for inspection certificates. Stores a JSON array of file
 * references on the certificate's PhotosJson column. Loads thumbnails by fetching
 * the bytes through the auth-required /api/files/{key} endpoint.
 */
@Component({
  selector: 'tuv-photo-gallery',
  standalone: true,
  imports: [CommonModule, ButtonModule],
  template: `
    <div class="bar">
      <div class="count">{{ photos().length }} photo{{ photos().length === 1 ? '' : 's' }}</div>
      @if (!readonly) {
        <input type="file" accept="image/png,image/jpeg,image/webp"
          (change)="onPick($event)" [disabled]="uploading()" />
      }
    </div>

    <div class="grid" *ngIf="photos().length > 0">
      @for (p of photos(); track p.key; let i = $index) {
        <figure class="tile">
          <img [src]="urls()[i] ?? ''" [alt]="p.fileName" />
          <figcaption>{{ p.fileName }}</figcaption>
          @if (!readonly) {
            <button type="button" class="remove" (click)="remove(i)" title="Remove">
              <i class="pi pi-times"></i>
            </button>
          }
        </figure>
      }
    </div>

    <div class="empty" *ngIf="photos().length === 0 && readonly">
      No photos attached.
    </div>
  `,
  styles: [
    `
      :host { display: block; }
      .bar { display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.6rem; }
      .count { font-size: 0.78rem; color: #64748b; font-weight: 500; }
      .grid {
        display: grid; grid-template-columns: repeat(auto-fill, minmax(140px, 1fr)); gap: 0.6rem;
      }
      .tile {
        position: relative; margin: 0; border-radius: 10px; overflow: hidden;
        background: #f1f5f9; border: 1px solid #e5e9f2; aspect-ratio: 4/3;
      }
      .tile img { width: 100%; height: 100%; object-fit: cover; }
      .tile figcaption {
        position: absolute; left: 0; right: 0; bottom: 0;
        background: linear-gradient(180deg, transparent, rgba(15, 23, 42, 0.85));
        color: #fff; font-size: 0.72rem; padding: 0.3rem 0.5rem;
        white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
      }
      .remove {
        position: absolute; top: 4px; right: 4px;
        background: rgba(15, 23, 42, 0.7); color: #fff;
        border: 0; border-radius: 50%; width: 22px; height: 22px;
        cursor: pointer;
      }
      .empty { color: #94a3b8; padding: 0.5rem 0; font-style: italic; }
    `,
  ],
})
export class PhotoGallery implements OnChanges {
  private filesApi = inject(FilesApi);
  private notify = inject(NotifyService);

  @Input() value: string | null = null;       // PhotosJson string
  @Input() readonly = false;
  @Output() valueChange = new EventEmitter<string>();

  protected photos = signal<CertPhoto[]>([]);
  protected urls = signal<(string | null)[]>([]);
  protected uploading = signal(false);

  ngOnChanges(c: SimpleChanges) { if (c['value']) this.parse(this.value); }

  private parse(json: string | null) {
    if (!json) { this.photos.set([]); this.urls.set([]); return; }
    try {
      const arr = JSON.parse(json);
      const list = Array.isArray(arr) ? arr.filter((x) => x && x.key) : [];
      this.photos.set(list);
      this.refreshUrls();
    } catch { this.photos.set([]); this.urls.set([]); }
  }

  private async refreshUrls() {
    const list = this.photos();
    const urls = await Promise.all(list.map(async (p) => {
      try { return await this.filesApi.fetchAsObjectUrl(p.key); }
      catch { return null; }
    }));
    this.urls.set(urls);
  }

  onPick(e: Event) {
    const input = e.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.uploading.set(true);
    this.filesApi.upload(file).subscribe({
      next: (res) => {
        this.uploading.set(false);
        const next = [...this.photos(), {
          key: res.key, fileName: res.fileName, contentType: res.contentType,
        }];
        this.photos.set(next);
        this.refreshUrls();
        this.emit();
        input.value = '';
      },
      error: (err) => { this.uploading.set(false); showHttpError(this.notify, err); input.value = ''; },
    });
  }

  remove(i: number) {
    const next = this.photos().filter((_, idx) => idx !== i);
    this.photos.set(next);
    this.refreshUrls();
    this.emit();
  }

  private emit() { this.valueChange.emit(JSON.stringify(this.photos())); }
}
