import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { DialogModule } from 'primeng/dialog';
import { TextareaModule } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';

import { PageHeader } from '../../shared/components/page-header.component';
import { StatusPill } from '../../shared/components/status-pill.component';
import { EmptyState } from '../../shared/components/empty-state.component';
import { KpiCard } from '../../shared/components/kpi-card.component';

import { CertificatesApi } from '../../core/api/certificates.api';
import {
  CertificateInspectionTypeLabel,
  CertificateListItem, CertificateState, CertificateStateName,
  InspectionResultLabel,
} from '../../core/models/certificate.models';
import { AuthService } from '../../core/auth/auth.service';
import { NotifyService } from '../../shared/services/notify.service';
import { showHttpError } from '../../shared/services/api-error.handler';

@Component({
  standalone: true,
  imports: [
    CommonModule, FormsModule, DatePipe,
    ButtonModule, TableModule, DialogModule, TextareaModule, TooltipModule,
    PageHeader, StatusPill, EmptyState, KpiCard,
  ],
  template: `
    <tuv-page-header
      title="My certificates"
      icon="pi-file-check"
      subtitle="Review and accept inspection certificates issued for your equipment by TÜV Rheinland Arabia.">
      <p-button icon="pi pi-refresh" severity="secondary" [text]="true"
        label="Refresh" (onClick)="reload()" />
    </tuv-page-header>

    <div class="kpis">
      <tuv-kpi-card label="Pending your acceptance" icon="pi-bell" tone="warn"
        [value]="pendingCount()" hint="Action required" />
      <tuv-kpi-card label="Accepted" icon="pi-check-circle" tone="positive"
        [value]="acceptedCount()" />
      <tuv-kpi-card label="Issues raised" icon="pi-flag" tone="danger"
        [value]="rejectedCount()" />
      <tuv-kpi-card label="Total received" icon="pi-inbox" tone="primary"
        [value]="totalCount()" />
    </div>

    <h2 class="section-title">Pending your action</h2>
    <div class="card">
      @if (loading()) { <div class="loader">Loading…</div> }
      @else if (pending().length === 0) {
        <tuv-empty-state icon="pi-check-circle" title="All caught up"
          message="No certificates are waiting for your acceptance right now." />
      } @else {
        <p-table [value]="pending()" [rowHover]="true" styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th>Certificate</th><th>Equipment</th>
              <th>Inspection date</th><th>Result</th>
              <th></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-c>
            <tr>
              <td>
                <div class="mono">{{ c.certificateNo }}</div>
                <div class="muted">{{ inspectionTypeLabel(c.inspectionType) }}</div>
              </td>
              <td>
                <div>{{ c.equipmentIdNo }}</div>
                <div class="muted">{{ c.equipmentTypeName }}</div>
              </td>
              <td>{{ c.inspectionDate | date: 'dd MMM yyyy' }}</td>
              <td><tuv-status-pill *ngIf="c.result" [value]="resultLabel(c.result)" /></td>
              <td class="actions">
                <p-button icon="pi pi-check" severity="success" label="Accept"
                  (onClick)="accept(c)" />
                <p-button icon="pi pi-flag" severity="danger" label="Raise issue"
                  [outlined]="true" (onClick)="openIssue(c)" />
              </td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>

    <h2 class="section-title">All your certificates</h2>
    <div class="card">
      @if (rows().length === 0) {
        <tuv-empty-state icon="pi-file-check" title="No certificates yet"
          message="Once TÜV inspects your equipment and sends a certificate, it will land here." />
      } @else {
        <p-table [value]="rows()" [rowHover]="true" styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th>Certificate</th><th>Equipment</th>
              <th>Inspection</th><th>Next due</th>
              <th>State</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-c>
            <tr (click)="open(c.id)" style="cursor: pointer">
              <td><span class="mono">{{ c.certificateNo }}</span></td>
              <td>{{ c.equipmentIdNo }} · {{ c.equipmentTypeName }}</td>
              <td>{{ c.inspectionDate | date: 'dd MMM yyyy' }}</td>
              <td>{{ c.nextDueDate ? (c.nextDueDate | date: 'dd MMM yyyy') : '—' }}</td>
              <td><tuv-status-pill [value]="stateName(c.state)" /></td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>

    <p-dialog [(visible)]="issueDialog" [modal]="true" [style]="{ width: '460px' }"
      header="Raise an issue with this certificate" [closable]="!firing()">
      <p>
        Tell TÜV what's wrong. Your comment is required and goes back to the inspection
        team for review and follow-up.
      </p>
      <label>Issue<span class="req">*</span></label>
      <textarea pTextarea rows="4" [(ngModel)]="issueComments"
        placeholder="e.g. Inspection date is wrong; equipment was not present on this date."></textarea>
      <ng-template pTemplate="footer">
        <p-button severity="secondary" label="Cancel" (onClick)="closeIssue()"
          [disabled]="firing()" />
        <p-button label="Submit issue" icon="pi pi-flag" severity="danger"
          [loading]="firing()" [disabled]="!issueComments.trim()"
          (onClick)="confirmIssue()" />
      </ng-template>
    </p-dialog>
  `,
  styles: [
    `
      :host { display: block; }
      .kpis {
        display: grid; gap: 1rem; margin-bottom: 1.5rem;
        grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      }
      .section-title { font-size: 0.85rem; font-weight: 600; text-transform: uppercase;
        letter-spacing: 0.08em; color: #475569; margin: 1.5rem 0 0.7rem; }
      .card { background: #fff; border: 1px solid #e5e9f2; border-radius: 14px; padding: 0.8rem 1rem; }
      .loader { padding: 2rem; text-align: center; color: #64748b; }
      .mono { font-family: ui-monospace, Menlo, monospace; font-weight: 600; color: #0f172a; }
      .muted { color: #94a3b8; font-size: 0.78rem; margin-top: 0.15rem; }
      .actions { display: flex; gap: 0.4rem; }
      .req { color: #dc2626; margin-left: 0.15rem; }
      label { font-size: 0.85rem; font-weight: 500; color: #334155; display: block; margin: 0.6rem 0 0.3rem; }
      textarea { width: 100%; }
    `,
  ],
})
export class MyCertificatesPage {
  private api = inject(CertificatesApi);
  private auth = inject(AuthService);
  private notify = inject(NotifyService);
  private router = inject(Router);

  protected loading = signal(true);
  protected rows = signal<CertificateListItem[]>([]);

  protected pending = computed(() =>
    this.rows().filter((c) => c.state === CertificateState.ClientSent));
  protected pendingCount = computed(() => this.pending().length);
  protected acceptedCount = computed(() =>
    this.rows().filter((c) => c.state === CertificateState.ClientAccepted
                           || c.state === CertificateState.Archived).length);
  protected rejectedCount = computed(() =>
    this.rows().filter((c) => c.state === CertificateState.ClientRejected).length);
  protected totalCount = computed(() => this.rows().length);

  protected issueDialog = false;
  protected issueTarget: CertificateListItem | null = null;
  protected issueComments = '';
  protected firing = signal(false);

  protected stateName = (s: number) => CertificateStateName[s] ?? 'Unknown';
  protected resultLabel = (r: number) => InspectionResultLabel[r];
  protected inspectionTypeLabel = (t: number) => CertificateInspectionTypeLabel[t];

  constructor() { this.reload(); }

  reload() {
    this.loading.set(true);
    this.api.list({ pageSize: 200 }).subscribe({
      next: (r) => { this.rows.set(r.items); this.loading.set(false); },
      error: (err) => { this.loading.set(false); showHttpError(this.notify, err); },
    });
  }

  open(id: string) { this.router.navigate(['/certificates', id]); }

  accept(c: CertificateListItem) {
    this.api.transition(c.id, 'ClientAccept', undefined).subscribe({
      next: () => { this.notify.success(`Accepted ${c.certificateNo}.`); this.reload(); },
      error: (err) => showHttpError(this.notify, err),
    });
  }

  openIssue(c: CertificateListItem) {
    this.issueTarget = c;
    this.issueComments = '';
    this.issueDialog = true;
  }
  closeIssue() {
    this.issueDialog = false;
    this.issueTarget = null;
    this.issueComments = '';
  }
  confirmIssue() {
    if (!this.issueTarget || !this.issueComments.trim()) return;
    this.firing.set(true);
    this.api.transition(this.issueTarget.id, 'ClientReject', this.issueComments.trim()).subscribe({
      next: () => {
        this.firing.set(false);
        this.notify.success('Issue submitted to TÜV.');
        this.closeIssue();
        this.reload();
      },
      error: (err) => { this.firing.set(false); showHttpError(this.notify, err); },
    });
  }
}
