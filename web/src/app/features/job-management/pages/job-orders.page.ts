import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { MultiSelectModule } from 'primeng/multiselect';
import { TooltipModule } from 'primeng/tooltip';

import { PageHeader } from '../../../shared/components/page-header.component';
import { StatusPill } from '../../../shared/components/status-pill.component';
import { EmptyState } from '../../../shared/components/empty-state.component';
import { JobOrdersApi } from '../../../core/api/job-management.api';
import { ClientsApi } from '../../../core/api/clients.api';
import { InspectorLookup, UsersApi } from '../../../core/api/users.api';
import {
  JobOrderDetail, JobOrderListItem, JobOrderStatusName, ServiceType, ServiceTypeLabel,
} from '../../../core/models/job-management.models';
import { ClientListItem } from '../../../core/models/client.models';
import { AuthService } from '../../../core/auth/auth.service';
import { Roles } from '../../../core/models/auth.models';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';

@Component({
  standalone: true,
  imports: [
    CommonModule, FormsModule, DatePipe,
    ButtonModule, TableModule, InputTextModule, DialogModule, SelectModule,
    MultiSelectModule, TooltipModule,
    PageHeader, StatusPill, EmptyState,
  ],
  template: `
    <tuv-page-header title="Job Orders" icon="pi-briefcase"
      subtitle="Work assignments. Track lifecycle, assigned inspectors, and operational windows.">
      <p-button *ngIf="canEdit()" icon="pi pi-plus" label="New job order" (onClick)="newDialog = true" />
    </tuv-page-header>

    <div class="card">
      @if (loading()) { <div class="loader">Loading…</div> }
      @else if (rows().length === 0) {
        <tuv-empty-state icon="pi-briefcase" title="No job orders"
          message="Create one directly or convert an accepted Job Request from the inbox." />
      } @else {
        <p-table [value]="rows()" [rowHover]="true" styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th>Job Order</th><th>Client</th><th>Service</th>
              <th>Window</th><th>Inspectors</th><th>Status</th>
              <th style="width: 220px"></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-j>
            <tr>
              <td><span class="mono">{{ j.jobOrderNo }}</span></td>
              <td>{{ j.clientName }}</td>
              <td>{{ serviceLabel(j.service) }}</td>
              <td>{{ j.dateFrom | date: 'dd MMM' }} → {{ j.dateTo | date: 'dd MMM yyyy' }}</td>
              <td>
                <span *ngIf="j.assignedInspectorCount > 0" class="chip">{{ j.assignedInspectorCount }}</span>
                <span *ngIf="j.assignedInspectorCount === 0" class="muted">—</span>
              </td>
              <td><tuv-status-pill [value]="statusName(j.status)" /></td>
              <td class="row-actions">
                <p-button icon="pi pi-file-check" severity="secondary"
                  [text]="true" rounded size="small"
                  pTooltip="Open inspections for this job order"
                  (onClick)="openCertificates(j)" />

                <p-button *ngIf="j.status === 0" icon="pi pi-play" severity="success"
                  [text]="true" rounded size="small"
                  pTooltip="Begin work — moves to In Progress"
                  [loading]="busy() === j.id" (onClick)="beginOrder(j)" />

                <p-button *ngIf="j.status === 1" icon="pi pi-check" severity="success"
                  [text]="true" rounded size="small"
                  pTooltip="Mark complete"
                  [loading]="busy() === j.id" (onClick)="completeOrder(j)" />

                <p-button *ngIf="canEdit()" icon="pi pi-users" severity="secondary"
                  [text]="true" rounded size="small"
                  pTooltip="Assign inspectors" (onClick)="openAssign(j)" />

                <p-button *ngIf="canEdit() && j.status !== 2 && j.status !== 3"
                  icon="pi pi-times" severity="danger"
                  [text]="true" rounded size="small"
                  pTooltip="Cancel job order"
                  [loading]="busy() === j.id" (onClick)="cancelOrder(j)" />
              </td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>

    <p-dialog [(visible)]="newDialog" [modal]="true" [style]="{ width: '460px' }"
      header="New job order" [closable]="!creating()">
      <div class="form">
        <label>Client</label>
        <p-select [options]="clientOptions()" optionLabel="label" optionValue="value"
          [(ngModel)]="newClientId" appendTo="body" placeholder="Select client" />
        <label>Service</label>
        <p-select [options]="serviceOptions" optionLabel="label" optionValue="value"
          [(ngModel)]="newService" appendTo="body" />
        <div class="row">
          <div><label>From</label><input pInputText type="date" [(ngModel)]="newFrom" /></div>
          <div><label>To</label><input pInputText type="date" [(ngModel)]="newTo" /></div>
        </div>
        <label>Location</label><input pInputText [(ngModel)]="newLocation" />
      </div>
      <ng-template pTemplate="footer">
        <p-button severity="secondary" label="Cancel" (onClick)="newDialog = false" />
        <p-button label="Create" icon="pi pi-plus" [loading]="creating()"
          [disabled]="!newClientId" (onClick)="createOrder()" />
      </ng-template>
    </p-dialog>

    <p-dialog [(visible)]="assignDialog" [modal]="true" [style]="{ width: '520px' }"
      header="Assign inspectors" [closable]="!assigning()">
      @if (assignTarget(); as t) {
        <div class="form">
          <p>Job order <code class="mono">{{ t.jobOrderNo }}</code> · {{ t.clientName }}</p>
          <label>Inspectors</label>
          <p-multiSelect [options]="inspectorOptions()" optionLabel="label" optionValue="value"
            [(ngModel)]="assignSelection" placeholder="Select one or more inspectors"
            [filter]="true" appendTo="body" styleClass="ms" />
          <small *ngIf="inspectors().length === 0">
            No users with the Inspector role were found. Add one in Admin first.
          </small>
        </div>
      }
      <ng-template pTemplate="footer">
        <p-button severity="secondary" label="Cancel" (onClick)="closeAssign()" [disabled]="assigning()" />
        <p-button label="Save" icon="pi pi-check" [loading]="assigning()" (onClick)="saveAssign()" />
      </ng-template>
    </p-dialog>
  `,
  styles: [
    `
      :host { display: block; }
      .card { background: #fff; border: 1px solid #e5e9f2; border-radius: 14px; padding: 1rem; }
      .loader { padding: 2rem; text-align: center; color: #64748b; }
      .mono { font-family: ui-monospace, Menlo, monospace; font-weight: 600; color: #0f172a; }
      .chip { background: #eef2ff; color: #4338ca; padding: 0.05rem 0.45rem; border-radius: 999px; font-size: 0.78rem; font-weight: 600; }
      .muted { color: #94a3b8; }
      .form { display: flex; flex-direction: column; gap: 0.5rem; }
      .form label { font-size: 0.85rem; font-weight: 500; color: #334155; margin-top: 0.3rem; }
      .form input { width: 100%; }
      :host ::ng-deep .form .p-select { width: 100%; }
      :host ::ng-deep .form .ms { width: 100%; }
      .row { display: grid; grid-template-columns: 1fr 1fr; gap: 0.7rem; }
      .row-actions { display: flex; gap: 0.15rem; flex-wrap: nowrap; }
    `,
  ],
})
export class JobOrdersPage {
  private api = inject(JobOrdersApi);
  private clientsApi = inject(ClientsApi);
  protected auth = inject(AuthService);
  private notify = inject(NotifyService);

  protected loading = signal(true);
  protected rows = signal<JobOrderListItem[]>([]);
  protected clients = signal<ClientListItem[]>([]);
  protected clientOptions = computed(() => this.clients().map(c => ({ label: c.name, value: c.id })));
  protected inspectors = signal<InspectorLookup[]>([]);
  protected inspectorOptions = computed(() => this.inspectors().map(i =>
    ({ label: `${i.displayName} (${i.email ?? ''})`, value: i.id })));

  protected assignDialog = false;
  protected assigning = signal(false);
  protected assignTarget = signal<JobOrderListItem | null>(null);
  protected assignDetail = signal<JobOrderDetail | null>(null);
  protected assignSelection: string[] = [];
  private usersApi = inject(UsersApi);

  protected newDialog = false;
  protected creating = signal(false);
  protected newClientId: string | null = null;
  protected newService = ServiceType.ThirdPartyInspection;
  protected newFrom = new Date().toISOString().substring(0, 10);
  protected newTo = new Date(Date.now() + 86400000 * 5).toISOString().substring(0, 10);
  protected newLocation = '';

  protected serviceOptions = [
    { value: 1, label: 'TPI' },
    { value: 2, label: 'Blue Sticker' },
    { value: 4, label: 'Operator Assessment' },
    { value: 7, label: 'All services' },
  ];

  protected canEdit = () => this.auth.hasAnyRole([Roles.Manager, Roles.Coordinator]);
  protected isInspectorOnly = () => this.auth.hasRole(Roles.Inspector) && !this.canEdit();
  protected statusName = (s: number) => JobOrderStatusName[s];
  protected serviceLabel = (s: number) => ServiceTypeLabel[s] ?? '—';

  protected busy = signal<string | null>(null);
  private router = inject(Router);

  constructor() {
    this.clientsApi.list({ pageSize: 200 }).subscribe({
      next: (r) => this.clients.set(r.items),
      error: (err) => showHttpError(this.notify, err),
    });
    if (this.canEdit()) {
      // Use the lightweight /api/users/inspectors lookup (Manager + Coordinator).
      // The full /api/admin/users endpoint is Manager-only and would 403 here for
      // Coordinators, leaving the dropdown empty.
      this.usersApi.inspectors().subscribe({
        next: (xs) => this.inspectors.set(xs),
        error: () => { /* leave empty — assign dialog will show empty hint */ },
      });
    }
    this.refresh();
  }

  private refresh() {
    this.loading.set(true);
    // Inspectors see only their own job orders; staff see everything.
    const params = this.isInspectorOnly() ? { mineOnly: true, pageSize: 100 } : { pageSize: 100 };
    this.api.list(params).subscribe({
      next: (r) => { this.rows.set(r.items); this.loading.set(false); },
      error: (err) => { this.loading.set(false); showHttpError(this.notify, err); },
    });
  }

  beginOrder(j: JobOrderListItem) {
    this.busy.set(j.id);
    this.api.begin(j.id).subscribe({
      next: () => { this.busy.set(null); this.notify.success(`${j.jobOrderNo} started.`); this.refresh(); },
      error: (err) => { this.busy.set(null); showHttpError(this.notify, err); },
    });
  }

  completeOrder(j: JobOrderListItem) {
    if (!confirm(`Mark ${j.jobOrderNo} as complete?`)) return;
    this.busy.set(j.id);
    this.api.complete(j.id).subscribe({
      next: () => { this.busy.set(null); this.notify.success(`${j.jobOrderNo} completed.`); this.refresh(); },
      error: (err) => { this.busy.set(null); showHttpError(this.notify, err); },
    });
  }

  cancelOrder(j: JobOrderListItem) {
    if (!confirm(`Cancel ${j.jobOrderNo}? Inspectors won't be able to record more work.`)) return;
    this.busy.set(j.id);
    this.api.cancel(j.id).subscribe({
      next: () => { this.busy.set(null); this.notify.success(`${j.jobOrderNo} cancelled.`); this.refresh(); },
      error: (err) => { this.busy.set(null); showHttpError(this.notify, err); },
    });
  }

  /** Navigate to /certificates pre-filtered to this job order's inspections. */
  openCertificates(j: JobOrderListItem) {
    this.router.navigate(['/certificates'], { queryParams: { jobOrderId: j.id } });
  }

  createOrder() {
    if (!this.newClientId) return;
    this.creating.set(true);
    this.api.create({
      clientId: this.newClientId, service: this.newService,
      dateFrom: this.newFrom, dateTo: this.newTo, location: this.newLocation || null,
    }).subscribe({
      next: (jo) => {
        this.creating.set(false); this.notify.success(`Created ${jo.jobOrderNo}`);
        this.newDialog = false; this.newLocation = '';
        this.refresh();
      },
      error: (err) => { this.creating.set(false); showHttpError(this.notify, err); },
    });
  }

  openAssign(j: JobOrderListItem) {
    this.assignTarget.set(j);
    this.assignSelection = [];
    this.assignDialog = true;
    this.api.get(j.id).subscribe({
      next: (d) => {
        this.assignDetail.set(d);
        this.assignSelection = [...d.assignedInspectorIds];
      },
      error: (err) => showHttpError(this.notify, err),
    });
  }

  closeAssign() {
    this.assignDialog = false;
    this.assignTarget.set(null);
    this.assignDetail.set(null);
    this.assignSelection = [];
  }

  saveAssign() {
    const t = this.assignTarget();
    const d = this.assignDetail();
    if (!t || !d) return;
    this.assigning.set(true);
    this.api.update(t.id, {
      dateFrom: d.dateFrom,
      dateTo: d.dateTo,
      location: d.location,
      status: d.status,
      assignedInspectorIds: this.assignSelection,
    }).subscribe({
      next: () => {
        this.assigning.set(false);
        this.notify.success(`Assigned ${this.assignSelection.length} inspector(s) to ${t.jobOrderNo}.`);
        this.closeAssign();
        this.refresh();
      },
      error: (err) => { this.assigning.set(false); showHttpError(this.notify, err); },
    });
  }
}
