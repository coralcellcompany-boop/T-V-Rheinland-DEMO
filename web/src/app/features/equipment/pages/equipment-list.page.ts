import { CommonModule } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { DrawerModule } from 'primeng/drawer';
import { SelectModule } from 'primeng/select';
import { DialogModule } from 'primeng/dialog';
import { TooltipModule } from 'primeng/tooltip';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { toSignal } from '@angular/core/rxjs-interop';

import { PageHeader } from '../../../shared/components/page-header.component';
import { StatusPill } from '../../../shared/components/status-pill.component';
import { EmptyState } from '../../../shared/components/empty-state.component';

import { EquipmentApi, EquipmentTypesApi } from '../../../core/api/equipment.api';
import { ClientsApi } from '../../../core/api/clients.api';
import {
  AramcoCategoryName,
  EquipmentDetail,
  EquipmentListItem,
  EquipmentStatusLabel,
  EquipmentType,
} from '../../../core/models/equipment.models';
import { ClientListItem } from '../../../core/models/client.models';
import { AuthService } from '../../../core/auth/auth.service';
import { Roles } from '../../../core/models/auth.models';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';
import { EquipmentForm } from '../components/equipment-form.component';

@Component({
  selector: 'tuv-equipment-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    ButtonModule,
    TableModule,
    InputTextModule,
    IconFieldModule,
    InputIconModule,
    DrawerModule,
    SelectModule,
    DialogModule,
    TooltipModule,
    PageHeader,
    StatusPill,
    EmptyState,
    EquipmentForm,
  ],
  template: `
    <tuv-page-header
      title="Equipment"
      icon="pi-wrench"
      subtitle="The master register of every inspectable equipment item, scoped per client.">
      <p-button *ngIf="canEdit()" icon="pi pi-upload" severity="secondary"
        label="Import Excel" (onClick)="importDialog = true" />
      <p-button *ngIf="canEdit()" icon="pi pi-plus" label="New equipment" (onClick)="openNew()" />
    </tuv-page-header>

    <div class="filters">
      <p-iconfield iconPosition="left" class="grow">
        <p-inputicon styleClass="pi pi-search" />
        <input pInputText placeholder="Search by ID, serial, manufacturer"
          [(ngModel)]="searchInput" (ngModelChange)="search$.next($event)" />
      </p-iconfield>

      <p-select [options]="clientOptions()" optionLabel="label" optionValue="value"
        [(ngModel)]="filterClient" (ngModelChange)="onFilterChange()"
        [showClear]="true" placeholder="All clients" appendTo="body" styleClass="filter" />

      <p-select [options]="categoryOptions" optionLabel="label" optionValue="value"
        [(ngModel)]="filterCategory" (ngModelChange)="onFilterChange()"
        [showClear]="true" placeholder="Any Aramco category" appendTo="body" styleClass="filter wide" />

      <p-select [options]="statusOptions" optionLabel="label" optionValue="value"
        [(ngModel)]="filterStatus" (ngModelChange)="onFilterChange()"
        [showClear]="true" placeholder="Any status" appendTo="body" styleClass="filter" />
    </div>

    <div class="card">
      @if (loading()) {
        <div class="loader">Loading equipment…</div>
      } @else if (rows().length === 0) {
        <tuv-empty-state icon="pi-wrench" title="No equipment matches your filters"
          message="Add equipment manually or import an Excel sheet from a client list.">
          <p-button *ngIf="canEdit()" icon="pi pi-plus" label="New equipment" (onClick)="openNew()" />
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
              <th style="width: 18%">ID No.</th>
              <th style="width: 22%">Type / Aramco</th>
              <th style="width: 18%">Client</th>
              <th>Manufacturer / Model</th>
              <th style="width: 8%">SWL</th>
              <th style="width: 12%">Status</th>
              <th style="width: 60px"></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-e>
            <tr>
              <td>
                <div class="idno">{{ e.idNo }}</div>
                <div class="muted" *ngIf="e.serialNo">SN: {{ e.serialNo }}</div>
              </td>
              <td>
                <div class="type">{{ e.equipmentTypeName }}</div>
                <span *ngIf="e.aramcoCategory" class="aramco">{{ aramcoLabel(e.aramcoCategory) }}</span>
              </td>
              <td>{{ e.clientName }}</td>
              <td>
                <div *ngIf="e.manufacturer">{{ e.manufacturer }}</div>
                <div class="muted" *ngIf="e.model">{{ e.model }}</div>
                <span class="muted" *ngIf="!e.manufacturer && !e.model">—</span>
              </td>
              <td>{{ e.swl ?? '—' }}</td>
              <td><tuv-status-pill [value]="statusLabel(e.status)" /></td>
              <td class="row-actions">
                <a [routerLink]="['/equipment', e.id, 'history']">
                  <p-button icon="pi pi-history" severity="secondary"
                    [text]="true" rounded pTooltip="History" />
                </a>
                <p-button *ngIf="canEdit()" icon="pi pi-pencil" severity="secondary"
                  [text]="true" rounded (onClick)="openEdit(e)" />
              </td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>

    <p-drawer
      [(visible)]="drawerOpen"
      position="right" [style]="{ width: '640px' }"
      [showCloseIcon]="true" [modal]="true"
      header="{{ editing() ? 'Edit equipment' : 'New equipment' }}">
      <tuv-equipment-form
        [editing]="editing()"
        [clients]="clients()"
        [types]="types()"
        (save)="onSave($event)"
        (cancel)="closeDrawer()" />
    </p-drawer>

    <p-dialog [(visible)]="importDialog"
      header="Import equipment from Excel"
      [modal]="true" [style]="{ width: '460px' }" [closable]="!importing()">
      <div class="import-body">
        <p>
          Upload an .xlsx with columns:
          <strong>IdNo · SerialNo · EquipmentType · AramcoCategory · Manufacturer · Model · Year · SWL · Location</strong>.
          Existing IdNos for the chosen client are skipped (no overwrite).
        </p>

        <label>Target client</label>
        <p-select [options]="clientOptions()" optionLabel="label" optionValue="value"
          [(ngModel)]="importClientId" placeholder="Select client" appendTo="body"
          [showClear]="false" />

        <label>Excel file</label>
        <input type="file" accept=".xlsx" (change)="onFilePicked($event)" />

        <div *ngIf="importResult() as r" class="result">
          <strong>Imported {{ r.imported }}</strong>, skipped {{ r.skipped }}, errors {{ r.errors.length }}.
          <ul *ngIf="r.errors.length"><li *ngFor="let err of r.errors">{{ err }}</li></ul>
        </div>
      </div>
      <ng-template pTemplate="footer">
        <p-button severity="secondary" label="Close" (onClick)="closeImport()" [disabled]="importing()" />
        <p-button label="Import" icon="pi pi-upload" [loading]="importing()"
          [disabled]="!importFile || !importClientId" (onClick)="runImport()" />
      </ng-template>
    </p-dialog>
  `,
  styles: [
    `
      :host { display: block; }
      .filters { display: flex; gap: 0.6rem; align-items: center; margin-bottom: 1rem; flex-wrap: wrap; }
      .filters .grow { flex: 1; min-width: 240px; max-width: 360px; }
      :host ::ng-deep .filter { width: 200px; }
      :host ::ng-deep .filter.wide { width: 280px; }
      .card { background: #fff; border-radius: 14px; border: 1px solid #e5e9f2; padding: 1rem; }
      .loader { padding: 2rem; text-align: center; color: #64748b; }
      .idno { font-weight: 600; color: #0f172a; font-family: ui-monospace, Menlo, monospace; font-size: 0.9rem; }
      .type { font-weight: 500; }
      .aramco {
        display: inline-block; margin-top: 0.2rem;
        background: #fff7e6; color: #b45309; border: 1px solid #fde68a;
        font-size: 0.7rem; padding: 0.05rem 0.45rem; border-radius: 999px;
      }
      .muted { color: #94a3b8; font-size: 0.85rem; }

      .import-body { display: flex; flex-direction: column; gap: 0.55rem; padding: 0.5rem 0; }
      .import-body label { font-size: 0.85rem; font-weight: 500; color: #334155; margin-top: 0.4rem; }
      .import-body p { color: #475569; font-size: 0.9rem; margin: 0 0 0.5rem 0; }
      .result { margin-top: 0.6rem; background: #f1f5f9; padding: 0.6rem 0.8rem; border-radius: 8px; }
      .result ul { margin: 0.4rem 0 0 0; padding-left: 1rem; color: #b91c1c; font-size: 0.82rem; }
    `,
  ],
})
export class EquipmentListPage {
  protected api = inject(EquipmentApi);
  protected typesApi = inject(EquipmentTypesApi);
  protected clientsApi = inject(ClientsApi);
  protected auth = inject(AuthService);
  private notify = inject(NotifyService);

  protected loading = signal(true);
  protected rows = signal<EquipmentListItem[]>([]);
  protected total = signal(0);
  protected page = signal(1);
  protected pageSize = signal(25);

  protected clients = signal<ClientListItem[]>([]);
  protected types = signal<EquipmentType[]>([]);

  protected searchInput = '';
  protected search$ = new Subject<string>();
  private searchSig = toSignal(this.search$.pipe(debounceTime(250), distinctUntilChanged()),
    { initialValue: '' });

  protected filterClient: string | null = null;
  protected filterCategory: number | null = null;
  protected filterStatus: number | null = null;

  protected drawerOpen = false;
  protected editing = signal<EquipmentDetail | null>(null);

  protected importDialog = false;
  protected importing = signal(false);
  protected importFile: File | null = null;
  protected importClientId: string | null = null;
  protected importResult = signal<{ imported: number; skipped: number; errors: string[] } | null>(null);

  protected canEdit = () =>
    this.auth.hasRole(Roles.Manager) || this.auth.hasRole(Roles.Coordinator);

  protected statusOptions = [
    { label: EquipmentStatusLabel[0], value: 0 },
    { label: EquipmentStatusLabel[1], value: 1 },
    { label: EquipmentStatusLabel[2], value: 2 },
  ];
  protected categoryOptions = Object.entries(AramcoCategoryName).map(([v, l]) => ({
    value: Number(v),
    label: l,
  }));
  protected clientOptions = computed(() =>
    this.clients().map((c) => ({ label: c.name, value: c.id }))
  );

  protected aramcoLabel = (n: number) => AramcoCategoryName[n] ?? '';
  protected statusLabel = (s: number) => EquipmentStatusLabel[s] ?? 'Unknown';

  constructor() {
    this.clientsApi.list({ pageSize: 200 }).subscribe({
      next: (r) => this.clients.set(r.items),
      error: (err) => showHttpError(this.notify, err),
    });
    this.typesApi.list().subscribe({
      next: (r) => this.types.set(r),
      error: (err) => showHttpError(this.notify, err),
    });

    // Skip first run — p-table's onLazyLoad fires the initial fetch.
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
    this.api
      .list({
        page,
        pageSize,
        search: search?.trim() || undefined,
        clientId: this.filterClient ?? undefined,
        aramcoCategory: this.filterCategory ?? undefined,
        status: this.filterStatus ?? undefined,
      })
      .subscribe({
        next: (res) => {
          this.rows.set(res.items);
          this.total.set(res.total);
          this.page.set(res.page);
          this.pageSize.set(res.pageSize);
          this.loading.set(false);
        },
        error: (err) => {
          this.loading.set(false);
          showHttpError(this.notify, err, 'Failed to load equipment');
        },
      });
  }

  openNew() { this.editing.set(null); this.drawerOpen = true; }

  openEdit(row: EquipmentListItem) {
    this.api.get(row.id).subscribe({
      next: (d) => { this.editing.set(d); this.drawerOpen = true; },
      error: (err) => showHttpError(this.notify, err),
    });
  }

  closeDrawer() { this.drawerOpen = false; this.editing.set(null); }

  onSave(payload: any) {
    const editing = this.editing();
    const obs = editing
      ? this.api.update(editing.id, payload)
      : this.api.create(payload);
    obs.subscribe({
      next: () => {
        this.notify.success(editing ? 'Equipment updated.' : 'Equipment created.');
        this.closeDrawer();
        this.refresh(this.page(), this.pageSize(), this.searchSig());
      },
      error: (err) => showHttpError(this.notify, err),
    });
  }

  onFilePicked(e: Event) {
    const input = e.target as HTMLInputElement;
    this.importFile = input.files?.[0] ?? null;
    this.importResult.set(null);
  }

  closeImport() {
    this.importDialog = false;
    this.importFile = null;
    this.importClientId = null;
    this.importResult.set(null);
  }

  runImport() {
    if (!this.importFile || !this.importClientId) return;
    this.importing.set(true);
    this.api.import(this.importClientId, this.importFile).subscribe({
      next: (r) => {
        this.importing.set(false);
        this.importResult.set(r);
        if (r.imported > 0) {
          this.notify.success(`Imported ${r.imported} equipment record(s).`);
          this.refresh(1, this.pageSize(), this.searchSig());
        } else if (r.errors.length === 0) {
          this.notify.info('No new rows imported (all already existed).');
        } else {
          this.notify.warn(`Import failed: ${r.errors.length} error(s).`);
        }
      },
      error: (err) => {
        this.importing.set(false);
        showHttpError(this.notify, err);
      },
    });
  }
}
