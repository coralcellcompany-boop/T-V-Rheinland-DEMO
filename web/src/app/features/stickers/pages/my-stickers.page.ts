import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';

import { PageHeader } from '../../../shared/components/page-header.component';
import { StatusPill } from '../../../shared/components/status-pill.component';
import { KpiCard } from '../../../shared/components/kpi-card.component';
import { EmptyState } from '../../../shared/components/empty-state.component';

import { PublicStickerApi, StickersApi } from '../../../core/api/stickers.api';
import {
  StickerColorHex,
  StickerColorName,
  StickerListItem,
  StickerStateName,
} from '../../../core/models/sticker.models';
import { AuthService } from '../../../core/auth/auth.service';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';

/**
 * Inspector's read-only view of stickers assigned to them. Drives off the same
 * /api/stickers list endpoint as the Manager/Coordinator stock register, but
 * pre-filtered to their own user id and stripped of procure/void/assign actions.
 */
@Component({
  selector: 'tuv-my-stickers',
  standalone: true,
  imports: [
    CommonModule, DatePipe,
    ButtonModule, TableModule, TooltipModule,
    PageHeader, StatusPill, KpiCard, EmptyState,
  ],
  template: `
    <tuv-page-header title="My stickers" icon="pi-qrcode"
      subtitle="Stickers reserved for you. Apply them to inspected equipment when the certificate is approved." />

    <div class="kpis">
      <tuv-kpi-card label="Available" icon="pi-tag" tone="primary"
        [value]="availableCount()" hint="Reserved for you, ready to issue" />
      <tuv-kpi-card label="Issued" icon="pi-check-circle" tone="positive"
        [value]="issuedCount()" hint="Already affixed to equipment" />
      <tuv-kpi-card label="Expired / Voided" icon="pi-hourglass" tone="warn"
        [value]="terminalCount()" />
    </div>

    <div class="card">
      @if (firstLoad()) {
        <div class="loader">Loading your stickers…</div>
      } @else if (rows().length === 0) {
        <tuv-empty-state icon="pi-inbox" title="No stickers assigned to you yet"
          message="Raise a sticker request from the Sticker requests page. Once a coordinator approves it, the stickers will appear here.">
        </tuv-empty-state>
      } @else {
        <p-table [value]="rows()" [rowHover]="true" styleClass="p-datatable-sm"
          [paginator]="true" [rows]="25" [rowsPerPageOptions]="[10, 25, 50]"
          [loading]="loading()" dataKey="id">
          <ng-template pTemplate="header">
            <tr>
              <th style="width: 14%">Sticker</th>
              <th style="width: 70px">Colour</th>
              <th style="width: 13%">State</th>
              <th>Linked equipment / cert</th>
              <th style="width: 14%">Client</th>
              <th style="width: 12%">Valid until</th>
              <th style="width: 70px"></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-s>
            <tr>
              <td>
                <div class="sticker-no">{{ s.stickerNo }}</div>
                <div class="muted">Created {{ s.createdAtUtc | date: 'dd MMM yyyy' }}</div>
              </td>
              <td>
                <span class="color-chip" [style.background]="colorHex(s.color)"
                  [pTooltip]="colorName(s.color)"></span>
              </td>
              <td><tuv-status-pill [value]="stateName(s.state)" /></td>
              <td>
                <div *ngIf="s.equipmentIdNo" class="equip-id">{{ s.equipmentIdNo }}</div>
                <div *ngIf="s.certificateNo" class="muted">{{ s.certificateNo }}</div>
                <span *ngIf="!s.equipmentIdNo && !s.certificateNo" class="muted">— not yet on equipment —</span>
              </td>
              <td>{{ s.clientName ?? '—' }}</td>
              <td>{{ s.validUntil ? (s.validUntil | date: 'dd MMM yyyy') : '—' }}</td>
              <td>
                <a [href]="qrUrl(s.stickerNo)" target="_blank" rel="noopener">
                  <p-button icon="pi pi-qrcode" severity="secondary" [text]="true" rounded
                    pTooltip="Open QR PNG" />
                </a>
              </td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>
  `,
  styles: [
    `
      :host { display: block; }
      .kpis {
        display: grid; gap: 1rem; margin-bottom: 1rem;
        grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      }
      .card { background: #fff; border-radius: 14px; border: 1px solid #e5e9f2; padding: 1rem; }
      .loader { padding: 2rem; text-align: center; color: #64748b; }
      .sticker-no { font-family: ui-monospace, Menlo, monospace; font-weight: 600; color: #0f172a; }
      .equip-id { font-family: ui-monospace, Menlo, monospace; font-weight: 500; }
      .muted { color: #94a3b8; font-size: 0.78rem; margin-top: 0.15rem; }
      .color-chip {
        display: inline-block; width: 18px; height: 18px;
        border-radius: 50%; border: 1px solid rgba(15, 23, 42, 0.18);
      }
    `,
  ],
})
export class MyStickersPage {
  private api = inject(StickersApi);
  private publicApi = inject(PublicStickerApi);
  private auth = inject(AuthService);
  private notify = inject(NotifyService);

  protected loading = signal(true);
  protected firstLoad = signal(true);
  protected rows = signal<StickerListItem[]>([]);

  protected availableCount = computed(() =>
    this.rows().filter((s) => s.state === 0).length);
  protected issuedCount = computed(() =>
    this.rows().filter((s) => s.state === 2).length);
  protected terminalCount = computed(() =>
    this.rows().filter((s) => s.state === 3 || s.state === 4 || s.state === 5).length);

  protected stateName = (s: number) => StickerStateName[s] ?? 'Unknown';
  protected colorName = (c: number) => StickerColorName[c] ?? 'Unknown';
  protected colorHex = (c: number) => StickerColorHex[c] ?? '#94a3b8';
  protected qrUrl = (no: string) => this.publicApi.qrUrl(no);

  constructor() {
    this.refresh();
  }

  private refresh() {
    const me = this.auth.user()?.id;
    if (!me) {
      this.loading.set(false);
      this.firstLoad.set(false);
      return;
    }
    this.loading.set(true);
    this.api.list({ assignedToInspectorId: me, pageSize: 200 }).subscribe({
      next: (res) => {
        this.rows.set(res.items);
        this.loading.set(false);
        this.firstLoad.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.firstLoad.set(false);
        showHttpError(this.notify, err, 'Failed to load your stickers');
      },
    });
  }
}
