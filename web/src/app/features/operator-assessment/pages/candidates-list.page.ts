import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { DrawerModule } from 'primeng/drawer';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { TooltipModule } from 'primeng/tooltip';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { toSignal } from '@angular/core/rxjs-interop';

import { PageHeader } from '../../../shared/components/page-header.component';
import { StatusPill } from '../../../shared/components/status-pill.component';
import { EmptyState } from '../../../shared/components/empty-state.component';

import { CandidatesApi } from '../../../core/api/assessments.api';
import { ClientsApi } from '../../../core/api/clients.api';
import { CandidateDetail, CandidateListItem } from '../../../core/models/assessment.models';
import { ClientListItem } from '../../../core/models/client.models';
import { AuthService } from '../../../core/auth/auth.service';
import { Roles } from '../../../core/models/auth.models';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';
import { CandidateForm } from '../components/candidate-form.component';

@Component({
  selector: 'tuv-candidates-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule, DatePipe,
    ButtonModule, TableModule, InputTextModule, IconFieldModule, InputIconModule,
    DrawerModule, DialogModule, SelectModule, TooltipModule,
    PageHeader, StatusPill, EmptyState, CandidateForm,
  ],
  template: `
    <tuv-page-header title="Candidates" icon="pi-id-card"
      subtitle="Operator registry. Each candidate can hold one or more competency assessments and cards.">
      <p-button *ngIf="canEdit()" icon="pi pi-upload" severity="secondary" [outlined]="true"
        label="Import" (onClick)="openImport()" />
      <p-button *ngIf="canEdit()" icon="pi pi-plus" label="New candidate" (onClick)="openNew()" />
    </tuv-page-header>

    <p-dialog [(visible)]="importOpen" [modal]="true" [style]="{ width: '480px' }"
      header="Import candidates from Excel" [closable]="!importing()">
      <div class="form">
        <p>Pick the client to import into:</p>
        <p-select [options]="clientOptions()" optionLabel="label" optionValue="value"
          [(ngModel)]="importClient" placeholder="Select client" appendTo="body" />
        <p>The first row must be the header. Required columns: <code>FullName</code>,
          <code>IdNumber</code>. Optional: <code>Phone</code>, <code>Email</code>,
          <code>EmployeeNo</code>, <code>Nationality</code>, <code>DateOfBirth</code>.
          Existing IdNumbers are skipped.</p>
        <input type="file" accept=".xlsx" (change)="onImportFileChange($event)"
          [disabled]="!importClient || importing()" />
        @if (importResult(); as r) {
          <div class="result">
            <strong>Imported {{ r.imported }}</strong>, skipped {{ r.skipped }}.
            @if (r.errors.length) {
              <ul>
                @for (e of r.errors.slice(0, 10); track e) { <li>{{ e }}</li> }
              </ul>
            }
          </div>
        }
      </div>
      <ng-template pTemplate="footer">
        <p-button severity="secondary" label="Close"
          (onClick)="importOpen = false" [disabled]="importing()" />
      </ng-template>
    </p-dialog>

    <div class="filters">
      <p-iconfield iconPosition="left" class="grow">
        <p-inputicon styleClass="pi pi-search" />
        <input pInputText placeholder="Search by name or ID/Iqama"
          [(ngModel)]="searchInput" (ngModelChange)="search$.next($event)" />
      </p-iconfield>

      <p-select [options]="clientOptions()" optionLabel="label" optionValue="value"
        [(ngModel)]="filterClient" (ngModelChange)="onFilterChange()"
        [showClear]="true" placeholder="All clients" appendTo="body" styleClass="filter" />
    </div>

    <div class="card">
      @if (loading()) {
        <div class="loader">Loading candidates…</div>
      } @else if (rows().length === 0) {
        <tuv-empty-state icon="pi-id-card" title="No candidates yet"
          message="Add the first operator/candidate so you can start assessments.">
          <p-button *ngIf="canEdit()" icon="pi pi-plus" label="New candidate" (onClick)="openNew()" />
        </tuv-empty-state>
      } @else {
        <p-table [value]="rows()" [rowHover]="true" styleClass="p-datatable-sm"
          [paginator]="true" [rows]="pageSize()" [totalRecords]="total()" [lazy]="true"
          (onLazyLoad)="onLazyLoad($event)" [rowsPerPageOptions]="[10, 25, 50, 100]">
          <ng-template pTemplate="header">
            <tr>
              <th>Candidate</th>
              <th style="width: 18%">ID / Iqama</th>
              <th style="width: 16%">Client</th>
              <th>Contact</th>
              <th style="width: 10%">Status</th>
              <th style="width: 12%">Created</th>
              <th style="width: 60px"></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-c>
            <tr>
              <td>
                <div class="name">{{ c.fullName }}</div>
                <div class="muted" *ngIf="c.nationality">{{ c.nationality }}</div>
              </td>
              <td><code>{{ c.identificationNumber }}</code></td>
              <td>{{ c.clientName }}</td>
              <td>
                <div *ngIf="c.email">{{ c.email }}</div>
                <div class="muted" *ngIf="c.phone">{{ c.phone }}</div>
                <span *ngIf="!c.email && !c.phone" class="muted">—</span>
              </td>
              <td>
                <tuv-status-pill *ngIf="c.isActive" value="Active" />
                <tuv-status-pill *ngIf="!c.isActive" value="Suspended" />
              </td>
              <td>{{ c.createdAtUtc | date: 'dd MMM yyyy' }}</td>
              <td>
                <p-button *ngIf="canEdit()" icon="pi pi-pencil" severity="secondary"
                  [text]="true" rounded (onClick)="openEdit(c)" pTooltip="Edit" />
              </td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>

    <p-drawer [(visible)]="drawerOpen" position="right" [style]="{ width: '600px' }"
      [showCloseIcon]="true" [modal]="true"
      header="{{ editing() ? 'Edit candidate' : 'New candidate' }}">
      <tuv-candidate-form *ngIf="drawerOpen"
        [editing]="editing()"
        [clients]="clients()"
        (save)="onSave($event)"
        (cancel)="closeDrawer()" />
    </p-drawer>
  `,
  styles: [
    `
      :host { display: block; }
      .filters { display: flex; gap: 0.6rem; align-items: center; margin-bottom: 1rem; flex-wrap: wrap; }
      .filters .grow { flex: 1; min-width: 220px; max-width: 360px; }
      :host ::ng-deep .filter { width: 200px; }
      .card { background: #fff; border-radius: 14px; border: 1px solid #e5e9f2; padding: 1rem; }
      .loader { padding: 2rem; text-align: center; color: #64748b; }
      .name { font-weight: 600; color: #0f172a; }
      .muted { color: #94a3b8; font-size: 0.78rem; margin-top: 0.15rem; }
      code { font-family: ui-monospace, Menlo, monospace; background: #f1f5f9; padding: 0.05rem 0.4rem; border-radius: 4px; font-size: 0.85rem; }
    `,
  ],
})
export class CandidatesListPage {
  private api = inject(CandidatesApi);
  private clientsApi = inject(ClientsApi);
  protected auth = inject(AuthService);
  private notify = inject(NotifyService);

  protected loading = signal(true);
  protected rows = signal<CandidateListItem[]>([]);
  protected total = signal(0);
  protected page = signal(1);
  protected pageSize = signal(25);

  protected clients = signal<ClientListItem[]>([]);
  protected clientOptions = computed(() => this.clients().map((c) => ({ label: c.name, value: c.id })));

  protected searchInput = '';
  protected search$ = new Subject<string>();
  private searchSig = toSignal(this.search$.pipe(debounceTime(250), distinctUntilChanged()),
    { initialValue: '' });

  protected filterClient: string | null = null;

  protected drawerOpen = false;
  protected editing = signal<CandidateDetail | null>(null);

  protected importOpen = false;
  protected importing = signal(false);
  protected importClient: string | null = null;
  protected importResult = signal<{ imported: number; skipped: number; errors: string[] } | null>(null);

  protected canEdit = () =>
    this.auth.hasRole(Roles.Manager) || this.auth.hasRole(Roles.Coordinator);

  openImport() {
    this.importResult.set(null);
    this.importClient = this.filterClient;
    this.importOpen = true;
  }

  onImportFileChange(e: Event) {
    const input = e.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file || !this.importClient) return;
    this.importing.set(true);
    this.api.import(this.importClient, file).subscribe({
      next: (r) => {
        this.importing.set(false);
        this.importResult.set(r);
        if (r.imported > 0) {
          this.notify.success(`Imported ${r.imported} candidate(s).`);
        }
        input.value = '';
      },
      error: (err) => {
        this.importing.set(false);
        showHttpError(this.notify, err);
        input.value = '';
      },
    });
  }

  constructor() {
    this.clientsApi.list({ pageSize: 200 }).subscribe({
      next: (r) => this.clients.set(r.items),
      error: (err) => showHttpError(this.notify, err),
    });
    effect(() => { const s = this.searchSig(); this.refresh(1, this.pageSize(), s); });
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
    }).subscribe({
      next: (res) => {
        this.rows.set(res.items); this.total.set(res.total);
        this.page.set(res.page); this.pageSize.set(res.pageSize);
        this.loading.set(false);
      },
      error: (err) => { this.loading.set(false); showHttpError(this.notify, err, 'Failed to load candidates'); },
    });
  }

  openNew() { this.editing.set(null); this.drawerOpen = true; }
  openEdit(row: CandidateListItem) {
    this.api.get(row.id).subscribe({
      next: (d) => { this.editing.set(d); this.drawerOpen = true; },
      error: (err) => showHttpError(this.notify, err),
    });
  }
  closeDrawer() { this.drawerOpen = false; this.editing.set(null); }

  onSave(payload: any) {
    const editing = this.editing();
    const obs = editing ? this.api.update(editing.id, payload) : this.api.create(payload);
    obs.subscribe({
      next: () => {
        this.notify.success(editing ? 'Candidate updated.' : 'Candidate created.');
        this.closeDrawer();
        this.refresh(this.page(), this.pageSize(), this.searchSig());
      },
      error: (err) => showHttpError(this.notify, err),
    });
  }
}
