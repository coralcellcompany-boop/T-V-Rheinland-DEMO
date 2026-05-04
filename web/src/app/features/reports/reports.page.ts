import { CommonModule, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { CardModule } from 'primeng/card';
import { SelectModule } from 'primeng/select';
import { InputTextModule } from 'primeng/inputtext';

import { PageHeader } from '../../shared/components/page-header.component';
import { EmptyState } from '../../shared/components/empty-state.component';
import {
  DueSoonRow,
  InspectorProductivityRow,
  MonthlyStatsRow,
  OverdueRow,
  ReportsApi,
} from '../../core/api/reports.api';
import { NotifyService } from '../../shared/services/notify.service';
import { showHttpError } from '../../shared/services/api-error.handler';

@Component({
  standalone: true,
  imports: [
    CommonModule, FormsModule, DatePipe,
    ButtonModule, TableModule, CardModule, SelectModule, InputTextModule,
    PageHeader, EmptyState,
  ],
  template: `
    <tuv-page-header title="Reports" icon="pi-chart-line"
      subtitle="Operational reports + the weekly Aramco Contractor Cranes Tracking export.">
      <p-button icon="pi pi-refresh" severity="secondary" [text]="true" (onClick)="reload()" label="Refresh" />
    </tuv-page-header>

    <!-- Aramco weekly export -->
    <p-card styleClass="aramco">
      <div class="aramco-header">
        <div>
          <h3>Aramco Contractor Cranes Tracking</h3>
          <p>Filters to Blue Sticker certificates approved in the Mon→Sun window
             containing the chosen cutoff. Outputs the SRS §5.5.7 .xlsx with the standard 18 columns.</p>
        </div>
        <div class="actions">
          <input pInputText type="date" [(ngModel)]="cutoff" />
          <p-button icon="pi pi-download" label="Download xlsx"
            [loading]="downloading()" (onClick)="downloadAramcoWeekly()" />
        </div>
      </div>
    </p-card>

    <!-- Monthly stats -->
    <h2 class="section-title">Monthly stats (last 6 months)</h2>
    <div class="card">
      @if (monthly().length === 0) {
        <tuv-empty-state icon="pi-chart-bar" title="No data yet" />
      } @else {
        <p-table [value]="monthly()" styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th>Period</th><th>Total</th>
              <th>Approved</th><th>Rejected</th><th>In progress</th>
              <th>Trend</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-r>
            <tr>
              <td>{{ r.period }}</td>
              <td><strong>{{ r.totalCertificates }}</strong></td>
              <td class="positive">{{ r.approved }}</td>
              <td class="negative">{{ r.rejected }}</td>
              <td>{{ r.inProgress }}</td>
              <td>
                <div class="bar">
                  <span class="seg ok"   [style.width.%]="ratio(r.approved, r.totalCertificates)"></span>
                  <span class="seg warn" [style.width.%]="ratio(r.inProgress, r.totalCertificates)"></span>
                  <span class="seg bad"  [style.width.%]="ratio(r.rejected, r.totalCertificates)"></span>
                </div>
              </td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>

    <!-- Inspector productivity -->
    <h2 class="section-title">Inspector productivity (last 30 days)</h2>
    <div class="card">
      @if (productivity().length === 0) {
        <tuv-empty-state icon="pi-users" title="No inspector activity in the window" />
      } @else {
        <p-table [value]="productivity()" styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th>Inspector</th>
              <th>Certs created</th><th>Certs approved</th>
              <th>DWR entries</th><th>Total hours</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-r>
            <tr>
              <td><strong>{{ r.inspectorName }}</strong></td>
              <td>{{ r.certificatesCreated }}</td>
              <td class="positive">{{ r.certificatesApproved }}</td>
              <td>{{ r.dwrEntries }}</td>
              <td>{{ r.totalHours }} h</td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>

    <!-- Due soon + Overdue -->
    <div class="grid">
      <div class="col">
        <h2 class="section-title">Due in the next 30 days</h2>
        <div class="card">
          @if (dueSoon().length === 0) {
            <tuv-empty-state icon="pi-calendar" title="Nothing due soon" />
          } @else {
            <p-table [value]="dueSoon()" styleClass="p-datatable-sm">
              <ng-template pTemplate="header">
                <tr>
                  <th>Certificate</th><th>Equipment</th><th>Client</th>
                  <th>Next due</th><th>Days</th>
                </tr>
              </ng-template>
              <ng-template pTemplate="body" let-r>
                <tr>
                  <td><span class="mono">{{ r.certificateNo }}</span></td>
                  <td>{{ r.equipmentIdNo }} · {{ r.equipmentTypeName }}</td>
                  <td>{{ r.clientName }}</td>
                  <td>{{ r.nextDueDate | date: 'dd MMM yyyy' }}</td>
                  <td><span class="pill" [attr.data-tone]="dueTone(r.daysUntilDue)">{{ r.daysUntilDue }}d</span></td>
                </tr>
              </ng-template>
            </p-table>
          }
        </div>
      </div>

      <div class="col">
        <h2 class="section-title">Overdue</h2>
        <div class="card">
          @if (overdue().length === 0) {
            <tuv-empty-state icon="pi-check-circle" title="No overdue equipment 🎉" />
          } @else {
            <p-table [value]="overdue()" styleClass="p-datatable-sm">
              <ng-template pTemplate="header">
                <tr>
                  <th>Certificate</th><th>Equipment</th>
                  <th>Was due</th><th>Days late</th>
                </tr>
              </ng-template>
              <ng-template pTemplate="body" let-r>
                <tr>
                  <td><span class="mono">{{ r.certificateNo }}</span></td>
                  <td>{{ r.equipmentIdNo }}</td>
                  <td>{{ r.nextDueDate | date: 'dd MMM yyyy' }}</td>
                  <td><span class="pill" data-tone="bad">{{ r.daysOverdue }}d</span></td>
                </tr>
              </ng-template>
            </p-table>
          }
        </div>
      </div>
    </div>
  `,
  styles: [
    `
      :host { display: block; }
      :host ::ng-deep .aramco {
        background: linear-gradient(135deg, #f97316 0%, #ea580c 100%);
        color: #fff; border-radius: 16px; margin-bottom: 1.5rem;
        box-shadow: 0 12px 30px -16px rgba(234, 88, 12, 0.5);
      }
      :host ::ng-deep .aramco .p-card-body { padding: 1.4rem 1.6rem; }
      :host ::ng-deep .aramco h3 { margin: 0; color: #fff; font-size: 1.2rem; }
      :host ::ng-deep .aramco p { margin: 0.4rem 0 0; opacity: 0.92; font-size: 0.9rem; max-width: 60ch; }
      .aramco-header { display: flex; align-items: center; justify-content: space-between; gap: 1rem; flex-wrap: wrap; }
      .aramco-header .actions { display: flex; gap: 0.55rem; align-items: center; }

      .section-title { font-size: 0.85rem; font-weight: 600; text-transform: uppercase; letter-spacing: 0.08em; color: #475569; margin: 1.5rem 0 0.7rem; }
      .card { background: #fff; border: 1px solid #e5e9f2; border-radius: 14px; padding: 0.8rem 1rem; }
      .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
      .grid .col { min-width: 0; }
      @media (max-width: 1080px) { .grid { grid-template-columns: 1fr; } }

      .mono { font-family: ui-monospace, Menlo, monospace; font-weight: 600; }
      .positive { color: #047857; font-weight: 600; }
      .negative { color: #b91c1c; font-weight: 600; }

      .bar { display: flex; height: 10px; background: #f1f5f9; border-radius: 999px; overflow: hidden; }
      .seg { display: block; height: 100%; }
      .seg.ok { background: #10b981; }
      .seg.warn { background: #f59e0b; }
      .seg.bad { background: #ef4444; }

      .pill { padding: 0.05rem 0.55rem; border-radius: 999px; font-size: 0.78rem; font-weight: 600; background: #f1f5f9; color: #475569; }
      .pill[data-tone='warn'] { background: #fef3c7; color: #b45309; }
      .pill[data-tone='bad']  { background: #fee2e2; color: #b91c1c; }
    `,
  ],
})
export class ReportsPage {
  private api = inject(ReportsApi);
  private notify = inject(NotifyService);

  protected monthly = signal<MonthlyStatsRow[]>([]);
  protected productivity = signal<InspectorProductivityRow[]>([]);
  protected dueSoon = signal<DueSoonRow[]>([]);
  protected overdue = signal<OverdueRow[]>([]);

  protected cutoff = new Date().toISOString().substring(0, 10);
  protected downloading = signal(false);

  protected ratio = (n: number, total: number) =>
    total > 0 ? Math.round((n / total) * 100) : 0;

  protected dueTone = (days: number) => days <= 7 ? 'bad' : days <= 14 ? 'warn' : 'neutral';

  constructor() { this.reload(); }

  reload() {
    this.api.monthly(6).subscribe({
      next: (r) => this.monthly.set(r),
      error: (err) => showHttpError(this.notify, err),
    });
    this.api.productivity(30).subscribe({
      next: (r) => this.productivity.set(r),
      error: (err) => showHttpError(this.notify, err),
    });
    this.api.dueSoon(30).subscribe({
      next: (r) => this.dueSoon.set(r),
      error: (err) => showHttpError(this.notify, err),
    });
    this.api.overdue().subscribe({
      next: (r) => this.overdue.set(r),
      error: (err) => showHttpError(this.notify, err),
    });
  }

  downloadAramcoWeekly() {
    this.downloading.set(true);
    this.api.aramcoWeekly(this.cutoff).subscribe({
      next: (blob) => {
        this.downloading.set(false);
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `Aramco-Weekly-${this.cutoff}.xlsx`;
        a.click();
        window.URL.revokeObjectURL(url);
        this.notify.success('Aramco weekly export downloaded.');
      },
      error: (err) => {
        this.downloading.set(false);
        showHttpError(this.notify, err);
      },
    });
  }
}
