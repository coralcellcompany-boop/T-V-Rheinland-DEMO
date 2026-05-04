import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';

import { PageHeader } from '../../../shared/components/page-header.component';
import { StatusPill } from '../../../shared/components/status-pill.component';
import { EmptyState } from '../../../shared/components/empty-state.component';
import { DwrApi, JobOrdersApi } from '../../../core/api/job-management.api';
import {
  DwrListItem, DwrStatusName, JobOrderListItem,
} from '../../../core/models/job-management.models';
import { AuthService } from '../../../core/auth/auth.service';
import { Roles } from '../../../core/models/auth.models';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';

@Component({
  standalone: true,
  imports: [
    CommonModule, FormsModule, DatePipe,
    ButtonModule, TableModule, InputTextModule, DialogModule, SelectModule, TextareaModule,
    PageHeader, StatusPill, EmptyState,
  ],
  template: `
    <tuv-page-header title="Timesheets / DWR" icon="pi-clock"
      subtitle="Daily Work Reports per inspector. Submit for coordinator approval before payroll cut.">
      <p-button icon="pi pi-plus" label="New DWR" (onClick)="newDialog = true" />
    </tuv-page-header>

    <div class="card">
      @if (loading()) { <div class="loader">Loading…</div> }
      @else if (rows().length === 0) {
        <tuv-empty-state icon="pi-clock" title="No DWRs yet"
          message="Inspectors log their daily work here. Coordinators approve for payroll." />
      } @else {
        <p-table [value]="rows()" [rowHover]="true" styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th>DWR</th><th>Job</th><th>Inspector</th>
              <th>Date</th><th>Hours</th>
              <th>Eq./Op.</th><th>Status</th><th></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-d>
            <tr>
              <td><span class="mono">{{ d.dwrNo }}</span></td>
              <td>{{ d.jobOrderNo }}<div class="muted">{{ d.clientName }}</div></td>
              <td>{{ d.inspectorName ?? d.inspectorId }}</td>
              <td>{{ d.date | date: 'dd MMM yyyy' }}</td>
              <td>{{ d.timeFrom }}–{{ d.timeTo }}</td>
              <td>{{ d.equipmentInspected }} / {{ d.operatorsAssessed }}</td>
              <td><tuv-status-pill [value]="statusName(d.status)" /></td>
              <td class="actions">
                <p-button *ngIf="d.status === 0" icon="pi pi-send" severity="primary"
                  [text]="true" rounded (onClick)="submit(d)" pTooltip="Submit" />
                <p-button *ngIf="d.status === 1 && canApprove()" icon="pi pi-check"
                  severity="success" [text]="true" rounded
                  (onClick)="approve(d)" pTooltip="Approve" />
                <p-button *ngIf="d.status === 1 && canApprove()" icon="pi pi-times"
                  severity="danger" [text]="true" rounded
                  (onClick)="openReject(d)" pTooltip="Reject" />
              </td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>

    <p-dialog [(visible)]="newDialog" [modal]="true" [style]="{ width: '500px' }"
      header="New DWR" [closable]="!creating()">
      <div class="form">
        <label>Job order</label>
        <p-select [options]="jobOrderOptions()" optionLabel="label" optionValue="value"
          [(ngModel)]="newJobOrderId" appendTo="body" placeholder="Select job order" [filter]="true" filterBy="label" />
        <div class="row">
          <div><label>Date</label><input pInputText type="date" [(ngModel)]="newDate" /></div>
          <div><label>From</label><input pInputText type="time" [(ngModel)]="newFrom" /></div>
          <div><label>To</label><input pInputText type="time" [(ngModel)]="newTo" /></div>
        </div>
        <label>Location</label><input pInputText [(ngModel)]="newLocation" />
        <div class="row">
          <div><label>Equipment inspected</label><input pInputText type="number" [(ngModel)]="newEq" min="0" /></div>
          <div><label>Operators assessed</label><input pInputText type="number" [(ngModel)]="newOp" min="0" /></div>
        </div>
        <label>Notes</label><textarea pTextarea rows="2" [(ngModel)]="newNotes"></textarea>
      </div>
      <ng-template pTemplate="footer">
        <p-button severity="secondary" label="Cancel" (onClick)="newDialog = false" />
        <p-button label="Create" icon="pi pi-plus" [loading]="creating()"
          [disabled]="!newJobOrderId" (onClick)="create()" />
      </ng-template>
    </p-dialog>

    <p-dialog [(visible)]="rejectDialog" [modal]="true" [style]="{ width: '420px' }"
      header="Reject DWR">
      <label>Reason</label>
      <textarea pTextarea rows="3" [(ngModel)]="rejectReason"></textarea>
      <ng-template pTemplate="footer">
        <p-button severity="secondary" label="Cancel" (onClick)="rejectDialog = false" />
        <p-button label="Reject" severity="danger" [disabled]="!rejectReason.trim()"
          (onClick)="confirmReject()" />
      </ng-template>
    </p-dialog>
  `,
  styles: [
    `
      :host { display: block; }
      .card { background: #fff; border: 1px solid #e5e9f2; border-radius: 14px; padding: 1rem; }
      .loader { padding: 2rem; text-align: center; color: #64748b; }
      .mono { font-family: ui-monospace, Menlo, monospace; font-weight: 600; }
      .muted { color: #94a3b8; font-size: 0.78rem; margin-top: 0.15rem; }
      .actions { display: flex; gap: 0.2rem; }
      .form { display: flex; flex-direction: column; gap: 0.5rem; }
      .form label { font-size: 0.85rem; font-weight: 500; color: #334155; margin-top: 0.3rem; }
      .form input, .form textarea { width: 100%; }
      :host ::ng-deep .form .p-select { width: 100%; }
      .row { display: grid; grid-template-columns: repeat(3, 1fr); gap: 0.7rem; }
    `,
  ],
})
export class TimesheetsPage {
  private api = inject(DwrApi);
  private jobOrdersApi = inject(JobOrdersApi);
  private auth = inject(AuthService);
  private notify = inject(NotifyService);

  protected loading = signal(true);
  protected rows = signal<DwrListItem[]>([]);
  protected jobOrders = signal<JobOrderListItem[]>([]);
  protected jobOrderOptions = computed(() =>
    this.jobOrders().map(j => ({
      label: `${j.jobOrderNo} — ${j.clientName}`,
      value: j.id,
    })));

  protected newDialog = false;
  protected creating = signal(false);
  protected newJobOrderId: string | null = null;
  protected newDate = new Date().toISOString().substring(0, 10);
  protected newFrom = '08:00';
  protected newTo = '17:00';
  protected newLocation = '';
  protected newEq = 0;
  protected newOp = 0;
  protected newNotes = '';

  protected rejectDialog = false;
  protected rejectTarget: DwrListItem | null = null;
  protected rejectReason = '';

  protected canApprove = () => this.auth.hasAnyRole([Roles.Manager, Roles.Coordinator]);
  protected statusName = (s: number) => DwrStatusName[s];

  constructor() {
    this.jobOrdersApi.list({ pageSize: 200 }).subscribe({
      next: (r) => this.jobOrders.set(r.items),
      error: (err) => showHttpError(this.notify, err),
    });
    this.refresh();
  }

  private refresh() {
    this.loading.set(true);
    this.api.list({ pageSize: 100 }).subscribe({
      next: (r) => { this.rows.set(r.items); this.loading.set(false); },
      error: (err) => { this.loading.set(false); showHttpError(this.notify, err); },
    });
  }

  create() {
    if (!this.newJobOrderId) return;
    this.creating.set(true);
    this.api.create({
      jobOrderId: this.newJobOrderId,
      date: this.newDate, timeFrom: this.newFrom + ':00', timeTo: this.newTo + ':00',
      location: this.newLocation || null,
      equipmentInspected: Number(this.newEq), operatorsAssessed: Number(this.newOp),
      notes: this.newNotes || null,
    }).subscribe({
      next: () => {
        this.creating.set(false); this.notify.success('DWR created.');
        this.newDialog = false;
        this.newLocation = ''; this.newNotes = ''; this.newEq = 0; this.newOp = 0;
        this.refresh();
      },
      error: (err) => { this.creating.set(false); showHttpError(this.notify, err); },
    });
  }

  submit(d: DwrListItem) {
    this.api.submit(d.id).subscribe({
      next: () => { this.notify.success('Submitted.'); this.refresh(); },
      error: (err) => showHttpError(this.notify, err),
    });
  }

  approve(d: DwrListItem) {
    this.api.approve(d.id).subscribe({
      next: () => { this.notify.success('Approved.'); this.refresh(); },
      error: (err) => showHttpError(this.notify, err),
    });
  }

  openReject(d: DwrListItem) { this.rejectTarget = d; this.rejectReason = ''; this.rejectDialog = true; }
  confirmReject() {
    if (!this.rejectTarget || !this.rejectReason.trim()) return;
    this.api.reject(this.rejectTarget.id, this.rejectReason.trim()).subscribe({
      next: () => { this.notify.success('Rejected.'); this.rejectDialog = false; this.refresh(); },
      error: (err) => showHttpError(this.notify, err),
    });
  }
}
