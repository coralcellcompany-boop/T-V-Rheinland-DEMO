import { CommonModule, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { TooltipModule } from 'primeng/tooltip';
import { TagModule } from 'primeng/tag';

import { StickersApi, StickerRequest } from '../../../core/api/stickers.api';
import { AuthService } from '../../../core/auth/auth.service';
import { Roles } from '../../../core/models/auth.models';
import {
  StickerColor, StickerColorHex, StickerColorName,
  StickerRequestState, StickerRequestStateName,
} from '../../../core/models/sticker.models';
import { PageHeader } from '../../../shared/components/page-header.component';
import { EmptyState } from '../../../shared/components/empty-state.component';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';

/**
 * Sticker requests workflow:
 *   - Inspectors raise requests for N stickers in a chosen colour with a justification.
 *   - Coordinator/Manager approves (allocates from the unallocated pool, assigns to the
 *     inspector) or rejects (with reason).
 *   - The inspector can cancel a still-pending request.
 */
@Component({
  selector: 'tuv-sticker-requests',
  standalone: true,
  imports: [
    CommonModule, FormsModule, DatePipe,
    ButtonModule, TableModule, DialogModule, InputTextModule, TextareaModule,
    InputNumberModule, SelectModule, TooltipModule, TagModule,
    PageHeader, EmptyState,
  ],
  template: `
    <tuv-page-header title="Sticker requests" icon="pi-inbox"
      subtitle="Inspectors request stickers; Coordinator/Manager allocates from the unallocated pool.">
      <p-button icon="pi pi-plus" label="Request stickers"
        [disabled]="!canRequest()" (onClick)="openCreate()" />
    </tuv-page-header>

    <div class="filters">
      <p-select [options]="stateOptions" optionLabel="label" optionValue="value"
        [(ngModel)]="filterState" (ngModelChange)="refresh(1)"
        [showClear]="true" placeholder="Any state" appendTo="body" styleClass="filter" />
      <span *ngIf="!canApprove() && me()" class="muted">Showing your requests only.</span>
    </div>

    <div class="card">
      @if (loading()) {
        <div class="loader">Loading requests…</div>
      } @else if (rows().length === 0) {
        <tuv-empty-state icon="pi-inbox" title="No requests yet"
          [message]="canApprove() ? 'Inspectors haven\\'t raised any sticker requests.' : 'You haven\\'t requested any stickers yet.'">
          <p-button *ngIf="canRequest()" icon="pi pi-plus" label="Request stickers" (onClick)="openCreate()" />
        </tuv-empty-state>
      } @else {
        <p-table [value]="rows()" dataKey="id" [rowHover]="true" styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th style="width: 14%">Request #</th>
              <th style="width: 16%">Inspector</th>
              <th style="width: 80px">Colour</th>
              <th style="width: 80px">Qty</th>
              <th style="width: 12%">State</th>
              <th>Justification / decision</th>
              <th style="width: 14%">Created</th>
              <th style="width: 130px"></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-r>
            <tr>
              <td><code>{{ r.requestNo }}</code></td>
              <td>{{ r.inspectorName ?? '—' }}</td>
              <td>
                <span class="color-chip" [style.background]="colorHex(r.color)"
                  [pTooltip]="colorName(r.color)"></span>
              </td>
              <td>
                <strong>{{ r.quantity }}</strong>
                <span *ngIf="r.state === 1" class="muted"> · {{ r.allocatedCount }} allocated</span>
              </td>
              <td>
                <p-tag [value]="stateName(r.state)" [severity]="stateSeverity(r.state)" />
              </td>
              <td>
                <div *ngIf="r.justification" class="just">{{ r.justification }}</div>
                <div *ngIf="r.decisionComments" class="muted decision">
                  {{ r.decidedByName ?? 'Decision' }}: {{ r.decisionComments }}
                </div>
              </td>
              <td>{{ r.createdAtUtc | date: 'dd MMM yyyy HH:mm' }}</td>
              <td class="actions">
                @if (r.state === 0 && canApprove()) {
                  <p-button icon="pi pi-check" severity="success" [text]="true" rounded size="small"
                    pTooltip="Approve & allocate" (onClick)="approve(r)" />
                  <p-button icon="pi pi-times" severity="danger" [text]="true" rounded size="small"
                    pTooltip="Reject" (onClick)="openReject(r)" />
                }
                @if (r.state === 0 && r.inspectorUserId === me()) {
                  <p-button icon="pi pi-trash" severity="secondary" [text]="true" rounded size="small"
                    pTooltip="Cancel" (onClick)="cancel(r)" />
                }
              </td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>

    <p-dialog [(visible)]="createOpen" [modal]="true" [style]="{ width: '480px' }"
      header="Request stickers" [closable]="!creating()">
      <div class="form">
        <label>Colour<span class="req">*</span></label>
        <div class="color-row">
          @for (opt of colorOptions; track opt.value) {
            <button type="button" class="color-btn"
              [class.selected]="newColor === opt.value"
              (click)="newColor = opt.value"
              [style.background]="colorHex(opt.value)"
              [attr.aria-label]="opt.label">
              <span>{{ opt.label }}</span>
            </button>
          }
        </div>
        <label>Quantity<span class="req">*</span></label>
        <p-inputNumber [(ngModel)]="newQuantity" [min]="1" [max]="500" [showButtons]="true" />
        <label>Justification (optional)</label>
        <textarea pTextarea rows="3" [(ngModel)]="newJustification"
          placeholder="e.g. Job order JOD2026-0042, 3-day visit at Yanbu refinery"></textarea>
      </div>
      <ng-template pTemplate="footer">
        <p-button severity="secondary" label="Cancel" (onClick)="createOpen = false" [disabled]="creating()" />
        <p-button label="Submit" icon="pi pi-send" [loading]="creating()" (onClick)="submit()" />
      </ng-template>
    </p-dialog>

    <p-dialog [(visible)]="rejectOpen" [modal]="true" [style]="{ width: '460px' }"
      header="Reject request" [closable]="!deciding()">
      <div class="form">
        <p>Rejecting <code>{{ rejectTarget()?.requestNo }}</code>.</p>
        <label>Reason<span class="req">*</span></label>
        <textarea pTextarea rows="3" [(ngModel)]="rejectReason"></textarea>
      </div>
      <ng-template pTemplate="footer">
        <p-button severity="secondary" label="Cancel" (onClick)="rejectOpen = false" [disabled]="deciding()" />
        <p-button label="Reject" icon="pi pi-times" severity="danger" [loading]="deciding()"
          [disabled]="!rejectReason.trim()" (onClick)="confirmReject()" />
      </ng-template>
    </p-dialog>
  `,
  styles: [
    `
      :host { display: block; }
      .filters { display: flex; gap: 0.6rem; align-items: center; margin-bottom: 1rem; flex-wrap: wrap; }
      :host ::ng-deep .filter { width: 220px; }
      .muted { color: #94a3b8; font-size: 0.78rem; }
      .card { background: #fff; border-radius: 14px; border: 1px solid #e5e9f2; padding: 1rem; }
      .loader { padding: 2rem; text-align: center; color: #64748b; }
      code { font-family: ui-monospace, Menlo, monospace; font-weight: 600; }
      .color-chip {
        display: inline-block; width: 18px; height: 18px;
        border-radius: 50%; border: 1px solid rgba(15, 23, 42, 0.18);
      }
      .actions { display: flex; gap: 0.2rem; }
      .just { font-size: 0.85rem; color: #334155; }
      .decision { margin-top: 0.2rem; }

      .form { display: flex; flex-direction: column; gap: 0.55rem; padding: 0.5rem 0; }
      .form label { font-size: 0.85rem; font-weight: 500; color: #334155; margin-top: 0.2rem; }
      .req { color: #dc2626; margin-left: 0.15rem; }
      .form textarea { width: 100%; }
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
    `,
  ],
})
export class StickerRequestsPage implements OnInit {
  private api = inject(StickersApi);
  protected auth = inject(AuthService);
  private notify = inject(NotifyService);

  protected loading = signal(true);
  protected rows = signal<StickerRequest[]>([]);
  protected filterState: number | null = null;

  protected createOpen = false;
  protected creating = signal(false);
  protected newColor: number = StickerColor.Blue;
  protected newQuantity = 25;
  protected newJustification = '';

  protected rejectOpen = false;
  protected rejectTarget = signal<StickerRequest | null>(null);
  protected rejectReason = '';
  protected deciding = signal(false);

  protected stateOptions = Object.entries(StickerRequestStateName).map(([v, l]) => ({
    value: Number(v), label: l,
  }));
  protected colorOptions = Object.entries(StickerColorName).map(([v, l]) => ({
    value: Number(v), label: l,
  }));
  protected colorName = (c: number) => StickerColorName[c] ?? 'Unknown';
  protected colorHex = (c: number) => StickerColorHex[c] ?? '#94a3b8';
  protected stateName = (s: number) => StickerRequestStateName[s] ?? 'Unknown';
  protected stateSeverity = (s: number): 'success' | 'info' | 'warn' | 'danger' | 'secondary' => {
    switch (s) {
      case StickerRequestState.Approved: return 'success';
      case StickerRequestState.Rejected: return 'danger';
      case StickerRequestState.Cancelled: return 'secondary';
      default: return 'warn';
    }
  };

  protected me = () => this.auth.user()?.id ?? '';
  protected canApprove = () => this.auth.hasRole(Roles.Manager) || this.auth.hasRole(Roles.Coordinator);
  protected canRequest = () =>
    this.auth.hasRole(Roles.Manager)
    || this.auth.hasRole(Roles.Coordinator)
    || this.auth.hasRole(Roles.Inspector);

  ngOnInit() { this.refresh(1); }

  refresh(_page: number) {
    this.loading.set(true);
    this.api.listRequests(this.filterState ?? undefined, undefined, 1, 100).subscribe({
      next: (r) => { this.rows.set(r.items); this.loading.set(false); },
      error: (err) => { this.loading.set(false); showHttpError(this.notify, err); },
    });
  }

  openCreate() {
    this.newColor = StickerColor.Blue;
    this.newQuantity = 25;
    this.newJustification = '';
    this.createOpen = true;
  }

  submit() {
    if (this.newQuantity < 1) return;
    this.creating.set(true);
    this.api.createRequest({
      color: this.newColor, quantity: this.newQuantity,
      justification: this.newJustification.trim() || null,
    }).subscribe({
      next: () => {
        this.creating.set(false);
        this.createOpen = false;
        this.notify.success('Sticker request submitted.');
        this.refresh(1);
      },
      error: (err) => { this.creating.set(false); showHttpError(this.notify, err); },
    });
  }

  approve(r: StickerRequest) {
    this.api.approveRequest(r.id, null).subscribe({
      next: (updated) => {
        const msg = updated.allocatedCount === updated.quantity
          ? `Approved · ${updated.allocatedCount} sticker(s) allocated.`
          : `Approved · ${updated.allocatedCount} of ${updated.quantity} allocated. Procure more stock to fulfil the rest.`;
        this.notify.success(msg);
        this.refresh(1);
      },
      error: (err) => showHttpError(this.notify, err),
    });
  }

  openReject(r: StickerRequest) {
    this.rejectTarget.set(r);
    this.rejectReason = '';
    this.rejectOpen = true;
  }

  confirmReject() {
    const t = this.rejectTarget();
    if (!t || !this.rejectReason.trim()) return;
    this.deciding.set(true);
    this.api.rejectRequest(t.id, this.rejectReason.trim()).subscribe({
      next: () => {
        this.deciding.set(false);
        this.rejectOpen = false;
        this.notify.success('Request rejected.');
        this.refresh(1);
      },
      error: (err) => { this.deciding.set(false); showHttpError(this.notify, err); },
    });
  }

  cancel(r: StickerRequest) {
    if (!confirm(`Cancel request ${r.requestNo}?`)) return;
    this.api.cancelRequest(r.id).subscribe({
      next: () => { this.notify.success('Request cancelled.'); this.refresh(1); },
      error: (err) => showHttpError(this.notify, err),
    });
  }
}
