import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';

import { PageHeader } from '../../../shared/components/page-header.component';
import { StatusPill } from '../../../shared/components/status-pill.component';
import { EmptyState } from '../../../shared/components/empty-state.component';
import { ApprovalsApi } from '../../../core/api/certificates.api';
import {
  CertificateInspectionTypeLabel,
  CertificateListItem,
  CertificateStateName,
} from '../../../core/models/certificate.models';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';

type Bucket = 'pending' | 'rejected' | 'mine';

@Component({
  selector: 'tuv-approvals',
  standalone: true,
  imports: [CommonModule, DatePipe, ButtonModule, TableModule, PageHeader, StatusPill, EmptyState],
  template: `
    <tuv-page-header title="Approval queue" icon="pi-thumbs-up"
      subtitle="Pending, rejected, and your own work in one place." />

    <div class="tabs">
      <button class="tab" [class.active]="bucket() === 'pending'" (click)="select('pending')">
        <i class="pi pi-clock"></i>
        <span class="lbl">Pending</span>
        <span class="count">{{ counts().pending }}</span>
      </button>
      <button class="tab" [class.active]="bucket() === 'rejected'" (click)="select('rejected')">
        <i class="pi pi-times"></i>
        <span class="lbl">Rejected</span>
        <span class="count">{{ counts().rejected }}</span>
      </button>
      <button class="tab" [class.active]="bucket() === 'mine'" (click)="select('mine')">
        <i class="pi pi-user"></i>
        <span class="lbl">My certificates</span>
        <span class="count">{{ counts().mine }}</span>
      </button>
    </div>

    <div class="card">
      @if (loading()) {
        <div class="loader">Loading…</div>
      } @else if (rows().length === 0) {
        <tuv-empty-state icon="pi-check-circle"
          title="Inbox zero"
          [message]="bucket() === 'pending' ? 'Nothing waiting on you right now.'
                   : bucket() === 'rejected' ? 'No rejected certificates.'
                   : 'You have no certificates yet.'" />
      } @else {
        <p-table [value]="rows()" [rowHover]="true" styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th>Certificate</th>
              <th>Equipment</th>
              <th>Client</th>
              <th>Inspection date</th>
              <th>State</th>
              <th></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-c>
            <tr (click)="open(c.id)" style="cursor: pointer">
              <td>
                <div class="cert-no">{{ c.certificateNo }}</div>
                <div class="muted">{{ inspectionTypeLabel(c.inspectionType) }}</div>
              </td>
              <td>
                <div class="equip-id">{{ c.equipmentIdNo }}</div>
                <div class="muted">{{ c.equipmentTypeName }}</div>
              </td>
              <td>{{ c.clientName }}</td>
              <td>{{ c.inspectionDate | date: 'dd MMM yyyy' }}</td>
              <td><tuv-status-pill [value]="stateName(c.state)" /></td>
              <td>
                <p-button icon="pi pi-arrow-right" severity="secondary"
                  [text]="true" rounded (onClick)="open(c.id); $event.stopPropagation()" />
              </td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>
  `,
  styles: [
    `
      :host { display: block; }
      .tabs { display: flex; gap: 0.6rem; margin-bottom: 1rem; flex-wrap: wrap; }
      .tab {
        flex: 1; min-width: 220px;
        display: flex; align-items: center; gap: 0.85rem;
        background: #fff; color: #475569;
        border: 1px solid #e5e9f2; border-radius: 12px;
        padding: 0.85rem 1.1rem; cursor: pointer;
        transition: all 0.18s ease;
        position: relative;
      }
      .tab:hover { border-color: #94a3b8; transform: translateY(-1px); }
      .tab.active {
        background: linear-gradient(135deg, #1d4ed8, #3b82f6);
        color: #fff; border-color: transparent;
        box-shadow: 0 8px 22px -10px rgba(29, 78, 216, 0.45);
      }
      .tab .pi { font-size: 1.1rem; }
      .tab .lbl { flex: 1; text-align: left; font-weight: 500; font-size: 0.9rem; }
      .tab .count {
        background: #f1f5f9; color: #475569;
        padding: 0.15rem 0.55rem; border-radius: 999px;
        font-size: 0.78rem; font-weight: 600; min-width: 28px; text-align: center;
      }
      .tab.active .count { background: rgba(255,255,255,0.18); color: #fff; }
      .card { background: #fff; border: 1px solid #e5e9f2; border-radius: 14px; padding: 1rem; }
      .loader { padding: 2rem; text-align: center; color: #64748b; }
      .cert-no { font-family: ui-monospace, Menlo, monospace; font-weight: 600; color: #0f172a; font-size: 0.9rem; }
      .equip-id { font-family: ui-monospace, Menlo, monospace; font-weight: 600; color: #0f172a; font-size: 0.85rem; }
      .muted { color: #94a3b8; font-size: 0.78rem; margin-top: 0.15rem; }
    `,
  ],
})
export class ApprovalsPage {
  private api = inject(ApprovalsApi);
  private router = inject(Router);
  private notify = inject(NotifyService);

  protected loading = signal(true);
  protected rows = signal<CertificateListItem[]>([]);
  protected counts = signal({ pending: 0, rejected: 0, mine: 0 });
  protected bucket = signal<Bucket>('pending');

  protected stateName = (s: number) => CertificateStateName[s] ?? 'Unknown';
  protected inspectionTypeLabel = (t: number) => CertificateInspectionTypeLabel[t];

  constructor() {
    this.refreshCounts();
    this.refresh();
  }

  select(b: Bucket) {
    if (b === this.bucket()) return;
    this.bucket.set(b);
    this.refresh();
  }

  private refreshCounts() {
    this.api.counts().subscribe({
      next: (c) => this.counts.set(c),
      error: (err) => showHttpError(this.notify, err),
    });
  }

  private refresh() {
    this.loading.set(true);
    this.api.list(this.bucket(), 1, 100).subscribe({
      next: (res) => { this.rows.set(res.items); this.loading.set(false); },
      error: (err) => { this.loading.set(false); showHttpError(this.notify, err); },
    });
  }

  open(id: string) { this.router.navigate(['/certificates', id]); }
}
