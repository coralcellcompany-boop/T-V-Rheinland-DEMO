import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, OnChanges, SimpleChanges, inject, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { FilesApi } from '../../core/api/files.api';
import { NotifyService } from '../services/notify.service';
import { showHttpError } from '../services/api-error.handler';

/**
 * Single-image uploader with thumbnail preview. Emits the storage key when an upload
 * succeeds; the parent persists that key on the entity it owns.
 */
@Component({
  selector: 'tuv-photo-uploader',
  standalone: true,
  imports: [CommonModule, ButtonModule],
  template: `
    <div class="wrap">
      <div class="thumb">
        @if (previewUrl()) {
          <img [src]="previewUrl()!" alt="Preview" />
          <button type="button" class="remove" (click)="clear()" *ngIf="!disabled" title="Remove">
            <i class="pi pi-times"></i>
          </button>
        } @else {
          <div class="placeholder"><i class="pi pi-image"></i></div>
        }
      </div>
      <div class="actions">
        <input type="file" accept="image/png,image/jpeg,image/webp" #file
          (change)="onPick($event)" [disabled]="disabled || uploading()" />
        <small *ngIf="hint">{{ hint }}</small>
      </div>
    </div>
  `,
  styles: [
    `
      :host { display: block; }
      .wrap { display: flex; gap: 1rem; align-items: flex-start; }
      .thumb {
        width: 120px; height: 120px; border-radius: 12px;
        background: #f1f5f9; border: 1px dashed #cbd5e1;
        display: flex; align-items: center; justify-content: center;
        position: relative; overflow: hidden;
      }
      .thumb img { width: 100%; height: 100%; object-fit: cover; }
      .thumb .pi-image { font-size: 2rem; color: #94a3b8; }
      .placeholder { color: #94a3b8; }
      .remove {
        position: absolute; top: 4px; right: 4px;
        background: rgba(15, 23, 42, 0.65); color: #fff;
        border: 0; border-radius: 50%; width: 22px; height: 22px;
        cursor: pointer; line-height: 1;
      }
      .actions { display: flex; flex-direction: column; gap: 0.4rem; flex: 1; }
      .actions small { color: #94a3b8; font-size: 0.75rem; }
    `,
  ],
})
export class PhotoUploader implements OnChanges {
  private filesApi = inject(FilesApi);
  private notify = inject(NotifyService);

  @Input() photoKey: string | null = null;
  @Input() disabled = false;
  @Input() hint = 'PNG, JPEG, WebP. Max 20 MB.';
  @Output() photoKeyChange = new EventEmitter<string | null>();

  protected previewUrl = signal<string | null>(null);
  protected uploading = signal(false);

  ngOnChanges(c: SimpleChanges) {
    if (c['photoKey']) this.refreshPreview();
  }

  private async refreshPreview() {
    if (!this.photoKey) { this.previewUrl.set(null); return; }
    try {
      const url = await this.filesApi.fetchAsObjectUrl(this.photoKey);
      this.previewUrl.set(url);
    } catch { this.previewUrl.set(null); }
  }

  async onPick(e: Event) {
    const input = e.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.uploading.set(true);
    this.filesApi.upload(file).subscribe({
      next: (res) => {
        this.uploading.set(false);
        this.photoKey = res.key;
        this.photoKeyChange.emit(res.key);
        this.refreshPreview();
        this.notify.success('Photo uploaded.');
        input.value = '';
      },
      error: (err) => {
        this.uploading.set(false);
        showHttpError(this.notify, err);
        input.value = '';
      },
    });
  }

  clear() {
    this.photoKey = null;
    this.photoKeyChange.emit(null);
    this.previewUrl.set(null);
  }
}
