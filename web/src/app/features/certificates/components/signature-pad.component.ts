import { CommonModule } from '@angular/common';
import {
  AfterViewInit, Component, ElementRef, EventEmitter, Input, OnDestroy, Output, ViewChild,
} from '@angular/core';
import { ButtonModule } from 'primeng/button';

/**
 * Canvas-based signature pad with mouse and touch support. Captures a PNG data-url and
 * emits it on save. The host owns whether the signature is persisted to the certificate
 * — this component is presentation only.
 */
@Component({
  selector: 'tuv-signature-pad',
  standalone: true,
  imports: [CommonModule, ButtonModule],
  template: `
    <div class="pad-wrap">
      <canvas #canvas
        [width]="width"
        [height]="height"
        (pointerdown)="onDown($event)"
        (pointermove)="onMove($event)"
        (pointerup)="onUp($event)"
        (pointercancel)="onUp($event)"
        (pointerleave)="onUp($event)"></canvas>
      <div class="empty-hint" *ngIf="empty">Sign here</div>
    </div>
    <div class="actions">
      <p-button icon="pi pi-refresh" severity="secondary" [text]="true" size="small"
        label="Clear" (onClick)="clear()" />
      <p-button icon="pi pi-check" size="small" label="Save signature"
        [disabled]="empty" (onClick)="commit()" />
    </div>
  `,
  styles: [
    `
      :host { display: block; }
      .pad-wrap {
        position: relative; width: 100%; aspect-ratio: 5 / 2;
        border-radius: 10px; border: 1.5px dashed #cbd5e1;
        background: #ffffff;
        background-image: linear-gradient(180deg, transparent 95%, #e5e9f2 95%);
        overflow: hidden;
        touch-action: none;
      }
      canvas {
        position: absolute; inset: 0; width: 100%; height: 100%;
        cursor: crosshair; display: block;
      }
      .empty-hint {
        position: absolute; inset: 0; display: flex; align-items: center; justify-content: center;
        color: #94a3b8; font-size: 0.85rem; pointer-events: none; letter-spacing: 0.04em;
      }
      .actions {
        display: flex; gap: 0.5rem; justify-content: flex-end;
        margin-top: 0.6rem;
      }
    `,
  ],
})
export class SignaturePad implements AfterViewInit, OnDestroy {
  @ViewChild('canvas', { static: true }) canvasRef!: ElementRef<HTMLCanvasElement>;
  @Input() width = 600;
  @Input() height = 240;
  @Output() commitSignature = new EventEmitter<string>();

  protected empty = true;

  private ctx?: CanvasRenderingContext2D;
  private drawing = false;
  private last = { x: 0, y: 0 };

  ngAfterViewInit() {
    const c = this.canvasRef.nativeElement;
    const ctx = c.getContext('2d');
    if (!ctx) return;
    this.ctx = ctx;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    ctx.lineWidth = 2.2;
    ctx.strokeStyle = '#0f172a';
  }

  ngOnDestroy() { this.drawing = false; }

  onDown(e: PointerEvent) {
    if (!this.ctx) return;
    e.preventDefault();
    this.drawing = true;
    this.last = this.eventPoint(e);
    this.canvasRef.nativeElement.setPointerCapture(e.pointerId);
  }

  onMove(e: PointerEvent) {
    if (!this.drawing || !this.ctx) return;
    e.preventDefault();
    const p = this.eventPoint(e);
    this.ctx.beginPath();
    this.ctx.moveTo(this.last.x, this.last.y);
    this.ctx.lineTo(p.x, p.y);
    this.ctx.stroke();
    this.last = p;
    if (this.empty) this.empty = false;
  }

  onUp(e: PointerEvent) {
    if (!this.drawing) return;
    this.drawing = false;
    try { this.canvasRef.nativeElement.releasePointerCapture(e.pointerId); } catch { /* ignore */ }
  }

  private eventPoint(e: PointerEvent) {
    const rect = this.canvasRef.nativeElement.getBoundingClientRect();
    const sx = this.canvasRef.nativeElement.width / rect.width;
    const sy = this.canvasRef.nativeElement.height / rect.height;
    return { x: (e.clientX - rect.left) * sx, y: (e.clientY - rect.top) * sy };
  }

  clear() {
    if (!this.ctx) return;
    const c = this.canvasRef.nativeElement;
    this.ctx.clearRect(0, 0, c.width, c.height);
    this.empty = true;
  }

  commit() {
    if (this.empty) return;
    const dataUrl = this.canvasRef.nativeElement.toDataURL('image/png');
    this.commitSignature.emit(dataUrl);
  }
}
