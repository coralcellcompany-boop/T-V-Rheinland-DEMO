import { CommonModule, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { DialogModule } from 'primeng/dialog';
import { TooltipModule } from 'primeng/tooltip';

import { AuditApi, AuditLogRow } from '../../../core/api/audit.api';
import { CertificatesApi } from '../../../core/api/certificates.api';
import { EquipmentApi } from '../../../core/api/equipment.api';
import {
  CertificateListItem,
  CertificateStateName,
  InspectionResultLabel,
} from '../../../core/models/certificate.models';
import { EquipmentDetail } from '../../../core/models/equipment.models';
import { PageHeader } from '../../../shared/components/page-header.component';
import { StatusPill } from '../../../shared/components/status-pill.component';
import { EmptyState } from '../../../shared/components/empty-state.component';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';

/**
 * Equipment history — combines two data sources for a single piece of equipment:
 *   1. The chronological list of certificates issued against it (from /api/certificates).
 *   2. The hash-chained audit log entries that touched the equipment record itself.
 * Together they form the cradle-to-grave story expected by an ISO 17020 auditor.
 */
@Component({
  selector: 'tuv-equipment-history',
  standalone: true,
  imports: [
    CommonModule, FormsModule, DatePipe, RouterLink,
    ButtonModule, TableModule, TagModule, DialogModule, TooltipModule,
    PageHeader, StatusPill, EmptyState,
  ],
  template: `
    @if (loadingEquipment()) {
      <div class="loader">Loading…</div>
    } @else if (equipment(); as e) {
      <tuv-page-header [title]="'History · ' + e.idNo"
        icon="pi-history"
        [subtitle]="e.equipmentTypeName + ' · ' + e.clientName">
        <a routerLink="/equipment" class="back"><i class="pi pi-arrow-left"></i> Back to equipment</a>
      </tuv-page-header>
    }

    <section class="card">
      <h3>Certificates issued</h3>
      @if (loadingCerts()) {
        <div class="loader">Loading certificates…</div>
      } @else if (certs().length === 0) {
        <tuv-empty-state icon="pi-file" title="No certificates"
          message="No inspection certificates have been issued against this equipment yet." />
      } @else {
        <p-table [value]="certs()" [rowHover]="true" styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th style="width: 14%">Certificate</th>
              <th style="width: 12%">State</th>
              <th style="width: 10%">Type</th>
              <th style="width: 10%">Result</th>
              <th>Inspection</th>
              <th>Next due</th>
              <th style="width: 80px"></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-c>
            <tr>
              <td><code>{{ c.certificateNo }}</code></td>
              <td><tuv-status-pill [value]="stateName(c.state)" /></td>
              <td>{{ inspectionTypeShort(c.inspectionType) }}</td>
              <td>
                <span [class]="'result result-' + c.result">{{ resultLabel(c.result) }}</span>
              </td>
              <td>{{ c.inspectionDate | date: 'dd MMM yyyy' }}</td>
              <td>{{ c.nextDueDate ? (c.nextDueDate | date: 'dd MMM yyyy') : '—' }}</td>
              <td>
                <a [routerLink]="['/certificates', c.id]">
                  <p-button icon="pi pi-arrow-up-right" [text]="true" rounded size="small"
                    pTooltip="Open certificate" />
                </a>
              </td>
            </tr>
          </ng-template>
        </p-table>

        <div class="diff-bar" *ngIf="certs().length >= 2">
          <span class="hint">Compare two certificates</span>
          <select [(ngModel)]="diffA" (change)="onDiffSelectChange()">
            <option [ngValue]="null">earlier…</option>
            <option *ngFor="let c of certs()" [ngValue]="c.id">{{ c.certificateNo }}</option>
          </select>
          <span>vs</span>
          <select [(ngModel)]="diffB" (change)="onDiffSelectChange()">
            <option [ngValue]="null">later…</option>
            <option *ngFor="let c of certs()" [ngValue]="c.id">{{ c.certificateNo }}</option>
          </select>
          <p-button label="Compare" icon="pi pi-arrows-h" [disabled]="!canDiff()"
            (onClick)="goDiff()" />
        </div>
      }
    </section>

    <section class="card">
      <h3>Audit trail</h3>
      <p class="muted">Hash-chained ISO 17020 audit rows that touched this equipment record.</p>
      @if (loadingAudit()) {
        <div class="loader">Loading audit trail…</div>
      } @else if (audit().length === 0) {
        <tuv-empty-state icon="pi-shield" title="No audit rows"
          message="No edits have been made to this equipment record yet." />
      } @else {
        <ul class="timeline">
          @for (a of audit(); track a.id) {
            <li>
              <div class="dot" [attr.data-action]="a.action"></div>
              <div class="body">
                <header>
                  <strong>{{ a.action }}</strong>
                  <span class="muted">·</span>
                  <span class="muted">{{ a.atUtc | date: 'dd MMM yyyy HH:mm' }} UTC</span>
                  <span class="muted" *ngIf="a.actorUserName">·</span>
                  <span *ngIf="a.actorUserName">{{ a.actorUserName }}</span>
                  <span class="muted" *ngIf="a.actorRole">({{ a.actorRole }})</span>
                </header>
              </div>
            </li>
          }
        </ul>
      }
    </section>
  `,
  styles: [
    `
      :host { display: block; }
      .loader { padding: 2rem; text-align: center; color: #64748b; }
      .back { color: #1d4ed8; text-decoration: none; font-size: 0.85rem; }
      .back:hover { text-decoration: underline; }
      .card { background: #fff; border-radius: 14px; border: 1px solid #e5e9f2; padding: 1.2rem 1.4rem; margin-bottom: 1rem; }
      .card h3 { margin: 0 0 0.85rem; }
      .muted { color: #94a3b8; font-size: 0.78rem; }
      code { font-family: ui-monospace, Menlo, monospace; font-weight: 600; color: #0f172a; }
      .result { padding: 0.15rem 0.5rem; border-radius: 999px; font-size: 0.78rem; font-weight: 600; background: #f1f5f9; color: #475569; }
      .result-1 { background: #dcfce7; color: #047857; }
      .result-2 { background: #fee2e2; color: #b91c1c; }
      .result-3 { background: #fef3c7; color: #b45309; }

      .diff-bar { display: flex; gap: 0.5rem; align-items: center; margin-top: 1rem; flex-wrap: wrap; }
      .diff-bar .hint { color: #64748b; font-size: 0.85rem; margin-right: 0.4rem; }
      .diff-bar select {
        padding: 0.45rem 0.6rem; border-radius: 8px; border: 1px solid #cbd5e1;
        background: #fff; font-family: ui-monospace, Menlo, monospace; font-size: 0.85rem;
      }

      .timeline { list-style: none; padding: 0; margin: 0; }
      .timeline li { display: grid; grid-template-columns: 16px 1fr; gap: 0.6rem; padding: 0.4rem 0; border-top: 1px dashed #f1f5f9; }
      .timeline li:first-child { border-top: 0; }
      .timeline .dot { width: 10px; height: 10px; border-radius: 50%; margin-top: 6px; background: #cbd5e1; }
      .timeline .dot[data-action='Create'] { background: #10b981; }
      .timeline .dot[data-action^='Update'] { background: #3b82f6; }
      .timeline .dot[data-action^='Delete'] { background: #ef4444; }
      .timeline .dot[data-action*='Transition'] { background: #f59e0b; }
      .timeline header { display: flex; gap: 0.4rem; align-items: center; flex-wrap: wrap; font-size: 0.85rem; }
    `,
  ],
})
export class EquipmentHistoryPage implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private auditApi = inject(AuditApi);
  private certsApi = inject(CertificatesApi);
  private equipmentApi = inject(EquipmentApi);
  private notify = inject(NotifyService);

  protected loadingEquipment = signal(true);
  protected loadingCerts = signal(true);
  protected loadingAudit = signal(true);

  protected equipment = signal<EquipmentDetail | null>(null);
  protected certs = signal<CertificateListItem[]>([]);
  protected audit = signal<AuditLogRow[]>([]);

  protected diffA: string | null = null;
  protected diffB: string | null = null;

  protected stateName = (s: number) => CertificateStateName[s] ?? 'Unknown';
  protected resultLabel = (r: number) => InspectionResultLabel[r] ?? '—';
  protected inspectionTypeShort = (t: number) => ['—', 'P.I.', 'Re.I.', 'I.I.'][t] ?? '—';

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) { this.router.navigate(['/equipment']); return; }

    this.equipmentApi.get(id).subscribe({
      next: (e) => { this.equipment.set(e); this.loadingEquipment.set(false); },
      error: (err) => { this.loadingEquipment.set(false); showHttpError(this.notify, err); },
    });

    this.certsApi.list({ equipmentId: id, page: 1, pageSize: 100 }).subscribe({
      next: (r) => {
        const sorted = [...r.items].sort((a, b) =>
          (a.inspectionDate < b.inspectionDate ? -1 : a.inspectionDate > b.inspectionDate ? 1 : 0));
        this.certs.set(sorted);
        this.loadingCerts.set(false);
      },
      error: (err) => { this.loadingCerts.set(false); showHttpError(this.notify, err); },
    });

    this.auditApi.equipmentHistory(id).subscribe({
      next: (r) => { this.audit.set(r.items); this.loadingAudit.set(false); },
      error: (err) => {
        // Silently ignore — non-Manager users get 403 here, but the certs section is still useful.
        this.loadingAudit.set(false);
        this.audit.set([]);
      },
    });
  }

  onDiffSelectChange() { /* binding only */ }

  canDiff(): boolean {
    return !!this.diffA && !!this.diffB && this.diffA !== this.diffB;
  }

  goDiff() {
    if (!this.canDiff()) return;
    this.router.navigate(['/certificates', this.diffB, 'diff'], { queryParams: { vs: this.diffA } });
  }
}
