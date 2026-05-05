import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { SelectModule } from 'primeng/select';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { TextareaModule } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { toSignal } from '@angular/core/rxjs-interop';

import { PageHeader } from '../../../shared/components/page-header.component';
import { StatusPill } from '../../../shared/components/status-pill.component';
import { KpiCard } from '../../../shared/components/kpi-card.component';
import { EmptyState } from '../../../shared/components/empty-state.component';

import {
  PublicStickerApi,
  StickersApi,
} from '../../../core/api/stickers.api';
import {
  StickerColor,
  StickerColorHex,
  StickerColorName,
  StickerListItem,
  StickerStateName,
  StickerStockSummary,
} from '../../../core/models/sticker.models';
import { AuthService } from '../../../core/auth/auth.service';
import { Roles } from '../../../core/models/auth.models';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';

@Component({
  selector: 'tuv-stickers-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule, DatePipe,
    ButtonModule, TableModule, InputTextModule, IconFieldModule, InputIconModule,
    SelectModule, DialogModule, InputNumberModule, TextareaModule, TooltipModule,
    PageHeader, StatusPill, KpiCard, EmptyState,
  ],
  template: `
    <tuv-page-header title="Blue Stickers" icon="pi-qrcode"
      subtitle="Sticker stock register. Auto-issued when an Aramco-categorized certificate is approved.">
      <p-button icon="pi pi-print" label="Print batch" severity="secondary"
        [outlined]="true" [loading]="printingBatch()" (onClick)="printBatch()"
        pTooltip="Print up to 24 unallocated stickers per A4 page (each with QR)." />
      <p-button *ngIf="canProcure()" icon="pi pi-plus" label="Procure stock"
        (onClick)="procureDialog = true" />
    </tuv-page-header>

    <div class="kpis">
      <tuv-kpi-card label="Unallocated" icon="pi-tag" tone="primary"
        [value]="summary()?.unallocated" [loading]="loadingSummary()"
        hint="Ready to be auto-issued" />
      <tuv-kpi-card label="Issued" icon="pi-check-circle" tone="positive"
        [value]="summary()?.issued" [loading]="loadingSummary()"
        hint="Currently on equipment" />
      <tuv-kpi-card label="Voided" icon="pi-ban" tone="danger"
        [value]="summary()?.voided" [loading]="loadingSummary()" />
      <tuv-kpi-card label="Expired" icon="pi-hourglass" tone="warn"
        [value]="summary()?.expired" [loading]="loadingSummary()" />
    </div>

    <div class="filters">
      <p-iconfield iconPosition="left" class="grow">
        <p-inputicon styleClass="pi pi-search" />
        <input pInputText placeholder="Search by sticker number"
          [(ngModel)]="searchInput" (ngModelChange)="search$.next($event)" />
      </p-iconfield>

      <p-select [options]="stateOptions" optionLabel="label" optionValue="value"
        [(ngModel)]="filterState" (ngModelChange)="onFilterChange()"
        [showClear]="true" placeholder="Any state" appendTo="body" styleClass="filter" />

      <p-select [options]="colorOptions" optionLabel="label" optionValue="value"
        [(ngModel)]="filterColor" (ngModelChange)="onFilterChange()"
        [showClear]="true" placeholder="Any colour" appendTo="body" styleClass="filter" />
    </div>

    <div class="card">
      @if (loading()) {
        <div class="loader">Loading sticker register…</div>
      } @else if (rows().length === 0) {
        <tuv-empty-state icon="pi-qrcode" title="No stickers"
          message="Procure your first batch of sticker numbers (TUVR000001…). They auto-issue when a Blue Sticker certificate is approved.">
          <p-button *ngIf="canProcure()" icon="pi pi-plus" label="Procure stock"
            (onClick)="procureDialog = true" />
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
              <th style="width: 14%">Sticker</th>
              <th style="width: 70px">Colour</th>
              <th style="width: 13%">State</th>
              <th style="width: 14%">Assigned to</th>
              <th>Linked equipment / cert</th>
              <th style="width: 12%">Client</th>
              <th style="width: 11%">Valid until</th>
              <th style="width: 100px"></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-s>
            <tr>
              <td>
                <div class="sticker-no">{{ s.stickerNo }}</div>
                <div class="muted">{{ s.createdAtUtc | date: 'dd MMM yyyy' }}</div>
              </td>
              <td>
                <span class="color-chip" [style.background]="colorHex(s.color)"
                  [pTooltip]="colorName(s.color)"></span>
              </td>
              <td><tuv-status-pill [value]="stateName(s.state)" /></td>
              <td>
                <span *ngIf="s.assignedToInspectorName">{{ s.assignedToInspectorName }}</span>
                <span *ngIf="!s.assignedToInspectorName" class="muted">—</span>
              </td>
              <td>
                <div *ngIf="s.equipmentIdNo" class="equip-id">{{ s.equipmentIdNo }}</div>
                <div *ngIf="s.certificateNo" class="muted">{{ s.certificateNo }}</div>
                <span *ngIf="!s.equipmentIdNo && !s.certificateNo" class="muted">—</span>
              </td>
              <td>{{ s.clientName ?? '—' }}</td>
              <td>{{ s.validUntil ? (s.validUntil | date: 'dd MMM yyyy') : '—' }}</td>
              <td class="actions">
                <a [href]="qrUrl(s.stickerNo)" target="_blank" rel="noopener">
                  <p-button icon="pi pi-qrcode" severity="secondary" [text]="true" rounded
                    pTooltip="Open QR PNG" />
                </a>
                <p-button *ngIf="canVoid(s)" icon="pi pi-ban" severity="danger"
                  [text]="true" rounded
                  (onClick)="openVoid(s)" pTooltip="Void" />
              </td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>

    <p-dialog [(visible)]="procureDialog" [modal]="true" [style]="{ width: '460px' }"
      header="Procure sticker stock" [closable]="!procuring()">
      <div class="form">
        <p>Generate a fresh batch of <code>TUVR######</code> sticker numbers in the selected
          colour. They auto-issue when matching certificates are approved.</p>
        <label>Colour<span class="req">*</span></label>
        <div class="color-row">
          @for (opt of colorOptions; track opt.value) {
            <button type="button" class="color-btn"
              [class.selected]="procureColor === opt.value"
              (click)="procureColor = opt.value"
              [style.background]="colorHex(opt.value)"
              [attr.aria-label]="opt.label">
              <span>{{ opt.label }}</span>
            </button>
          }
        </div>
        <label>Count<span class="req">*</span></label>
        <p-inputNumber [(ngModel)]="procureCount" [min]="1" [max]="1000" [showButtons]="true" />
        <small>1–1000 stickers per batch.</small>
      </div>
      <ng-template pTemplate="footer">
        <p-button severity="secondary" label="Cancel" (onClick)="procureDialog = false" [disabled]="procuring()" />
        <p-button label="Procure" icon="pi pi-plus" [loading]="procuring()"
          (onClick)="procure()" />
      </ng-template>
    </p-dialog>

    <p-dialog [(visible)]="voidDialog" [modal]="true" [style]="{ width: '460px' }"
      header="Void sticker" [closable]="!voiding()">
      <div class="form">
        <p>Voiding <code>{{ voidTarget()?.stickerNo }}</code>. This is permanent and audited.</p>
        <label>Reason<span class="req">*</span></label>
        <textarea pTextarea rows="3" [(ngModel)]="voidReason"
          placeholder="e.g. Damaged during installation"></textarea>
      </div>
      <ng-template pTemplate="footer">
        <p-button severity="secondary" label="Cancel" (onClick)="closeVoid()" [disabled]="voiding()" />
        <p-button label="Void" icon="pi pi-ban" severity="danger" [loading]="voiding()"
          [disabled]="!voidReason.trim()" (onClick)="confirmVoid()" />
      </ng-template>
    </p-dialog>
  `,
  styles: [
    `
      :host { display: block; }
      .kpis {
        display: grid; gap: 1rem; margin-bottom: 1rem;
        grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      }
      .filters { display: flex; gap: 0.6rem; align-items: center; margin-bottom: 1rem; flex-wrap: wrap; }
      .filters .grow { flex: 1; min-width: 240px; max-width: 360px; }
      :host ::ng-deep .filter { width: 200px; }
      .card { background: #fff; border-radius: 14px; border: 1px solid #e5e9f2; padding: 1rem; }
      .loader { padding: 2rem; text-align: center; color: #64748b; }
      .sticker-no { font-family: ui-monospace, Menlo, monospace; font-weight: 600; color: #0f172a; }
      .equip-id { font-family: ui-monospace, Menlo, monospace; font-weight: 500; }
      .muted { color: #94a3b8; font-size: 0.78rem; margin-top: 0.15rem; }
      .actions { display: flex; gap: 0.2rem; }
      .color-chip {
        display: inline-block; width: 18px; height: 18px;
        border-radius: 50%; border: 1px solid rgba(15, 23, 42, 0.18);
      }
      .color-row { display: flex; gap: 0.4rem; flex-wrap: wrap; }
      .color-btn {
        flex: 1; min-width: 70px;
        display: flex; align-items: center; justify-content: center;
        padding: 0.6rem 0.4rem; border-radius: 10px;
        border: 2px solid transparent; cursor: pointer;
        color: #fff; font-size: 0.72rem; font-weight: 600;
        text-shadow: 0 1px 2px rgba(0, 0, 0, 0.4);
      }
      .color-btn.selected { border-color: #0f172a; box-shadow: 0 0 0 3px rgba(15, 23, 42, 0.18); }
      .color-btn[aria-label='White'] { color: #0f172a; text-shadow: none; }
      .form { display: flex; flex-direction: column; gap: 0.55rem; padding: 0.5rem 0; }
      .form label { font-size: 0.85rem; font-weight: 500; color: #334155; margin-top: 0.2rem; }
      .req { color: #dc2626; margin-left: 0.15rem; }
      .form small { color: #94a3b8; font-size: 0.75rem; }
      .form code { font-family: ui-monospace, Menlo, monospace; background: #f1f5f9; padding: 0.05rem 0.4rem; border-radius: 4px; }
      .form textarea { width: 100%; }
    `,
  ],
})
export class StickersListPage {
  private api = inject(StickersApi);
  private publicApi = inject(PublicStickerApi);
  protected auth = inject(AuthService);
  private notify = inject(NotifyService);

  protected loading = signal(true);
  protected loadingSummary = signal(true);
  protected rows = signal<StickerListItem[]>([]);
  protected total = signal(0);
  protected page = signal(1);
  protected pageSize = signal(25);
  protected summary = signal<StickerStockSummary | null>(null);

  protected searchInput = '';
  protected search$ = new Subject<string>();
  private searchSig = toSignal(this.search$.pipe(debounceTime(250), distinctUntilChanged()),
    { initialValue: '' });

  protected filterState: number | null = null;
  protected filterColor: number | null = null;
  protected procureColor: number = StickerColor.Blue;

  protected colorOptions = Object.entries(StickerColorName).map(([v, l]) => ({
    value: Number(v), label: l,
  }));
  protected colorName = (c: number) => StickerColorName[c] ?? 'Unknown';
  protected colorHex = (c: number) => StickerColorHex[c] ?? '#94a3b8';

  protected procureDialog = false;
  protected procuring = signal(false);
  protected procureCount = 50;

  protected voidDialog = false;
  protected voiding = signal(false);
  protected voidTarget = signal<StickerListItem | null>(null);
  protected voidReason = '';

  protected printingBatch = signal(false);

  protected canProcure = () => this.auth.hasRole(Roles.Manager);
  protected canVoid = (s: StickerListItem) =>
    this.auth.hasRole(Roles.Manager) && s.state !== 4 && s.state !== 5 && s.state !== 3;

  protected stateOptions = Object.entries(StickerStateName).map(([v, l]) => ({
    value: Number(v), label: l,
  }));

  protected stateName = (s: number) => StickerStateName[s] ?? 'Unknown';
  protected qrUrl = (no: string) => this.publicApi.qrUrl(no);

  constructor() {
    this.refreshSummary();
    effect(() => {
      const s = this.searchSig();
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
      state: this.filterState ?? undefined,
      color: this.filterColor ?? undefined,
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
        showHttpError(this.notify, err, 'Failed to load stickers');
      },
    });
  }

  private refreshSummary() {
    this.loadingSummary.set(true);
    this.api.stockSummary().subscribe({
      next: (s) => { this.summary.set(s); this.loadingSummary.set(false); },
      error: (err) => { this.loadingSummary.set(false); showHttpError(this.notify, err); },
    });
  }

  procure() {
    if (!this.procureCount || this.procureCount < 1) return;
    this.procuring.set(true);
    this.api.procure(this.procureCount, this.procureColor).subscribe({
      next: (r) => {
        this.procuring.set(false);
        this.procureDialog = false;
        this.notify.success(`Added ${r.added} ${this.colorName(this.procureColor)} sticker(s) to stock.`);
        this.refresh(1, this.pageSize(), this.searchSig());
        this.refreshSummary();
      },
      error: (err) => {
        this.procuring.set(false);
        showHttpError(this.notify, err);
      },
    });
  }

  openVoid(s: StickerListItem) {
    this.voidTarget.set(s);
    this.voidReason = '';
    this.voidDialog = true;
  }

  closeVoid() {
    this.voidDialog = false;
    this.voidTarget.set(null);
    this.voidReason = '';
  }

  confirmVoid() {
    const t = this.voidTarget();
    if (!t || !this.voidReason.trim()) return;
    this.voiding.set(true);
    this.api.void(t.id, this.voidReason.trim()).subscribe({
      next: () => {
        this.voiding.set(false);
        this.notify.success('Sticker voided.');
        this.closeVoid();
        this.refresh(this.page(), this.pageSize(), this.searchSig());
        this.refreshSummary();
      },
      error: (err) => {
        this.voiding.set(false);
        showHttpError(this.notify, err);
      },
    });
  }

  printBatch() {
    const state = this.filterState ?? 0; // default to Unallocated
    const color = this.filterColor ?? undefined;
    this.printingBatch.set(true);
    this.api.printBatch(state, color, 24).subscribe({
      next: (blob) => {
        this.printingBatch.set(false);
        const url = window.URL.createObjectURL(blob);
        window.open(url, '_blank');
        setTimeout(() => window.URL.revokeObjectURL(url), 60_000);
      },
      error: (err) => {
        this.printingBatch.set(false);
        showHttpError(this.notify, err, 'Could not generate sticker batch.');
      },
    });
  }
}
