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
import { PasswordModule } from 'primeng/password';
import { TooltipModule } from 'primeng/tooltip';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { toSignal } from '@angular/core/rxjs-interop';

import { PageHeader } from '../../../shared/components/page-header.component';
import { StatusPill } from '../../../shared/components/status-pill.component';
import { EmptyState } from '../../../shared/components/empty-state.component';

import { UsersApi } from '../../../core/api/users.api';
import { ClientsApi } from '../../../core/api/clients.api';
import { ClientListItem } from '../../../core/models/client.models';
import { UserListItem } from '../../../core/models/user.models';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';
import { UserForm } from '../components/user-form.component';

@Component({
  selector: 'tuv-users-admin',
  standalone: true,
  imports: [
    CommonModule, FormsModule, DatePipe,
    ButtonModule, TableModule, InputTextModule, IconFieldModule, InputIconModule,
    DrawerModule, DialogModule, PasswordModule, TooltipModule,
    PageHeader, StatusPill, EmptyState, UserForm,
  ],
  template: `
    <tuv-page-header title="Users & roles" icon="pi-users"
      subtitle="Manager-only · Add team members, assign roles and clients, reset passwords.">
      <p-iconfield iconPosition="left" class="search">
        <p-inputicon styleClass="pi pi-search" />
        <input pInputText placeholder="Search users"
          [(ngModel)]="searchInput" (ngModelChange)="search$.next($event)" />
      </p-iconfield>
      <p-button icon="pi pi-plus" label="New user" (onClick)="openNew()" />
    </tuv-page-header>

    <div class="card">
      @if (loading()) {
        <div class="loader">Loading users…</div>
      } @else if (users().length === 0) {
        <tuv-empty-state icon="pi-users" title="No users match your search"
          message="Add a teammate to get them an account.">
          <p-button icon="pi pi-plus" label="New user" (onClick)="openNew()" />
        </tuv-empty-state>
      } @else {
        <p-table [value]="users()" [rowHover]="true" styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th style="width: 26%">User</th>
              <th style="width: 13%">SAP / Cert</th>
              <th>Roles</th>
              <th style="width: 10%">Clients</th>
              <th style="width: 10%">Status</th>
              <th style="width: 14%">Created</th>
              <th style="width: 100px"></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-u>
            <tr>
              <td>
                <div class="name">{{ u.fullName ?? u.userName }}</div>
                <div class="muted">{{ u.email }}</div>
              </td>
              <td>
                <div *ngIf="u.sapNo">SAP {{ u.sapNo }}</div>
                <div *ngIf="u.certNo" class="muted">{{ u.certNo }}</div>
                <span *ngIf="!u.sapNo && !u.certNo" class="muted">—</span>
              </td>
              <td>
                <div class="role-chips">
                  <span *ngFor="let r of u.roles" class="chip" [attr.data-role]="r">{{ r }}</span>
                </div>
              </td>
              <td>
                <span *ngIf="u.assignedClientIds.length === 0" class="muted">All / none</span>
                <span *ngIf="u.assignedClientIds.length > 0">
                  {{ u.assignedClientIds.length }} client{{ u.assignedClientIds.length > 1 ? 's' : '' }}
                </span>
              </td>
              <td>
                <tuv-status-pill *ngIf="u.isActive && !u.isLockedOut" value="Active" />
                <tuv-status-pill *ngIf="!u.isActive" value="Suspended" />
                <tuv-status-pill *ngIf="u.isLockedOut" value="Terminated" />
              </td>
              <td>{{ u.createdAtUtc | date: 'dd MMM yyyy' }}</td>
              <td class="actions">
                <p-button icon="pi pi-pencil" severity="secondary" [text]="true" rounded
                  (onClick)="openEdit(u)" pTooltip="Edit" />
                <p-button icon="pi pi-key" severity="secondary" [text]="true" rounded
                  (onClick)="openReset(u)" pTooltip="Reset password" />
              </td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>

    <p-drawer [(visible)]="drawerOpen" position="right" [style]="{ width: '600px' }"
      [showCloseIcon]="true" [modal]="true"
      header="{{ editing() ? 'Edit user' : 'New user' }}">
      <tuv-user-form *ngIf="drawerOpen"
        [editing]="editing()"
        [roleOptions]="roles()"
        [clientOptions]="clients()"
        (save)="onSave($event)"
        (cancel)="closeDrawer()" />
    </p-drawer>

    <p-dialog [(visible)]="resetDialog" [modal]="true" [style]="{ width: '420px' }"
      header="Reset password" [closable]="!resetting()">
      <div class="reset-form">
        <p>Resetting password for <strong>{{ resetTarget()?.email ?? resetTarget()?.userName }}</strong>.</p>
        <label>New password<span class="req">*</span></label>
        <p-password [(ngModel)]="newPassword" [feedback]="true" [toggleMask]="true"
          inputStyleClass="pw-input" styleClass="pw" />
        <small>Min 12 chars, with upper, lower, digit, special. Communicate this to the user securely.</small>
      </div>
      <ng-template pTemplate="footer">
        <p-button severity="secondary" label="Cancel" (onClick)="closeReset()" [disabled]="resetting()" />
        <p-button label="Reset" icon="pi pi-key" severity="warn" [loading]="resetting()"
          [disabled]="!newPassword || newPassword.length < 12" (onClick)="confirmReset()" />
      </ng-template>
    </p-dialog>
  `,
  styles: [
    `
      :host { display: block; }
      .search { width: 260px; }
      .card { background: #fff; border-radius: 14px; border: 1px solid #e5e9f2; padding: 1rem; }
      .loader { padding: 2rem; text-align: center; color: #64748b; }
      .name { font-weight: 600; color: #0f172a; }
      .muted { color: #94a3b8; font-size: 0.82rem; margin-top: 0.15rem; }
      .role-chips { display: flex; flex-wrap: wrap; gap: 0.3rem; }
      .chip {
        padding: 0.1rem 0.55rem; border-radius: 999px; font-size: 0.72rem; font-weight: 500;
        background: #eef2ff; color: #4338ca; border: 1px solid #c7d2fe;
      }
      .chip[data-role='Manager']      { background: #fef3c7; color: #b45309; border-color: #fde68a; }
      .chip[data-role='Coordinator']  { background: #e0f2fe; color: #075985; border-color: #bae6fd; }
      .chip[data-role='Inspector']    { background: #dcfce7; color: #047857; border-color: #86efac; }
      .chip[data-role='TechReviewer'] { background: #f3e8ff; color: #6d28d9; border-color: #e9d5ff; }
      .chip[data-role='ClientUser']   { background: #fee2e2; color: #b91c1c; border-color: #fca5a5; }
      .actions { display: flex; gap: 0.2rem; }

      .reset-form { display: flex; flex-direction: column; gap: 0.5rem; padding: 0.4rem 0; }
      .reset-form label { font-size: 0.85rem; font-weight: 500; color: #334155; }
      .reset-form .req { color: #dc2626; }
      .reset-form small { color: #94a3b8; font-size: 0.75rem; margin-top: 0.2rem; }
      :host ::ng-deep .pw, :host ::ng-deep .pw input { width: 100%; }
      :host ::ng-deep .pw-input { width: 100%; }
    `,
  ],
})
export class UsersAdminPage {
  private api = inject(UsersApi);
  private clientsApi = inject(ClientsApi);
  private notify = inject(NotifyService);

  protected loading = signal(true);
  protected users = signal<UserListItem[]>([]);
  protected roles = signal<string[]>([]);
  protected clients = signal<ClientListItem[]>([]);

  protected searchInput = '';
  protected search$ = new Subject<string>();
  private searchSig = toSignal(this.search$.pipe(debounceTime(250), distinctUntilChanged()),
    { initialValue: '' });

  protected drawerOpen = false;
  protected editing = signal<UserListItem | null>(null);

  protected resetDialog = false;
  protected resetTarget = signal<UserListItem | null>(null);
  protected newPassword = '';
  protected resetting = signal(false);

  constructor() {
    this.api.roles().subscribe({ next: (r) => this.roles.set(r) });
    this.clientsApi.list({ pageSize: 200 }).subscribe({
      next: (r) => this.clients.set(r.items),
      error: (err) => showHttpError(this.notify, err),
    });

    effect(() => {
      const s = this.searchSig();
      this.refresh(s);
    });
  }

  private refresh(search: string) {
    this.loading.set(true);
    this.api.list(search?.trim() || undefined).subscribe({
      next: (rows) => { this.users.set(rows); this.loading.set(false); },
      error: (err) => { this.loading.set(false); showHttpError(this.notify, err); },
    });
  }

  openNew() { this.editing.set(null); this.drawerOpen = true; }
  openEdit(u: UserListItem) { this.editing.set(u); this.drawerOpen = true; }
  closeDrawer() { this.drawerOpen = false; this.editing.set(null); }

  onSave(payload: any) {
    const editing = this.editing();
    if (editing) {
      this.api.update(editing.id, payload).subscribe({
        next: () => {
          this.notify.success('User updated.');
          this.closeDrawer();
          this.refresh(this.searchSig());
        },
        error: (err) => showHttpError(this.notify, err),
      });
    } else {
      this.api.create(payload).subscribe({
        next: () => {
          this.notify.success('User created.');
          this.closeDrawer();
          this.refresh('');
        },
        error: (err) => showHttpError(this.notify, err),
      });
    }
  }

  openReset(u: UserListItem) {
    this.resetTarget.set(u);
    this.newPassword = '';
    this.resetDialog = true;
  }

  closeReset() {
    this.resetDialog = false;
    this.resetTarget.set(null);
    this.newPassword = '';
  }

  confirmReset() {
    const u = this.resetTarget();
    if (!u || !this.newPassword) return;
    this.resetting.set(true);
    this.api.resetPassword(u.id, this.newPassword).subscribe({
      next: () => {
        this.resetting.set(false);
        this.notify.success('Password reset. Communicate it to the user securely.');
        this.closeReset();
      },
      error: (err) => {
        this.resetting.set(false);
        showHttpError(this.notify, err);
      },
    });
  }
}
