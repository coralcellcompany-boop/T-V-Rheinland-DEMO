import { CommonModule, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { SelectModule } from 'primeng/select';
import { DialogModule } from 'primeng/dialog';
import { TagModule } from 'primeng/tag';

import { AuditApi, AuditLogRow } from '../../core/api/audit.api';
import { PageHeader } from '../../shared/components/page-header.component';
import { EmptyState } from '../../shared/components/empty-state.component';
import { NotifyService } from '../../shared/services/notify.service';
import { showHttpError } from '../../shared/services/api-error.handler';

const ENTITY_OPTIONS = [
  { value: '', label: 'Any entity' },
  { value: 'InspectionCertificate', label: 'Certificates' },
  { value: 'Equipment', label: 'Equipment' },
  { value: 'Client', label: 'Clients' },
  { value: 'Assessment', label: 'Assessments' },
  { value: 'CompetencyCard', label: 'Competency cards' },
  { value: 'Sticker', label: 'Stickers' },
  { value: 'JobOrder', label: 'Job orders' },
  { value: 'JobRequest', label: 'Job requests' },
];

@Component({
  selector: 'tuv-audit-log',
  standalone: true,
  imports: [
    CommonModule, FormsModule, DatePipe,
    ButtonModule, TableModule, InputTextModule, IconFieldModule, InputIconModule,
    SelectModule, DialogModule, TagModule,
    PageHeader, EmptyState,
  ],
  template: `
    <tuv-page-header title="Audit log" icon="pi-history"
      subtitle="Append-only, hash-chained record of every change. Manager-only.">
      <p-button icon="pi pi-refresh" severity="secondary" [outlined]="true" label="Refresh"
        (onClick)="refresh(1)" />
    </tuv-page-header>

    <div class="filters">
      <p-iconfield iconPosition="left" class="grow">
        <p-inputicon styleClass="pi pi-search" />
        <input pInputText placeholder="Search entity ID, action, JSON payload"
          [(ngModel)]="search" (ngModelChange)="onFilterChange()" />
      </p-iconfield>
      <p-select [options]="entityOptions" optionLabel="label" optionValue="value"
        [(ngModel)]="entityName" (ngModelChange)="onFilterChange()"
        placeholder="Any entity" appendTo="body" styleClass="filter" />
      <input pInputText type="date" [(ngModel)]="fromDate" (ngModelChange)="onFilterChange()"
        title="From (UTC)" />
      <input pInputText type="date" [(ngModel)]="toDate" (ngModelChange)="onFilterChange()"
        title="To (UTC)" />
    </div>

    <div class="card">
      @if (loading()) {
        <div class="loader">Loading audit log…</div>
      } @else if (rows().length === 0) {
        <tuv-empty-state icon="pi-history" title="No audit rows"
          message="No events match the current filters." />
      } @else {
        <p-table
          [value]="rows()"
          [paginator]="true"
          [rows]="pageSize()"
          [totalRecords]="total()"
          [lazy]="true"
          (onLazyLoad)="onLazyLoad($event)"
          [rowsPerPageOptions]="[25, 50, 100, 200]"
          dataKey="id"
          [rowHover]="true"
          styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th style="width: 14%">When (UTC)</th>
              <th style="width: 14%">Entity</th>
              <th style="width: 22%">ID</th>
              <th style="width: 10%">Action</th>
              <th style="width: 18%">Actor</th>
              <th></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-r>
            <tr>
              <td>
                <div class="when">{{ r.atUtc | date: 'dd MMM yyyy' }}</div>
                <div class="muted">{{ r.atUtc | date: 'HH:mm:ss' }}</div>
              </td>
              <td>{{ entityLabel(r.entityName) }}</td>
              <td><code>{{ r.entityId }}</code></td>
              <td>
                <p-tag [value]="r.action" [severity]="actionSeverity(r.action)" />
              </td>
              <td>
                <div *ngIf="r.actorUserName">{{ r.actorUserName }}</div>
                <div *ngIf="!r.actorUserName" class="muted">system</div>
                <div *ngIf="r.actorRole" class="muted">{{ r.actorRole }}</div>
              </td>
              <td>
                <p-button icon="pi pi-eye" severity="secondary" [text]="true" rounded size="small"
                  (onClick)="openDetail(r)" pTooltip="Inspect before/after" />
              </td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>

    <p-dialog [(visible)]="detailOpen" [modal]="true" [style]="{ width: '900px' }"
      [header]="detailHeader()">
      @if (detail(); as d) {
        <div class="detail">
          <div class="meta">
            <div><dt>When</dt><dd>{{ d.atUtc | date: 'dd MMM yyyy HH:mm:ss' }} UTC</dd></div>
            <div><dt>Entity</dt><dd>{{ entityLabel(d.entityName) }}</dd></div>
            <div><dt>ID</dt><dd><code>{{ d.entityId }}</code></dd></div>
            <div><dt>Action</dt><dd>{{ d.action }}</dd></div>
            <div><dt>Actor</dt><dd>{{ d.actorUserName ?? 'system' }}</dd></div>
            <div><dt>IP</dt><dd>{{ d.ip ?? '—' }}</dd></div>
            <div class="span2"><dt>Hash chain</dt>
              <dd><code class="hash">{{ d.previousHash }}</code> → <code class="hash">{{ d.currentHash }}</code></dd>
            </div>
          </div>
          <h4>Diff</h4>
          <div class="diff">
            <div>
              <h5>Before</h5>
              <pre><code>{{ pretty(d.beforeJson) }}</code></pre>
            </div>
            <div>
              <h5>After</h5>
              <pre><code>{{ pretty(d.afterJson) }}</code></pre>
            </div>
          </div>
        </div>
      }
    </p-dialog>
  `,
  styles: [
    `
      :host { display: block; }
      .filters { display: flex; gap: 0.6rem; align-items: center; margin-bottom: 1rem; flex-wrap: wrap; }
      .filters .grow { flex: 1; min-width: 240px; max-width: 360px; }
      :host ::ng-deep .filter { width: 220px; }
      .filters input[type='date'] { padding: 0.5rem 0.6rem; border-radius: 8px; border: 1px solid #cbd5e1; font-size: 0.85rem; }
      .card { background: #fff; border-radius: 14px; border: 1px solid #e5e9f2; padding: 1rem; }
      .loader { padding: 2rem; text-align: center; color: #64748b; }
      .when { font-weight: 500; }
      .muted { color: #94a3b8; font-size: 0.78rem; }
      code { font-family: ui-monospace, Menlo, monospace; font-size: 0.78rem; background: #f1f5f9; padding: 0.05rem 0.4rem; border-radius: 4px; word-break: break-all; }

      .detail .meta {
        display: grid; grid-template-columns: repeat(2, 1fr); gap: 0.6rem 1.4rem;
        margin-bottom: 1rem;
      }
      .detail .meta .span2 { grid-column: 1 / -1; }
      .detail .meta dt { font-size: 0.72rem; color: #94a3b8; text-transform: uppercase; letter-spacing: 0.04em; }
      .detail .meta dd { margin: 0.1rem 0 0; font-weight: 500; color: #0f172a; }
      .detail h4 { margin: 0.5rem 0 0.4rem; }
      .detail .diff { display: grid; grid-template-columns: 1fr 1fr; gap: 0.8rem; }
      .detail .diff h5 { margin: 0 0 0.3rem; font-size: 0.8rem; color: #64748b; }
      .detail .diff pre {
        margin: 0; padding: 0.6rem; background: #0f172a; color: #e2e8f0; border-radius: 8px;
        max-height: 50vh; overflow: auto; font-size: 0.72rem; white-space: pre-wrap; word-break: break-word;
      }
      .hash { font-size: 0.7rem; }
    `,
  ],
})
export class AuditLogPage implements OnInit {
  private api = inject(AuditApi);
  private notify = inject(NotifyService);

  protected entityOptions = ENTITY_OPTIONS;

  protected loading = signal(true);
  protected rows = signal<AuditLogRow[]>([]);
  protected total = signal(0);
  protected page = signal(1);
  protected pageSize = signal(50);

  protected search = '';
  protected entityName = '';
  protected fromDate = '';
  protected toDate = '';

  protected detailOpen = false;
  protected detail = signal<AuditLogRow | null>(null);
  protected detailHeader = computed(() => {
    const d = this.detail();
    return d ? `${d.action} · ${this.entityLabel(d.entityName)}` : '';
  });

  ngOnInit() { this.refresh(1); }

  protected onFilterChange() { this.refresh(1); }

  protected onLazyLoad(e: any) {
    const page = Math.floor((e.first ?? 0) / (e.rows ?? this.pageSize())) + 1;
    this.refresh(page, e.rows ?? this.pageSize());
  }

  refresh(page: number, pageSize?: number) {
    this.loading.set(true);
    this.api.list({
      page, pageSize: pageSize ?? this.pageSize(),
      entityName: this.entityName || undefined,
      search: this.search?.trim() || undefined,
      fromUtc: this.fromDate ? new Date(this.fromDate + 'T00:00:00Z').toISOString() : undefined,
      toUtc: this.toDate ? new Date(this.toDate + 'T23:59:59Z').toISOString() : undefined,
    }).subscribe({
      next: (r) => {
        this.rows.set(r.items);
        this.total.set(r.total);
        this.page.set(r.page);
        this.pageSize.set(r.pageSize);
        this.loading.set(false);
      },
      error: (err) => { this.loading.set(false); showHttpError(this.notify, err); },
    });
  }

  protected openDetail(r: AuditLogRow) {
    this.detail.set(r);
    this.detailOpen = true;
  }

  protected entityLabel(name: string): string {
    const found = ENTITY_OPTIONS.find((o) => o.value === name);
    return found?.label ?? name;
  }

  protected actionSeverity(action: string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' {
    const a = action.toLowerCase();
    if (a.startsWith('create')) return 'success';
    if (a.startsWith('update')) return 'info';
    if (a.startsWith('delete') || a.startsWith('void')) return 'danger';
    if (a.includes('transition') || a.includes('approv') || a.includes('reject')) return 'warn';
    return 'secondary';
  }

  protected pretty(json: string | null): string {
    if (!json) return '(none)';
    try { return JSON.stringify(JSON.parse(json), null, 2); }
    catch { return json; }
  }
}
