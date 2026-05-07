import { CommonModule, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DialogModule } from 'primeng/dialog';
import { TextareaModule } from 'primeng/textarea';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TooltipModule } from 'primeng/tooltip';

import { PageHeader } from '../../../shared/components/page-header.component';
import { StatusPill } from '../../../shared/components/status-pill.component';
import { CertificatesApi } from '../../../core/api/certificates.api';
import {
  CertificateDetail,
  CertificateInspectionTypeLabel,
  CertificateStateName,
  CertificateTrigger,
  InspectionResultLabel,
  LoadTestKindLabel,
} from '../../../core/models/certificate.models';
import { AuthService } from '../../../core/auth/auth.service';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';
import { TransitionTimeline } from '../components/transition-timeline.component';
import { ChecklistEditor } from '../components/checklist-editor.component';
import { PhotoGallery } from '../components/photo-gallery.component';
import { SignaturesPanel } from '../components/signatures-panel.component';
import { AramcoFormComponent } from '../components/aramco-form.component';
import { PublicStickerApi } from '../../../core/api/stickers.api';
import { environment } from '../../../../environments/environment';
import {
  AvailableTransition,
  availableTransitions,
} from '../transition-rules';

@Component({
  selector: 'tuv-certificate-detail',
  standalone: true,
  imports: [
    CommonModule, FormsModule, DatePipe, RouterLink,
    ButtonModule, CardModule, DialogModule, TextareaModule,
    InputTextModule, SelectModule, TooltipModule,
    PageHeader, StatusPill, TransitionTimeline, ChecklistEditor, PhotoGallery,
    SignaturesPanel, AramcoFormComponent,
  ],
  template: `
    @if (loading()) {
      <div class="loader">Loading certificate…</div>
    } @else if (cert(); as c) {
      <tuv-page-header [title]="c.certificateNo" icon="pi-file-check"
        [subtitle]="'Equipment ' + c.equipmentIdNo + ' · ' + c.equipmentTypeName + ' · ' + c.clientName">
        <a class="back" routerLink="/certificates"><i class="pi pi-arrow-left"></i> Back to list</a>
        <a [routerLink]="['/equipment', c.equipmentId, 'history']">
          <p-button icon="pi pi-history" severity="secondary" [outlined]="true" label="Equipment history" />
        </a>
        <p-button *ngIf="previousCertId() as pid" icon="pi pi-arrows-h" severity="secondary"
          [outlined]="true" label="Compare with previous"
          (onClick)="goCompare(c.id, pid)" />
        <p-button icon="pi pi-file-pdf" severity="secondary" label="Download PDF"
          [loading]="downloadingPdf()" (onClick)="downloadPdf()" />
      </tuv-page-header>

      <div class="grid">
        <!-- Summary card -->
        <section class="summary card">
          <header>
            <tuv-status-pill [value]="stateName(c.state)" />
            <tuv-status-pill *ngIf="c.result" [value]="resultLabel(c.result)" />
          </header>

          <dl>
            <div><dt>Inspection date</dt><dd>{{ c.inspectionDate | date: 'dd MMM yyyy' }}</dd></div>
            <div><dt>Report issue</dt><dd>{{ c.reportIssueDate | date: 'dd MMM yyyy' }}</dd></div>
            <div><dt>Next due</dt><dd>{{ c.nextDueDate ? (c.nextDueDate | date: 'dd MMM yyyy') : '—' }}</dd></div>
            <div><dt>Inspection type</dt><dd>{{ inspectionTypeLabel(c.inspectionType) }}</dd></div>
            <div><dt>Load test</dt><dd>{{ loadTestLabel(c.loadTest) }}</dd></div>
            <div><dt>Sticker</dt><dd>{{ c.stickerNo ?? '—' }}</dd></div>
            <div class="span2"><dt>Standards</dt><dd>{{ c.standards ?? '—' }}</dd></div>
          </dl>

          <div *ngIf="c.stickerNo" class="sticker-block">
            <div class="qr-frame">
              <img [src]="qrUrl(c.stickerNo)" alt="Sticker QR" />
            </div>
            <div class="qr-meta">
              <strong>{{ c.stickerNo }}</strong>
              <span class="links">
                <a [href]="stickerPdfUrl(c.stickerNo)" target="_blank" rel="noopener">
                  <i class="pi pi-file-pdf"></i> Open verification PDF
                </a>
                · <a [href]="verifyUrl(c.stickerNo)" target="_blank" rel="noopener">verification page</a>
              </span>
              <span class="muted">Scan QR to open the PDF on a phone.</span>
            </div>
          </div>
        </section>

        <!-- Actions card -->
        <section class="actions card">
          <h3>Actions for this certificate</h3>
          @if (transitions().length === 0) {
            <p class="muted">No actions are available to you in the current state.</p>
          } @else {
            <div class="action-buttons">
              @for (t of transitions(); track t.trigger) {
                <p-button
                  [icon]="'pi ' + t.icon"
                  [label]="t.label"
                  [severity]="t.severity"
                  (onClick)="prepareTrigger(t)" />
              }
            </div>
          }

          <h4>State machine</h4>
          <ul class="legal-list">
            <li>Inspector → Submit · TechReviewer/Manager → Begin review / Reject</li>
            <li>TechReviewer → Advance for approval · Manager → Final approve · Manager → Void</li>
            <li>Manager/Coordinator → Send to client · Client → Accept / Raise issue</li>
          </ul>
        </section>

        <!-- Checklist -->
        <section class="checklist card">
          <header class="block-header">
            <h3>Inspection checklist</h3>
            <span class="muted" *ngIf="!isMutable()">
              <i class="pi pi-lock"></i>
              Read-only — certificate is in {{ stateName(c.state) }} state.
            </span>
          </header>
          <tuv-checklist-editor
            [value]="c.checklistJson"
            [equipmentTypeId]="c.equipmentTypeId"
            [readonly]="!isMutable()"
            (save)="saveChecklist($event)" />
        </section>

        <!-- Aramco Annex 1 — Blue Sticker only (Aramco-categorised equipment).
             Third Party Inspection certs use the per-equipment-type checklist above
             and don't need this section. -->
        @if (c.isBlueStickerCertificate) {
          <section class="aramco card">
            <header class="block-header">
              <h3>
                <i class="pi pi-tag" style="color: #0a64a4"></i>
                Aramco Blue Sticker report (Annex 1 · MS0053813)
                <span class="badge" *ngIf="c.equipmentAramcoCategory">{{ c.equipmentAramcoCategory }}</span>
              </h3>
              <span class="muted" *ngIf="!isMutable()">
                <i class="pi pi-lock"></i>
                Read-only — certificate is in {{ stateName(c.state) }} state.
              </span>
            </header>
            <tuv-aramco-form
              [value]="c.aramcoReportJson"
              [readonly]="!isMutable()"
              [canDownloadPdf]="true"
              [aramcoPdfUrl]="aramcoPdfUrl(c.id)"
              (save)="saveAramcoReport($event)" />
          </section>
        } @else {
          <section class="aramco card tpi-note">
            <h3><i class="pi pi-info-circle"></i> Third Party Inspection</h3>
            <p>This equipment isn't Aramco-categorised, so the Annex 1 / Blue Sticker
              report doesn't apply. Use the equipment-type checklist above to record
              the inspection.</p>
          </section>
        }

        <!-- Photos -->
        <section class="photos card">
          <header class="block-header">
            <h3>Photos</h3>
            <span class="muted" *ngIf="!isMutable()">
              <i class="pi pi-lock"></i>
              Read-only — certificate is in {{ stateName(c.state) }} state.
            </span>
          </header>
          <tuv-photo-gallery
            [value]="c.photosJson"
            [readonly]="!isMutable()"
            (valueChange)="savePhotos($event)" />
        </section>

        <!-- Signatures -->
        <section class="signatures card">
          <header class="block-header">
            <h3>Signatures</h3>
            <span class="muted" *ngIf="!isMutable()">
              <i class="pi pi-lock"></i>
              Read-only — certificate is in {{ stateName(c.state) }} state.
            </span>
          </header>
          <tuv-signatures-panel
            [value]="c.signaturesJson"
            [readonly]="!isMutable()"
            (valueChange)="saveSignatures($event)" />
        </section>

        <!-- Timeline -->
        <section class="timeline card">
          <h3>Lifecycle timeline</h3>
          <tuv-transition-timeline [transitions]="c.transitions" />
        </section>
      </div>

      <p-dialog [(visible)]="triggerDialog"
        [header]="pendingTrigger()?.label ?? ''"
        [modal]="true" [style]="{ width: '460px' }">
        <p>{{ pendingTrigger()?.description ?? confirmText() }}</p>
        @if (pendingTrigger()?.requireComments) {
          <label>Comments<span class="req">*</span></label>
          <textarea pTextarea rows="3" [(ngModel)]="comments" placeholder="Required for this transition"></textarea>
        } @else {
          <label>Comments (optional)</label>
          <textarea pTextarea rows="3" [(ngModel)]="comments"></textarea>
        }
        <ng-template pTemplate="footer">
          <p-button severity="secondary" label="Cancel" (onClick)="closeDialog()" [disabled]="firing()" />
          <p-button [label]="pendingTrigger()?.label ?? 'Confirm'"
            [severity]="pendingTrigger()?.severity"
            [loading]="firing()"
            [disabled]="!canFire()"
            (onClick)="fire()" />
        </ng-template>
      </p-dialog>
    }
  `,
  styles: [
    `
      :host { display: block; }
      .loader { padding: 3rem; text-align: center; color: #64748b; }
      .back { color: #1d4ed8; text-decoration: none; font-size: 0.85rem; }
      .back:hover { text-decoration: underline; }
      .grid {
        display: grid; grid-template-columns: 2fr 1fr; gap: 1rem;
        grid-template-areas: 'summary actions' 'checklist checklist' 'aramco aramco' 'photos photos' 'signatures signatures' 'timeline timeline';
      }
      .summary { grid-area: summary; }
      .actions { grid-area: actions; }
      .aramco { grid-area: aramco; }
      .aramco h3 .badge {
        display: inline-block; margin-left: 0.5rem;
        background: #0a64a4; color: #fff; font-size: 0.6rem;
        padding: 0.1rem 0.5rem; border-radius: 999px; vertical-align: middle;
      }
      .tpi-note { background: #f8fafc; }
      .tpi-note p { color: #475569; font-size: 0.88rem; }
      .checklist { grid-area: checklist; }
      .photos { grid-area: photos; }
      .signatures { grid-area: signatures; }
      .timeline { grid-area: timeline; }
      .block-header {
        display: flex; justify-content: space-between; align-items: baseline;
        margin-bottom: 0.85rem; gap: 0.6rem; flex-wrap: wrap;
      }
      .block-header h3 { margin: 0; }
      .block-header .muted {
        color: #94a3b8; font-size: 0.78rem;
        display: inline-flex; align-items: center; gap: 0.35rem;
      }
      .card {
        background: #fff; border: 1px solid #e5e9f2; border-radius: 14px;
        padding: 1.2rem 1.4rem;
      }
      .summary header { display: flex; gap: 0.5rem; margin-bottom: 1rem; }

      dl {
        display: grid; grid-template-columns: 1fr 1fr; gap: 0.85rem 1.5rem; margin: 0;
      }
      dl > div { display: flex; flex-direction: column; }
      dl .span2 { grid-column: 1 / -1; }
      dt { font-size: 0.72rem; color: #94a3b8; text-transform: uppercase; letter-spacing: 0.04em; }
      dd { margin: 0; font-weight: 500; color: #0f172a; font-size: 0.95rem; }

      .sticker-block {
        display: flex; align-items: center; gap: 1rem;
        margin-top: 1.2rem; padding: 0.85rem;
        background: linear-gradient(135deg, #eff6ff, #ffffff);
        border: 1px solid #bfdbfe; border-radius: 12px;
      }
      .qr-frame {
        width: 96px; height: 96px;
        background: #fff; border: 1px solid #cbd5e1; border-radius: 8px;
        display: flex; align-items: center; justify-content: center;
        padding: 6px;
      }
      .qr-frame img { width: 100%; height: 100%; image-rendering: pixelated; }
      .qr-meta { display: flex; flex-direction: column; gap: 0.2rem; }
      .qr-meta strong { font-family: ui-monospace, Menlo, monospace; color: #0f172a; }
      .qr-meta span, .qr-meta a { color: #1d4ed8; font-size: 0.85rem; }

      .actions h3, .timeline h3, .actions h4 { margin: 0 0 0.85rem 0; font-size: 0.95rem; color: #334155; }
      .actions h4 { margin-top: 1.4rem; font-size: 0.8rem; color: #64748b; }
      .action-buttons { display: flex; flex-direction: column; gap: 0.55rem; }
      .action-buttons :host ::ng-deep .p-button { width: 100%; justify-content: flex-start; }
      .legal-list { font-size: 0.78rem; color: #64748b; padding-left: 1.1rem; line-height: 1.55; margin: 0; }
      .muted { color: #94a3b8; }

      .req { color: #dc2626; margin-left: 0.15rem; }
      label { display: block; font-size: 0.85rem; font-weight: 500; color: #334155; margin: 0.6rem 0 0.3rem; }
      textarea { width: 100%; }

      @media (max-width: 1080px) {
        .grid { grid-template-columns: 1fr; grid-template-areas: 'summary' 'actions' 'checklist' 'aramco' 'photos' 'signatures' 'timeline'; }
      }
    `,
  ],
})
export class CertificateDetailPage implements OnInit {
  private api = inject(CertificatesApi);
  private stickersApi = inject(PublicStickerApi);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private notify = inject(NotifyService);
  protected auth = inject(AuthService);

  protected qrUrl = (no: string) => this.stickersApi.qrUrl(no);
  protected verifyUrl = (no: string) => `/verify/${encodeURIComponent(no)}`;
  protected stickerPdfUrl = (no: string) => this.stickersApi.publicPdfUrl(no);

  protected loading = signal(true);
  protected cert = signal<CertificateDetail | null>(null);
  protected previousCertId = signal<string | null>(null);

  protected transitions = computed(() => {
    const c = this.cert();
    if (!c) return [];
    return availableTransitions(c.state, this.auth.roles());
  });

  protected stateName = (s: number) => CertificateStateName[s] ?? 'Unknown';
  protected resultLabel = (r: number) => InspectionResultLabel[r];
  protected loadTestLabel = (l: number) => LoadTestKindLabel[l];
  protected inspectionTypeLabel = (t: number) => CertificateInspectionTypeLabel[t];

  // Mirrors the InspectionCertificate.EnsureMutable() rule on the backend.
  protected isMutable = computed(() => {
    const s = this.cert()?.state;
    return s === 0 /* Draft */ || s === 8 /* Rejected */;
  });

  protected triggerDialog = false;
  protected pendingTrigger = signal<AvailableTransition | null>(null);
  protected comments = '';
  protected firing = signal(false);
  protected downloadingPdf = signal(false);

  protected confirmText = computed(() => {
    const t = this.pendingTrigger();
    if (!t) return '';
    return `Are you sure you want to ${t.label.toLowerCase()} this certificate?`;
  });

  protected canFire = () => {
    const t = this.pendingTrigger();
    if (!t) return false;
    if (t.requireComments && !(this.comments?.trim())) return false;
    return !this.firing();
  };

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) { this.router.navigate(['/certificates']); return; }
    this.load(id);
  }

  private load(id: string) {
    this.loading.set(true);
    this.api.get(id).subscribe({
      next: (c) => {
        this.cert.set(c);
        this.loading.set(false);
        this.findPrevious(c);
      },
      error: (err) => {
        this.loading.set(false);
        showHttpError(this.notify, err);
        this.router.navigate(['/certificates']);
      },
    });
  }

  private findPrevious(c: CertificateDetail) {
    this.previousCertId.set(null);
    this.api.list({ equipmentId: c.equipmentId, page: 1, pageSize: 50 }).subscribe({
      next: (r) => {
        const sorted = [...r.items].sort((a, b) =>
          (a.inspectionDate < b.inspectionDate ? 1 : a.inspectionDate > b.inspectionDate ? -1 : 0));
        const idx = sorted.findIndex((x) => x.id === c.id);
        const prev = idx >= 0 ? sorted[idx + 1] : null;
        this.previousCertId.set(prev?.id ?? null);
      },
      error: () => { /* nav button just stays hidden */ },
    });
  }

  goCompare(later: string, earlier: string) {
    this.router.navigate(['/certificates', later, 'diff'], { queryParams: { vs: earlier } });
  }

  prepareTrigger(t: AvailableTransition) {
    this.pendingTrigger.set(t);
    this.comments = '';
    this.triggerDialog = true;
  }

  closeDialog() {
    this.triggerDialog = false;
    this.pendingTrigger.set(null);
    this.comments = '';
  }

  fire() {
    const c = this.cert();
    const t = this.pendingTrigger();
    if (!c || !t) return;
    this.firing.set(true);
    this.api.transition(c.id, t.trigger as CertificateTrigger, this.comments?.trim() || undefined).subscribe({
      next: (updated) => {
        this.firing.set(false);
        this.cert.set(updated);
        this.notify.success(`${t.label} succeeded — now ${this.stateName(updated.state)}.`);
        this.closeDialog();
      },
      error: (err) => {
        this.firing.set(false);
        showHttpError(this.notify, err);
      },
    });
  }

  downloadPdf() {
    const c = this.cert();
    if (!c) return;
    this.downloadingPdf.set(true);
    this.api.pdf(c.id).subscribe({
      next: (blob) => {
        this.downloadingPdf.set(false);
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${c.certificateNo}.pdf`;
        a.click();
        window.URL.revokeObjectURL(url);
      },
      error: (err) => {
        this.downloadingPdf.set(false);
        showHttpError(this.notify, err);
      },
    });
  }

  saveChecklist(json: string) {
    this.partialUpdate({ checklistJson: json }, 'Checklist saved.');
  }

  savePhotos(json: string) {
    this.partialUpdate({ photosJson: json }, 'Photos updated.');
  }

  saveSignatures(json: string) {
    this.partialUpdate({ signaturesJson: json }, 'Signatures updated.');
  }

  saveAramcoReport(json: string) {
    this.partialUpdate({ aramcoReportJson: json }, 'Annex 1 fields saved.');
  }

  aramcoPdfUrl(certId: string): string {
    return `${environment.apiBaseUrl}/api/certificates/${certId}/aramco-report`;
  }

  private partialUpdate(patch: Partial<{
    inspectionDate: string; reportIssueDate: string; nextDueDate: string | null;
    inspectionType: number; loadTest: number; result: number;
    standards: string | null; stickerNo: string | null;
    checklistJson: string | null; findingsJson: string | null;
    photosJson: string | null; signaturesJson: string | null;
    aramcoReportJson: string | null;
  }>, successMsg: string) {
    const c = this.cert();
    if (!c) return;
    this.api.update(c.id, {
      inspectionDate: c.inspectionDate,
      reportIssueDate: c.reportIssueDate,
      nextDueDate: c.nextDueDate,
      inspectionType: c.inspectionType,
      loadTest: c.loadTest,
      result: c.result,
      standards: c.standards,
      stickerNo: c.stickerNo,
      checklistJson: c.checklistJson,
      findingsJson: c.findingsJson,
      photosJson: c.photosJson,
      signaturesJson: c.signaturesJson,
      aramcoReportJson: c.aramcoReportJson,
      ...patch,
    }).subscribe({
      next: (updated) => { this.cert.set(updated); this.notify.success(successMsg); },
      error: (err) => showHttpError(this.notify, err),
    });
  }
}
