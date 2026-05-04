import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';

import { PageHeader } from '../../../shared/components/page-header.component';
import { StatusPill } from '../../../shared/components/status-pill.component';
import { EmptyState } from '../../../shared/components/empty-state.component';
import { JobRequestsApi } from '../../../core/api/job-management.api';
import { ClientsApi } from '../../../core/api/clients.api';
import {
  JobRequestListItem, JobRequestStatusName, ServiceTypeLabel,
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
    ButtonModule, TableModule, InputTextModule, IconFieldModule, InputIconModule,
    DialogModule, SelectModule, TextareaModule,
    PageHeader, StatusPill, EmptyState,
  ],
  template: `
    <tuv-page-header title="Job Requests" icon="pi-inbox"
      subtitle="Inbound queue. Accept and convert to a Job Order, or reject with a reason.">
      <p-button *ngIf="canEdit()" icon="pi pi-plus" label="New request" (onClick)="newDialog = true" />
    </tuv-page-header>

    <div class="card">
      @if (loading()) { <div class="loader">Loading…</div> }
      @else if (rows().length === 0) {
        <tuv-empty-state icon="pi-inbox" title="Inbox zero"
          message="No job requests yet. Coordinators can create one or wait for a client to submit." />
      } @else {
        <p-table [value]="rows()" [rowHover]="true" styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th>Request</th><th>Client</th><th>Service</th><th>Window</th>
              <th>Site</th><th>Status</th><th></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-r>
            <tr>
              <td><span class="mono">{{ r.requestNo }}</span></td>
              <td>{{ r.clientName }}</td>
              <td>{{ serviceLabel(r.service) }}</td>
              <td>{{ r.requestedFrom | date: 'dd MMM' }} → {{ r.requestedTo | date: 'dd MMM yyyy' }}</td>
              <td>{{ r.site ?? '—' }}</td>
              <td><tuv-status-pill [value]="statusName(r.status)" /></td>
              <td class="actions">
                <p-button *ngIf="r.status === 0 || r.status === 1" icon="pi pi-check"
                  severity="success" [text]="true" rounded
                  (onClick)="accept(r)" pTooltip="Accept" />
                <p-button *ngIf="r.status === 2" icon="pi pi-arrow-circle-right"
                  severity="primary" [text]="true" rounded
                  (onClick)="convert(r)" pTooltip="Convert to Job Order" />
                <p-button *ngIf="r.status === 0 || r.status === 1 || r.status === 2"
                  icon="pi pi-times" severity="danger" [text]="true" rounded
                  (onClick)="openReject(r)" pTooltip="Reject" />
              </td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>

    <p-dialog [(visible)]="newDialog" [modal]="true" [style]="{ width: '500px' }"
      header="New job request" [closable]="!creating()">
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
        <label>Site</label><input pInputText [(ngModel)]="newSite" />
        <label>Contact email</label><input pInputText type="email" [(ngModel)]="newEmail" />
        <label>Scope notes</label>
        <textarea pTextarea rows="2" [(ngModel)]="newScope"></textarea>
      </div>
      <ng-template pTemplate="footer">
        <p-button severity="secondary" label="Cancel" (onClick)="newDialog = false" />
        <p-button label="Create" icon="pi pi-plus" [loading]="creating()"
          [disabled]="!newClientId" (onClick)="createRequest()" />
      </ng-template>
    </p-dialog>

    <p-dialog [(visible)]="rejectDialog" [modal]="true" [style]="{ width: '420px' }"
      header="Reject request">
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
      .actions { display: flex; gap: 0.2rem; }
      .form { display: flex; flex-direction: column; gap: 0.5rem; }
      .form label { font-size: 0.85rem; font-weight: 500; color: #334155; margin-top: 0.3rem; }
      .form input, .form textarea { width: 100%; }
      :host ::ng-deep .form .p-select { width: 100%; }
      .row { display: grid; grid-template-columns: 1fr 1fr; gap: 0.7rem; }
    `,
  ],
})
export class JobRequestsPage {
  private api = inject(JobRequestsApi);
  private clientsApi = inject(ClientsApi);
  protected auth = inject(AuthService);
  private notify = inject(NotifyService);

  protected loading = signal(true);
  protected rows = signal<JobRequestListItem[]>([]);
  protected clients = signal<ClientListItem[]>([]);
  protected clientOptions = computed(() => this.clients().map(c => ({ label: c.name, value: c.id })));

  protected newDialog = false;
  protected creating = signal(false);
  protected newClientId: string | null = null;
  protected newService = 1;
  protected newFrom = new Date().toISOString().substring(0, 10);
  protected newTo = new Date().toISOString().substring(0, 10);
  protected newSite = '';
  protected newEmail = '';
  protected newScope = '';

  protected rejectDialog = false;
  protected rejectTarget: JobRequestListItem | null = null;
  protected rejectReason = '';

  protected serviceOptions = [
    { value: 1, label: 'TPI' },
    { value: 2, label: 'Blue Sticker' },
    { value: 4, label: 'Operator Assessment' },
    { value: 7, label: 'All services' },
  ];

  protected canEdit = () => this.auth.hasAnyRole([Roles.Manager, Roles.Coordinator]);
  protected statusName = (s: number) => JobRequestStatusName[s];
  protected serviceLabel = (s: number) => ServiceTypeLabel[s] ?? '—';

  constructor() {
    this.clientsApi.list({ pageSize: 200 }).subscribe({
      next: (r) => this.clients.set(r.items),
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

  createRequest() {
    if (!this.newClientId) return;
    this.creating.set(true);
    this.api.create({
      clientId: this.newClientId,
      service: this.newService,
      requestedFrom: this.newFrom,
      requestedTo: this.newTo,
      site: this.newSite || null,
      contactEmail: this.newEmail || null,
      scopeNotes: this.newScope || null,
    }).subscribe({
      next: () => {
        this.creating.set(false);
        this.notify.success('Request created.');
        this.newDialog = false;
        this.newSite = ''; this.newEmail = ''; this.newScope = '';
        this.refresh();
      },
      error: (err) => { this.creating.set(false); showHttpError(this.notify, err); },
    });
  }

  accept(r: JobRequestListItem) {
    this.api.accept(r.id).subscribe({
      next: () => { this.notify.success('Accepted.'); this.refresh(); },
      error: (err) => showHttpError(this.notify, err),
    });
  }

  convert(r: JobRequestListItem) {
    this.api.convert(r.id).subscribe({
      next: (jo) => { this.notify.success(`Converted to ${jo.jobOrderNo}.`); this.refresh(); },
      error: (err) => showHttpError(this.notify, err),
    });
  }

  openReject(r: JobRequestListItem) {
    this.rejectTarget = r; this.rejectReason = ''; this.rejectDialog = true;
  }
  confirmReject() {
    if (!this.rejectTarget || !this.rejectReason.trim()) return;
    this.api.reject(this.rejectTarget.id, this.rejectReason.trim()).subscribe({
      next: () => { this.notify.success('Rejected.'); this.rejectDialog = false; this.refresh(); },
      error: (err) => showHttpError(this.notify, err),
    });
  }
}
