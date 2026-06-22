import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { CheckboxModule } from 'primeng/checkbox';
import { TagModule } from 'primeng/tag';
import { BlueStickerApi } from '../../../core/api/blue-sticker.api';
import { JobOrdersApi } from '../../../core/api/job-management.api';
import { EquipmentApi } from '../../../core/api/equipment.api';
import { EquipmentListItem } from '../../../core/models/equipment.models';
import { JobOrderListItem, JobOrderStatus } from '../../../core/models/job-management.models';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';

/**
 * Coordinator-facing dialog that creates Blue Sticker reports for a chosen job order.
 * Replaces the previous "auto-include every Aramco equipment under the client" behaviour —
 * the coordinator now picks exactly which equipment gets a report this trip + sets the
 * Aramco admin fields (Org Code, RPO, CRM, Department/Contractor) once for the batch.
 */
@Component({
  selector: 'tuv-blue-sticker-create-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, ButtonModule, InputTextModule,
    SelectModule, CheckboxModule, TagModule],
  template: `
    <p-dialog [(visible)]="visible" [modal]="true" [style]="{ width: '720px' }"
      header="New Blue Sticker batch" [closable]="!busy()"
      (onHide)="cancel.emit()">

      <div class="form">
        <label class="row">
          <span>Job Order<span class="req">*</span></span>
          <p-select [options]="jobOrders()" optionLabel="label" optionValue="id"
            [(ngModel)]="selectedJobOrderId" (onChange)="onJobOrderChange()"
            placeholder="Pick a job order…" [filter]="true" filterBy="label"
            appendTo="body" [loading]="loadingJos()" />
        </label>

        <fieldset class="admin">
          <legend>Aramco administrative info</legend>
          <div class="grid">
            <label>Org. Code
              <input pInputText [(ngModel)]="form.orgCode" /></label>
            <label>RPO No.
              <input pInputText [(ngModel)]="form.rpoNo" /></label>
            <label>CRM No.
              <input pInputText [(ngModel)]="form.crmNo" /></label>
            <label>Department / Contractor
              <input pInputText [(ngModel)]="form.departmentContractor" /></label>
          </div>
        </fieldset>

        <fieldset class="equipment">
          <legend>Equipment to inspect<span class="req">*</span></legend>
          @if (!selectedJobOrderId) {
            <p class="hint">Pick a job order first to load its Aramco-categorised equipment.</p>
          } @else if (loadingEquip()) {
            <p class="hint">Loading equipment…</p>
          } @else if (equipment().length === 0) {
            <p class="hint warn">No Aramco-categorised equipment found for this client.
              Add equipment + set its Aramco category before creating a Blue Sticker batch.</p>
          } @else {
            <div class="select-all">
              <p-checkbox [binary]="true" [ngModel]="allSelected()"
                (ngModelChange)="toggleAll($event)" inputId="all" />
              <label for="all">Select all ({{ equipment().length }})</label>
            </div>
            <ul class="equip-list">
              @for (e of equipment(); track e.id) {
                <li>
                  <p-checkbox [binary]="true" [ngModel]="selectedIds().has(e.id)"
                    (ngModelChange)="toggle(e.id, $event)" [inputId]="'eq-' + e.id" />
                  <label [for]="'eq-' + e.id">
                    <span class="eid">{{ e.idNo }}</span>
                    <span class="etype">{{ e.equipmentTypeName }}</span>
                    <p-tag [value]="categoryShort(e.aramcoCategory)" severity="info" />
                    @if (e.serialNo) { <span class="serial">S/N {{ e.serialNo }}</span> }
                    @if (e.location) { <span class="loc">{{ e.location }}</span> }
                  </label>
                </li>
              }
            </ul>
            <p class="hint">{{ selectedIds().size }} of {{ equipment().length }} selected</p>
          }
        </fieldset>
      </div>

      <ng-template pTemplate="footer">
        <p-button label="Cancel" severity="secondary" (onClick)="cancel.emit()"
          [disabled]="busy()" />
        <p-button label="Create {{ selectedIds().size || '' }} report(s)" icon="pi pi-plus"
          [loading]="busy()"
          [disabled]="!canSubmit()"
          (onClick)="submit()" />
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .form{display:flex;flex-direction:column;gap:1rem;padding:.4rem 0}
    .row{display:flex;flex-direction:column;gap:.3rem}
    .row > span{font-size:.85rem;color:#475569;font-weight:500}
    .req{color:#dc2626;margin-left:.15rem}
    fieldset{border:1px solid #e5e9f2;border-radius:10px;padding:.7rem 1rem .9rem}
    legend{font-size:.78rem;color:#475569;font-weight:600;padding:0 .35rem;
      text-transform:uppercase;letter-spacing:.04em}
    .grid{display:grid;grid-template-columns:1fr 1fr;gap:.5rem 1rem}
    .grid label{display:flex;flex-direction:column;gap:.2rem;font-size:.78rem;color:#475569}
    .grid input{font-size:.92rem}
    .select-all{display:flex;align-items:center;gap:.5rem;
      padding:.4rem 0 .5rem;border-bottom:1px dashed #e5e9f2;margin-bottom:.5rem}
    .equip-list{list-style:none;padding:0;margin:0;display:flex;flex-direction:column;
      gap:.3rem;max-height:280px;overflow-y:auto}
    .equip-list li{display:flex;align-items:center;gap:.5rem;
      padding:.4rem .55rem;border-radius:8px;border:1px solid transparent}
    .equip-list li:hover{background:#f8fafc;border-color:#e5e9f2}
    .equip-list label{display:flex;align-items:center;gap:.55rem;cursor:pointer;flex:1}
    .eid{font-weight:600;color:#0f172a}
    .etype{color:#475569;font-size:.88rem}
    .serial,.loc{font-size:.75rem;color:#64748b}
    .hint{color:#64748b;font-size:.85rem;margin:.4rem 0 0}
    .hint.warn{color:#92400e}
  `],
})
export class BlueStickerCreateDialog {
  @Input() set open(value: boolean) {
    this.visible = value;
    if (value) this.reset();
  }
  @Output() created = new EventEmitter<number>();   // emits the number of reports created
  @Output() cancel = new EventEmitter<void>();

  private blueApi = inject(BlueStickerApi);
  private joApi = inject(JobOrdersApi);
  private equipApi = inject(EquipmentApi);
  private notify = inject(NotifyService);

  protected visible = false;
  protected busy = signal(false);
  protected loadingJos = signal(false);
  protected loadingEquip = signal(false);
  protected jobOrders = signal<{ id: string; label: string; clientId: string }[]>([]);
  protected equipment = signal<EquipmentListItem[]>([]);
  protected selectedIds = signal<Set<string>>(new Set());
  protected selectedJobOrderId: string | null = null;
  protected form: { orgCode: string; rpoNo: string; crmNo: string; departmentContractor: string } = {
    orgCode: '', rpoNo: '', crmNo: '', departmentContractor: '',
  };

  constructor() {
    this.loadJobOrders();
  }

  private reset() {
    this.selectedJobOrderId = null;
    this.equipment.set([]);
    this.selectedIds.set(new Set());
    this.form = { orgCode: '', rpoNo: '', crmNo: '', departmentContractor: '' };
  }

  private loadJobOrders() {
    this.loadingJos.set(true);
    // Open / InProgress job orders are the ones a coordinator would batch reports against;
    // Completed/Cancelled are excluded.
    this.joApi.list({ pageSize: 200 }).subscribe({
      next: (res) => {
        const active = res.items.filter(j =>
          j.status === JobOrderStatus.Open || j.status === JobOrderStatus.InProgress);
        this.jobOrders.set(active.map(j => ({
          id: j.id,
          clientId: j.clientId,
          label: `${j.jobOrderNo} — ${j.clientName}` + (j.location ? ` · ${j.location}` : ''),
        })));
        this.loadingJos.set(false);
      },
      error: (e) => { this.loadingJos.set(false); showHttpError(this.notify, e); },
    });
  }

  onJobOrderChange() {
    this.selectedIds.set(new Set());
    const jo = this.jobOrders().find(j => j.id === this.selectedJobOrderId);
    if (!jo) { this.equipment.set([]); return; }
    this.loadingEquip.set(true);
    // Pull every Aramco-categorised equipment for the JO's client; one row per equipment.
    // We rely on `aramcoCategory != null` rows only — the list endpoint doesn't expose an
    // "Aramco only" flag, so we filter client-side after the round-trip.
    this.equipApi.list({ clientId: jo.clientId, pageSize: 500 }).subscribe({
      next: (res) => {
        const aramco = res.items.filter(e => e.aramcoCategory != null && e.aramcoCategory !== 0);
        this.equipment.set(aramco);
        // Pre-select everything by default — coordinator can untick what they don't want.
        this.selectedIds.set(new Set(aramco.map(e => e.id)));
        this.loadingEquip.set(false);
      },
      error: (e) => { this.loadingEquip.set(false); showHttpError(this.notify, e); },
    });
  }

  protected allSelected(): boolean {
    const eq = this.equipment();
    return eq.length > 0 && this.selectedIds().size === eq.length;
  }

  protected toggleAll(checked: boolean) {
    this.selectedIds.set(checked
      ? new Set(this.equipment().map(e => e.id))
      : new Set());
  }

  protected toggle(id: string, checked: boolean) {
    const next = new Set(this.selectedIds());
    if (checked) next.add(id); else next.delete(id);
    this.selectedIds.set(next);
  }

  protected categoryShort(c: number | null): string {
    if (c == null || c === 0) return '—';
    return 'CR' + String(c).padStart(2, '0');
  }

  protected canSubmit(): boolean {
    return !!this.selectedJobOrderId && this.selectedIds().size > 0 && !this.busy();
  }

  submit() {
    if (!this.canSubmit()) return;
    this.busy.set(true);
    this.blueApi.create({
      jobOrderId: this.selectedJobOrderId!,
      orgCode: this.form.orgCode || null,
      rpoNo: this.form.rpoNo || null,
      crmNo: this.form.crmNo || null,
      departmentContractor: this.form.departmentContractor || null,
      equipmentIds: Array.from(this.selectedIds()),
    }).subscribe({
      next: (reports) => {
        this.busy.set(false);
        this.notify.success(`Created ${reports.length} report(s).`);
        this.created.emit(reports.length);
      },
      error: (e) => { this.busy.set(false); showHttpError(this.notify, e); },
    });
  }
}
