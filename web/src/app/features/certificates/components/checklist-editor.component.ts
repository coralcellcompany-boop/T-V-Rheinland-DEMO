import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';
import { DefectPicker } from './defect-picker.component';
import { DefectCode } from '../../../core/api/defects.api';

export type ChecklistResult = 'Pass' | 'Fail' | 'NA' | 'NotSet';

export interface ChecklistItem {
  itemNo: string;
  acceptanceCriteria: string;
  referenceStandard: string;
  result: ChecklistResult;
  remark: string;
}

export interface ChecklistDoc {
  items: ChecklistItem[];
  generatedFromTemplateId?: string | null;
}

const DEFAULT_ROW = (n: number): ChecklistItem => ({
  itemNo: String(n),
  acceptanceCriteria: '',
  referenceStandard: '',
  result: 'NotSet',
  remark: '',
});

const RESULT_OPTIONS = [
  { value: 'NotSet', label: '—' },
  { value: 'Pass', label: 'Pass' },
  { value: 'Fail', label: 'Fail' },
  { value: 'NA', label: 'N/A' },
];

@Component({
  selector: 'tuv-checklist-editor',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    ButtonModule, InputTextModule, SelectModule, TextareaModule, TooltipModule,
    DefectPicker,
  ],
  template: `
    <div class="header">
      <div class="counts">
        <span class="count" data-tone="success">
          <i class="pi pi-check"></i> {{ counts().pass }} pass
        </span>
        <span class="count" data-tone="danger">
          <i class="pi pi-times"></i> {{ counts().fail }} fail
        </span>
        <span class="count" data-tone="neutral">
          <i class="pi pi-minus"></i> {{ counts().na }} N/A
        </span>
        <span class="count" data-tone="warn">
          <i class="pi pi-question"></i> {{ counts().pending }} pending
        </span>
      </div>
      <div class="header-actions">
        <p-button *ngIf="!readonly" icon="pi pi-list" severity="secondary" size="small"
          label="Add from defect catalogue"
          [outlined]="true"
          (onClick)="openPicker()" />
        <p-button *ngIf="!readonly" icon="pi pi-plus" severity="secondary" size="small"
          label="Add row" (onClick)="addRow()" />
      </div>
    </div>

    <tuv-defect-picker
      [(open)]="pickerOpen"
      [equipmentTypeId]="equipmentTypeId"
      (picked)="onDefectPicked($event)" />

    @if (items().length === 0) {
      <div class="empty">
        <i class="pi pi-list"></i>
        <p>No checklist items yet.</p>
        <p-button *ngIf="!readonly" icon="pi pi-plus" label="Add first row" (onClick)="addRow()" />
      </div>
    } @else {
      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th class="num">#</th>
              <th>Acceptance criteria</th>
              <th class="ref">Reference standard</th>
              <th class="result">Result</th>
              <th>Remark</th>
              <th *ngIf="!readonly" class="actions"></th>
            </tr>
          </thead>
          <tbody>
            @for (item of items(); track item.itemNo + $index; let i = $index) {
              <tr [attr.data-result]="item.result">
                <td class="num">
                  <input pInputText [(ngModel)]="item.itemNo" [readonly]="readonly" />
                </td>
                <td>
                  <input pInputText [(ngModel)]="item.acceptanceCriteria"
                    [readonly]="readonly"
                    placeholder="What is being verified" />
                </td>
                <td class="ref">
                  <input pInputText [(ngModel)]="item.referenceStandard"
                    [readonly]="readonly"
                    placeholder="e.g. ASME B30.5 §5.2" />
                </td>
                <td class="result">
                  <p-select [options]="resultOptions" optionLabel="label" optionValue="value"
                    [(ngModel)]="item.result" [disabled]="readonly" appendTo="body" />
                </td>
                <td>
                  <input pInputText [(ngModel)]="item.remark"
                    [readonly]="readonly"
                    placeholder="Notes / measurements" />
                </td>
                <td *ngIf="!readonly" class="actions">
                  <p-button icon="pi pi-trash" severity="danger" [text]="true" rounded size="small"
                    (onClick)="removeRow(i)" pTooltip="Remove row" />
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }

    <div class="footer" *ngIf="!readonly">
      <small>Changes apply immediately to the form. Click <strong>Save checklist</strong> at the top to persist.</small>
      <div class="footer-actions">
        <p-button label="Save checklist" icon="pi pi-save"
          [loading]="saving()" [disabled]="saving()"
          (onClick)="emitSave()" />
      </div>
    </div>
  `,
  styles: [
    `
      :host { display: block; }
      .header {
        display: flex; justify-content: space-between; align-items: center;
        margin-bottom: 0.85rem; flex-wrap: wrap; gap: 0.5rem;
      }
      .counts { display: flex; gap: 0.5rem; flex-wrap: wrap; }
      .count {
        display: inline-flex; align-items: center; gap: 0.35rem;
        padding: 0.2rem 0.6rem; border-radius: 999px; font-size: 0.78rem; font-weight: 600;
        background: #f1f5f9; color: #475569;
      }
      .count[data-tone='success'] { background: #dcfce7; color: #047857; }
      .count[data-tone='danger']  { background: #fee2e2; color: #b91c1c; }
      .count[data-tone='warn']    { background: #fef3c7; color: #b45309; }
      .count[data-tone='neutral'] { background: #f1f5f9; color: #64748b; }

      .empty {
        text-align: center; padding: 2rem 1rem; color: #64748b;
        background: #f8fafc; border-radius: 12px; border: 1px dashed #cbd5e1;
      }
      .empty .pi { font-size: 1.6rem; color: #94a3b8; }
      .empty p { margin: 0.4rem 0 0.8rem; }

      .table-wrap { overflow-x: auto; border-radius: 10px; border: 1px solid #e5e9f2; }
      table { width: 100%; border-collapse: collapse; font-size: 0.86rem; min-width: 760px; }
      th, td { padding: 0.45rem 0.55rem; text-align: left; vertical-align: middle; }
      thead { background: #f8fafc; }
      thead th { font-size: 0.72rem; text-transform: uppercase; letter-spacing: 0.04em; color: #64748b; font-weight: 600; }
      tbody tr { border-top: 1px solid #f1f5f9; }
      tbody tr:hover { background: #fafbff; }
      tbody tr[data-result='Pass'] td:first-child { box-shadow: inset 3px 0 0 #10b981; }
      tbody tr[data-result='Fail'] td:first-child { box-shadow: inset 3px 0 0 #ef4444; }
      tbody tr[data-result='NA']   td:first-child { box-shadow: inset 3px 0 0 #94a3b8; }
      th.num, td.num { width: 70px; }
      th.ref, td.ref { width: 180px; }
      th.result, td.result { width: 130px; }
      th.actions, td.actions { width: 50px; text-align: right; }
      input { width: 100%; }
      :host ::ng-deep .p-select { width: 100%; }

      .footer {
        display: flex; justify-content: space-between; align-items: center;
        margin-top: 0.85rem; flex-wrap: wrap; gap: 0.5rem;
      }
      .footer small { color: #94a3b8; font-size: 0.78rem; }
    `,
  ],
})
export class ChecklistEditor {
  @Input() set value(json: string | null) { this.parse(json); }
  @Input() readonly = false;
  @Input() equipmentTypeId: string | null = null;
  @Output() save = new EventEmitter<string>();

  protected readonly resultOptions = RESULT_OPTIONS;
  protected readonly items = signal<ChecklistItem[]>([]);
  protected readonly saving = signal(false);
  protected pickerOpen = false;

  protected counts = computed(() => {
    let pass = 0, fail = 0, na = 0, pending = 0;
    for (const i of this.items()) {
      if (i.result === 'Pass') pass++;
      else if (i.result === 'Fail') fail++;
      else if (i.result === 'NA') na++;
      else pending++;
    }
    return { pass, fail, na, pending };
  });

  private parse(json: string | null) {
    if (!json) { this.items.set([]); return; }
    try {
      const parsed = JSON.parse(json) as ChecklistDoc;
      this.items.set(Array.isArray(parsed?.items) ? parsed.items : []);
    } catch {
      this.items.set([]);
    }
  }

  addRow() {
    const next = DEFAULT_ROW(this.items().length + 1);
    this.items.update((rows) => [...rows, next]);
  }

  removeRow(i: number) {
    this.items.update((rows) => rows.filter((_, idx) => idx !== i));
  }

  emitSave() {
    this.saving.set(true);
    const doc: ChecklistDoc = { items: this.items() };
    this.save.emit(JSON.stringify(doc));
    setTimeout(() => this.saving.set(false), 600);
  }

  openPicker() { this.pickerOpen = true; }

  onDefectPicked(d: DefectCode) {
    const next = this.items().length + 1;
    const row: ChecklistItem = {
      itemNo: String(next),
      acceptanceCriteria: d.description,
      referenceStandard: d.code,
      result: 'Fail',
      remark: `Defect ${d.code} (${d.severity})`,
    };
    this.items.update((rows) => [...rows, row]);
  }
}
