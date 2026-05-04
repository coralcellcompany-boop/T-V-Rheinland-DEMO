import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';

import { PageHeader } from '../../../shared/components/page-header.component';
import { StatusPill } from '../../../shared/components/status-pill.component';
import { EmptyState } from '../../../shared/components/empty-state.component';
import { SurveysApi } from '../../../core/api/job-management.api';
import { ClientsApi } from '../../../core/api/clients.api';
import { SurveyListItem, SurveyStatusName } from '../../../core/models/job-management.models';
import { ClientListItem } from '../../../core/models/client.models';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';

@Component({
  standalone: true,
  imports: [
    CommonModule, FormsModule, DatePipe,
    ButtonModule, TableModule, InputTextModule, DialogModule, SelectModule,
    PageHeader, StatusPill, EmptyState,
  ],
  template: `
    <tuv-page-header title="Site Surveys" icon="pi-map-marker"
      subtitle="Pre-visit site surveys to scope the next Job Order: equipment count, access, safety notes.">
      <p-button icon="pi pi-plus" label="New survey" (onClick)="newDialog = true" />
    </tuv-page-header>

    <div class="card">
      @if (loading()) { <div class="loader">Loading…</div> }
      @else if (rows().length === 0) {
        <tuv-empty-state icon="pi-map-marker" title="No surveys yet"
          message="Use surveys before scheduling a Job Order to get a feel for site conditions and equipment counts." />
      } @else {
        <p-table [value]="rows()" [rowHover]="true" styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th>Survey</th><th>Client</th><th>Date</th>
              <th>Site</th><th>Equipment</th><th>Status</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-s>
            <tr>
              <td><span class="mono">{{ s.surveyNo }}</span></td>
              <td>{{ s.clientName }}</td>
              <td>{{ s.date | date: 'dd MMM yyyy' }}</td>
              <td>{{ s.site ?? '—' }}</td>
              <td>{{ s.estimatedEquipmentCount }}</td>
              <td><tuv-status-pill [value]="statusName(s.status)" /></td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>

    <p-dialog [(visible)]="newDialog" [modal]="true" [style]="{ width: '460px' }"
      header="New site survey" [closable]="!creating()">
      <div class="form">
        <label>Client</label>
        <p-select [options]="clientOptions()" optionLabel="label" optionValue="value"
          [(ngModel)]="newClientId" appendTo="body" placeholder="Select client" />
        <label>Date</label>
        <input pInputText type="date" [(ngModel)]="newDate" />
        <label>Site</label>
        <input pInputText [(ngModel)]="newSite" />
      </div>
      <ng-template pTemplate="footer">
        <p-button severity="secondary" label="Cancel" (onClick)="newDialog = false" />
        <p-button label="Create" icon="pi pi-plus" [loading]="creating()"
          [disabled]="!newClientId" (onClick)="create()" />
      </ng-template>
    </p-dialog>
  `,
  styles: [
    `
      :host { display: block; }
      .card { background: #fff; border: 1px solid #e5e9f2; border-radius: 14px; padding: 1rem; }
      .loader { padding: 2rem; text-align: center; color: #64748b; }
      .mono { font-family: ui-monospace, Menlo, monospace; font-weight: 600; }
      .form { display: flex; flex-direction: column; gap: 0.5rem; }
      .form label { font-size: 0.85rem; font-weight: 500; color: #334155; margin-top: 0.3rem; }
      .form input { width: 100%; }
      :host ::ng-deep .form .p-select { width: 100%; }
    `,
  ],
})
export class SurveysPage {
  private api = inject(SurveysApi);
  private clientsApi = inject(ClientsApi);
  private notify = inject(NotifyService);

  protected loading = signal(true);
  protected rows = signal<SurveyListItem[]>([]);
  protected clients = signal<ClientListItem[]>([]);
  protected clientOptions = computed(() => this.clients().map(c => ({ label: c.name, value: c.id })));

  protected newDialog = false;
  protected creating = signal(false);
  protected newClientId: string | null = null;
  protected newDate = new Date().toISOString().substring(0, 10);
  protected newSite = '';

  protected statusName = (s: number) => SurveyStatusName[s];

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

  create() {
    if (!this.newClientId) return;
    this.creating.set(true);
    this.api.create({
      clientId: this.newClientId, date: this.newDate, site: this.newSite || null,
    }).subscribe({
      next: () => {
        this.creating.set(false); this.notify.success('Survey created.');
        this.newDialog = false; this.newSite = '';
        this.refresh();
      },
      error: (err) => { this.creating.set(false); showHttpError(this.notify, err); },
    });
  }
}
