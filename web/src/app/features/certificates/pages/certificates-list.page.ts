import { CommonModule } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { SelectModule } from 'primeng/select';
import { DialogModule } from 'primeng/dialog';
import { TextareaModule } from 'primeng/textarea';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { toSignal } from '@angular/core/rxjs-interop';

import { PageHeader } from '../../../shared/components/page-header.component';
import { StatusPill } from '../../../shared/components/status-pill.component';
import { EmptyState } from '../../../shared/components/empty-state.component';

import { CertificatesApi } from '../../../core/api/certificates.api';
import { ClientsApi } from '../../../core/api/clients.api';
import { EquipmentApi } from '../../../core/api/equipment.api';
import {
  CertificateInspectionTypeLabel,
  CertificateListItem,
  CertificateStateName,
  InspectionResultLabel,
} from '../../../core/models/certificate.models';
import { ClientListItem } from '../../../core/models/client.models';
import { EquipmentListItem } from '../../../core/models/equipment.models';
import { AuthService } from '../../../core/auth/auth.service';
import { Roles } from '../../../core/models/auth.models';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';
import { DatePipe } from '@angular/common';

@Component({
  selector: 'tuv-certificates-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DatePipe,
    ButtonModule,
    TableModule,
    InputTextModule,
    IconFieldModule,
    InputIconModule,
    SelectModule,
    DialogModule,
    TextareaModule,
    PageHeader,
    StatusPill,
    EmptyState,
  ],
  template: `
    <tuv-page-header title="Certificates" icon="pi-file-check"
      subtitle="Inspection certificates with their full lifecycle. Use filters to scope to a client, state, or inspection type.">
      <p-button *ngIf="canCreate()" icon="pi pi-plus" label="New certificate"
        (onClick)="newDialog = true" />
    </tuv-page-header>

    <div class="filters">
      <p-iconfield iconPosition="left" class="grow">
        <p-inputicon styleClass="pi pi-search" />
        <input pInputText placeholder="Search by certificate number"
          [(ngModel)]="searchInput" (ngModelChange)="search$.next($event)" />
      </p-iconfield>

      <p-select [options]="clientOptions()" optionLabel="label" optionValue="value"
        [(ngModel)]="filterClient" (ngModelChange)="onFilterChange()"
        [showClear]="true" placeholder="All clients" appendTo="body" styleClass="filter" />

      <p-select [options]="stateOptions" optionLabel="label" optionValue="value"
        [(ngModel)]="filterState" (ngModelChange)="onFilterChange()"
        [showClear]="true" placeholder="Any state" appendTo="body" styleClass="filter" />

      <p-select [options]="typeOptions" optionLabel="label" optionValue="value"
        [(ngModel)]="filterType" (ngModelChange)="onFilterChange()"
        [showClear]="true" placeholder="Any type" appendTo="body" styleClass="filter" />

      <p-select [options]="resultOptions" optionLabel="label" optionValue="value"
        [(ngModel)]="filterResult" (ngModelChange)="onFilterChange()"
        [showClear]="true" placeholder="Any result" appendTo="body" styleClass="filter" />
    </div>

    <div class="card">
      @if (loading()) {
        <div class="loader">Loading certificates…</div>
      } @else if (rows().length === 0) {
        <tuv-empty-state icon="pi-file-check" title="No certificates yet"
          message="Create the first inspection certificate from an existing equipment record.">
          <p-button *ngIf="canCreate()" icon="pi pi-plus" label="New certificate"
            (onClick)="newDialog = true" />
        </tuv-empty-state>
      } @else {
        <p-table
          [value]="rows()"
          [paginator]="true"
          [rows]="pageSize()"
          [totalRecords]="total()"
          [lazy]="true"
          (onLazyLoad)="onLazyLoad($event)"
          [rowsPerPageOptions]="[10, 25, 50, 100]"
          dataKey="id"
          [rowHover]="true"
          styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th style="width: 16%">Certificate</th>
              <th style="width: 22%">Equipment</th>
              <th style="width: 18%">Client</th>
              <th style="width: 11%">Inspection</th>
              <th style="width: 11%">Next due</th>
              <th style="width: 9%">Result</th>
              <th style="width: 13%">State</th>
              <th style="width: 60px"></th>
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
              <td>
                <span *ngIf="c.nextDueDate">{{ c.nextDueDate | date: 'dd MMM yyyy' }}</span>
                <span *ngIf="!c.nextDueDate" class="muted">—</span>
              </td>
              <td><tuv-status-pill *ngIf="c.result" [value]="resultLabel(c.result)" /></td>
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

    <!-- Create-certificate dialog -->
    <p-dialog [(visible)]="newDialog" header="New certificate" [modal]="true"
      [style]="{ width: '520px' }" [closable]="!creating()">
      <div class="new-form">
        <label>Equipment<span class="req">*</span></label>
        <p-select [options]="equipmentOptions()" optionLabel="label" optionValue="value"
          [(ngModel)]="newEquipmentId" placeholder="Choose equipment" appendTo="body"
          [filter]="true" filterBy="label" />

        <label>Inspection date<span class="req">*</span></label>
        <input pInputText type="date" [(ngModel)]="newInspectionDate" />

        <label>Report issue date<span class="req">*</span></label>
        <input pInputText type="date" [(ngModel)]="newReportIssueDate" />

        <label>Inspection type<span class="req">*</span></label>
        <p-select [options]="typeOptions" optionLabel="label" optionValue="value"
          [(ngModel)]="newInspectionType" appendTo="body" />

        <label>Reference standards</label>
        <textarea pTextarea rows="2" [(ngModel)]="newStandards" placeholder="Auto-fills from equipment type"></textarea>
      </div>
      <ng-template pTemplate="footer">
        <p-button severity="secondary" label="Cancel" (onClick)="closeNew()" [disabled]="creating()" />
        <p-button label="Create" icon="pi pi-plus" [loading]="creating()"
          [disabled]="!canSubmitNew()" (onClick)="createCertificate()" />
      </ng-template>
    </p-dialog>
  `,
  styles: [
    `
      :host { display: block; }
      .filters { display: flex; gap: 0.6rem; align-items: center; margin-bottom: 1rem; flex-wrap: wrap; }
      .filters .grow { flex: 1; min-width: 220px; max-width: 320px; }
      :host ::ng-deep .filter { width: 180px; }
      .card { background: #fff; border-radius: 14px; border: 1px solid #e5e9f2; padding: 1rem; }
      .loader { padding: 2rem; text-align: center; color: #64748b; }
      .cert-no { font-family: ui-monospace, Menlo, monospace; font-weight: 600; color: #0f172a; font-size: 0.9rem; }
      .equip-id { font-family: ui-monospace, Menlo, monospace; font-weight: 600; color: #0f172a; font-size: 0.85rem; }
      .muted { color: #94a3b8; font-size: 0.78rem; margin-top: 0.15rem; }
      .new-form { display: flex; flex-direction: column; gap: 0.6rem; padding: 0.5rem 0; }
      .new-form label { font-size: 0.85rem; font-weight: 500; color: #334155; margin-top: 0.2rem; }
      .req { color: #dc2626; margin-left: 0.15rem; }
      .new-form input, .new-form textarea, .new-form p-select { width: 100%; }
    `,
  ],
})
export class CertificatesListPage {
  protected api = inject(CertificatesApi);
  private clientsApi = inject(ClientsApi);
  private equipmentApi = inject(EquipmentApi);
  protected auth = inject(AuthService);
  private notify = inject(NotifyService);
  private router = inject(Router);

  protected loading = signal(true);
  protected rows = signal<CertificateListItem[]>([]);
  protected total = signal(0);
  protected page = signal(1);
  protected pageSize = signal(25);

  protected clients = signal<ClientListItem[]>([]);
  protected equipment = signal<EquipmentListItem[]>([]);

  protected searchInput = '';
  protected search$ = new Subject<string>();
  private searchSig = toSignal(this.search$.pipe(debounceTime(250), distinctUntilChanged()),
    { initialValue: '' });

  protected filterClient: string | null = null;
  protected filterState: number | null = null;
  protected filterType: number | null = null;
  protected filterResult: number | null = null;

  protected newDialog = false;
  protected creating = signal(false);
  protected newEquipmentId: string | null = null;
  protected newInspectionDate = new Date().toISOString().substring(0, 10);
  protected newReportIssueDate = new Date().toISOString().substring(0, 10);
  protected newInspectionType = 0;
  protected newStandards = '';

  protected canCreate = () =>
    this.auth.hasAnyRole([Roles.Inspector, Roles.Coordinator, Roles.Manager]);

  protected stateOptions = Object.entries(CertificateStateName).map(([v, l]) => ({
    value: Number(v), label: l,
  }));
  protected typeOptions = Object.entries(CertificateInspectionTypeLabel).map(([v, l]) => ({
    value: Number(v), label: l,
  }));
  protected resultOptions = Object.entries(InspectionResultLabel)
    .filter(([v]) => Number(v) !== 0)
    .map(([v, l]) => ({ value: Number(v), label: l }));
  protected clientOptions = computed(() => this.clients().map((c) => ({ label: c.name, value: c.id })));
  protected equipmentOptions = computed(() =>
    this.equipment().map((e) => ({
      label: `${e.idNo} — ${e.equipmentTypeName} (${e.clientName})`,
      value: e.id,
    }))
  );

  protected stateName = (s: number) => CertificateStateName[s] ?? 'Unknown';
  protected resultLabel = (r: number) => InspectionResultLabel[r];
  protected inspectionTypeLabel = (t: number) => CertificateInspectionTypeLabel[t];

  protected canSubmitNew = () =>
    !!this.newEquipmentId && !!this.newInspectionDate && !!this.newReportIssueDate;

  constructor() {
    this.clientsApi.list({ pageSize: 200 }).subscribe({
      next: (r) => this.clients.set(r.items),
      error: (err) => showHttpError(this.notify, err),
    });
    this.equipmentApi.list({ pageSize: 200 }).subscribe({
      next: (r) => this.equipment.set(r.items),
      error: (err) => showHttpError(this.notify, err),
    });

    this.refresh(1, this.pageSize(), '');
    let first = true;
    effect(() => {
      const s = this.searchSig();
      if (first) { first = false; return; }
      this.refresh(1, this.pageSize(), s);
    });
  }

  onFilterChange() { this.refresh(1, this.pageSize(), this.searchSig()); }

  onLazyLoad(e: any) {
    const page = Math.floor((e.first ?? 0) / (e.rows ?? this.pageSize())) + 1;
    this.refresh(page, e.rows ?? this.pageSize(), this.searchSig());
  }

  private refresh(page: number, pageSize: number, search: string) {
    this.loading.set(true);
    this.api.list({
      page, pageSize,
      search: search?.trim() || undefined,
      clientId: this.filterClient ?? undefined,
      state: this.filterState ?? undefined,
      inspectionType: this.filterType ?? undefined,
      result: this.filterResult ?? undefined,
    }).subscribe({
      next: (res) => {
        this.rows.set(res.items);
        this.total.set(res.total);
        this.page.set(res.page);
        this.pageSize.set(res.pageSize);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        showHttpError(this.notify, err, 'Failed to load certificates');
      },
    });
  }

  open(id: string) { this.router.navigate(['/certificates', id]); }

  closeNew() {
    this.newDialog = false;
    this.newEquipmentId = null;
    this.newStandards = '';
  }

  createCertificate() {
    if (!this.canSubmitNew()) return;
    this.creating.set(true);
    this.api.create({
      equipmentId: this.newEquipmentId!,
      inspectionDate: this.newInspectionDate,
      reportIssueDate: this.newReportIssueDate,
      inspectionType: this.newInspectionType,
      standards: this.newStandards || null,
    }).subscribe({
      next: (cert) => {
        this.creating.set(false);
        this.notify.success(`Created ${cert.certificateNo}`);
        this.closeNew();
        this.router.navigate(['/certificates', cert.id]);
      },
      error: (err) => {
        this.creating.set(false);
        showHttpError(this.notify, err);
      },
    });
  }
}
