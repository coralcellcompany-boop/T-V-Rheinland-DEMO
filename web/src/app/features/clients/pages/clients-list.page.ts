import { CommonModule } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { DrawerModule } from 'primeng/drawer';
import { ConfirmationService } from 'primeng/api';
import { DatePipe } from '@angular/common';
import { catchError, debounceTime, distinctUntilChanged, of, Subject, switchMap } from 'rxjs';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';

import { PageHeader } from '../../../shared/components/page-header.component';
import { StatusPill } from '../../../shared/components/status-pill.component';
import { EmptyState } from '../../../shared/components/empty-state.component';
import { ClientsApi } from '../../../core/api/clients.api';
import {
  ClientDetail,
  ClientListItem,
  ContractStatusLabel,
  CreateClientRequest,
  UpdateClientRequest,
} from '../../../core/models/client.models';
import { AuthService } from '../../../core/auth/auth.service';
import { Roles } from '../../../core/models/auth.models';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';
import { ClientForm } from '../components/client-form.component';

@Component({
  selector: 'tuv-clients-list',
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
    DrawerModule,
    PageHeader,
    StatusPill,
    EmptyState,
    ClientForm,
  ],
  template: `
    <tuv-page-header
      title="Clients"
      subtitle="Contractors and Aramco-affiliated companies for whom TÜV performs inspections."
      icon="pi-building">
      <p-iconfield iconPosition="left" class="search">
        <p-inputicon styleClass="pi pi-search" />
        <input pInputText placeholder="Search by name or code"
          [(ngModel)]="searchInput" (ngModelChange)="search$.next($event)" />
      </p-iconfield>
      <p-button *ngIf="canEdit()"
        icon="pi pi-plus" label="New client"
        (onClick)="openNew()" />
    </tuv-page-header>

    <div class="card">
      @if (loading()) {
        <div class="loader">Loading clients…</div>
      } @else if (rows().length === 0) {
        <tuv-empty-state
          icon="pi-building"
          title="No clients yet"
          message="Add your first client to start scoping equipment and inspections.">
          <p-button *ngIf="canEdit()"
            icon="pi pi-plus" label="New client" (onClick)="openNew()" />
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
              <th style="width: 28%">Name</th>
              <th style="width: 12%">Code</th>
              <th style="width: 14%">Status</th>
              <th>Contact</th>
              <th style="width: 14%">Created</th>
              <th style="width: 9%"></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-c>
            <tr>
              <td>
                <div class="name">{{ c.name }}</div>
                <div class="services">
                  <span *ngIf="hasService(c, 1)">TPI</span>
                  <span *ngIf="hasService(c, 2)">Blue Sticker</span>
                  <span *ngIf="hasService(c, 4)">Operator</span>
                </div>
              </td>
              <td><code>{{ c.code }}</code></td>
              <td>
                <tuv-status-pill [value]="contractStatusLabel(c.contractStatus)" />
              </td>
              <td>
                <div *ngIf="c.contactName">{{ c.contactName }}</div>
                <div class="muted" *ngIf="c.contactEmail">{{ c.contactEmail }}</div>
                <span class="muted" *ngIf="!c.contactName && !c.contactEmail">—</span>
              </td>
              <td>{{ c.createdAtUtc | date: 'dd MMM yyyy' }}</td>
              <td>
                <p-button *ngIf="canEdit()" icon="pi pi-pencil" severity="secondary" [text]="true" rounded
                  (onClick)="openEdit(c)" pTooltip="Edit" />
              </td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>

    <p-drawer
      [(visible)]="drawerOpen"
      position="right"
      [style]="{ width: '560px' }"
      [showCloseIcon]="true"
      [modal]="true"
      header="{{ editing() ? 'Edit client' : 'New client' }}">
      <tuv-client-form
        [editing]="editing()"
        (save)="onSave($event)"
        (cancel)="closeDrawer()" />
    </p-drawer>
  `,
  styles: [
    `
      :host { display: block; }
      .card { background: #fff; border-radius: 14px; border: 1px solid #e5e9f2; padding: 1rem; }
      .loader { padding: 2rem; text-align: center; color: #64748b; }
      .name { font-weight: 600; color: #0f172a; }
      .services { display: flex; gap: 0.4rem; margin-top: 0.2rem; }
      .services span {
        font-size: 0.7rem; padding: 0.05rem 0.45rem; border-radius: 999px;
        background: #eef2ff; color: #4338ca;
      }
      .muted { color: #94a3b8; font-size: 0.85rem; }
      code { font-size: 0.8rem; background: #f1f5f9; padding: 0.1rem 0.35rem; border-radius: 4px; }
      .search { width: 280px; }
      .search :host ::ng-deep input { width: 100%; }
    `,
  ],
})
export class ClientsListPage {
  protected api = inject(ClientsApi);
  protected auth = inject(AuthService);
  private notify = inject(NotifyService);
  private confirm = inject(ConfirmationService);

  protected loading = signal(true);
  protected rows = signal<ClientListItem[]>([]);
  protected total = signal(0);
  protected page = signal(1);
  protected pageSize = signal(25);

  protected searchInput = '';
  protected search$ = new Subject<string>();
  private searchSig = toSignal(
    this.search$.pipe(debounceTime(250), distinctUntilChanged()),
    { initialValue: '' },
  );

  protected drawerOpen = false;
  protected editing = signal<ClientDetail | null>(null);

  protected canEdit = () => this.auth.hasRole(Roles.Manager);
  protected contractStatusLabel = (s: number) => ContractStatusLabel[s] ?? 'Unknown';
  protected hasService = (c: ClientListItem, bit: number) => (c.allowedServices & bit) !== 0;

  constructor() {
    // Refetch when search changes
    effect(() => {
      const s = this.searchSig();
      this.refresh(1, this.pageSize(), s);
    });
  }

  onLazyLoad(e: any) {
    const page = Math.floor((e.first ?? 0) / (e.rows ?? this.pageSize())) + 1;
    this.refresh(page, e.rows ?? this.pageSize(), this.searchSig());
  }

  private refresh(page: number, pageSize: number, search: string) {
    this.loading.set(true);
    this.api.list({ page, pageSize, search: search?.trim() || undefined }).subscribe({
      next: (res) => {
        this.rows.set(res.items);
        this.total.set(res.total);
        this.page.set(res.page);
        this.pageSize.set(res.pageSize);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        showHttpError(this.notify, err, 'Failed to load clients');
      },
    });
  }

  openNew() {
    this.editing.set(null);
    this.drawerOpen = true;
  }

  openEdit(row: ClientListItem) {
    this.api.get(row.id).subscribe({
      next: (detail) => {
        this.editing.set(detail);
        this.drawerOpen = true;
      },
      error: (err) => showHttpError(this.notify, err),
    });
  }

  closeDrawer() {
    this.drawerOpen = false;
    this.editing.set(null);
  }

  onSave(payload: any) {
    const editing = this.editing();
    if (editing) {
      const body: UpdateClientRequest = {
        name: payload.name,
        address: payload.address,
        contactName: payload.contactName,
        contactPhone: payload.contactPhone,
        contactEmail: payload.contactEmail,
        contractStatus: payload.contractStatus,
        allowedServices: payload.allowedServices,
      };
      this.api.update(editing.id, body).subscribe({
        next: () => {
          this.notify.success('Client updated.');
          this.closeDrawer();
          this.refresh(this.page(), this.pageSize(), this.searchSig());
        },
        error: (err) => showHttpError(this.notify, err),
      });
    } else {
      const body: CreateClientRequest = payload;
      this.api.create(body).subscribe({
        next: () => {
          this.notify.success('Client created.');
          this.closeDrawer();
          this.refresh(1, this.pageSize(), '');
        },
        error: (err) => showHttpError(this.notify, err),
      });
    }
  }
}
