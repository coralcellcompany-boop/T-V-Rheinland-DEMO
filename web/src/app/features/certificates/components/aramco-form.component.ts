import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnInit, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { CheckboxModule } from 'primeng/checkbox';

export enum DeficiencySeverity {
  Minor = 0,
  Major = 1,
  Critical = 2,
}

export interface DeficiencyItem {
  code: string | null;
  description: string | null;
  severity: DeficiencySeverity;
  correctiveAction: string | null;
  deadline: string | null;          // "YYYY-MM-DD"
  resolved: boolean;
}

/**
 * Aramco Annex 1 (MS0053813) inspection report fields.
 *
 * Persisted as JSON in <c>InspectionCertificate.AramcoReportJson</c> and unfolded by
 * AramcoReportPdfRenderer when the PDF is requested. The shape mirrors the
 * AramcoReportData record on the backend exactly.
 */
export interface AramcoFormDoc {
  tuvJobOrderNo: string | null;
  aramcoCategoryNo: string | null;
  orgCode: string | null;
  rpoNo: string | null;
  crmNo: string | null;
  reportNo: string | null;
  departmentContractor: string | null;
  inspectionTime: string | null;          // "HH:mm"
  previousStickerNo: string | null;
  previousStickerIssuedBy: string | null;
  areaOfInspection: string | null;
  capacity: string | null;
  equipmentLocationOnSite: string | null;
  manufacturer: string | null;
  model: string | null;
  equipmentType: string | null;
  equipmentSerialNo: string | null;
  stickerExpirationDate: string | null;   // "YYYY-MM-DD"
  receiverName: string | null;
  receiverBadgeNo: string | null;
  receiverTelephone: string | null;
  inspectorTelephone: string | null;
  receivedDate: string | null;            // "YYYY-MM-DD"
  reviewedDate: string | null;            // "YYYY-MM-DD"
  deficiencies: string | null;
  correctiveActionsTaken: string | null;
  deficiencyItems: DeficiencyItem[];
}

const EMPTY: AramcoFormDoc = {
  tuvJobOrderNo: null, aramcoCategoryNo: null, orgCode: null, rpoNo: null,
  crmNo: null, reportNo: null, departmentContractor: null, inspectionTime: null,
  previousStickerNo: null, previousStickerIssuedBy: null,
  areaOfInspection: null, capacity: null, equipmentLocationOnSite: null,
  manufacturer: null, model: null, equipmentType: null, equipmentSerialNo: null, stickerExpirationDate: null,
  receiverName: null, receiverBadgeNo: null, receiverTelephone: null,
  inspectorTelephone: null, receivedDate: null, reviewedDate: null,
  deficiencies: null, correctiveActionsTaken: null,
  deficiencyItems: [],
};

const SEVERITY_OPTIONS = [
  { label: 'Minor', value: DeficiencySeverity.Minor },
  { label: 'Major', value: DeficiencySeverity.Major },
  { label: 'Critical', value: DeficiencySeverity.Critical },
];

// Official Aramco category list — canonical source:
// "Aramco Category & Equipment Types.xlsx" (Blue Sticker Services). value = the
// "Aramco Category Number" that prints on the Annex 1 sheet.
const ARAMCO_CATEGORY_OPTIONS = [
  { label: 'CR01 — Mobile Crane', value: 'CR01' },
  { label: 'CR02 — Elevator & Escalator', value: 'CR02' },
  { label: 'CR03 — Elevation Work Platform', value: 'CR03' },
  { label: 'CR04 — Marine & Offshore Cranes', value: 'CR04' },
  { label: 'CR05 — Storage Retrieval Machine', value: 'CR05' },
  { label: 'CR06 — Articulating Boom Crane', value: 'CR06' },
  { label: 'CR07 — Lifting / Spreader Beam', value: 'CR07' },
  { label: 'CR08 — Powered Platform / Sky Climber', value: 'CR08' },
  { label: 'CR09 — Vehicle Mounted Elevation & Rotating Aerial Device', value: 'CR09' },
  { label: 'CR10 — Manbasket', value: 'CR10' },
  { label: 'CR11 — Fixed Cranes & Hoists', value: 'CR11' },
  { label: 'CR12 — Side Boom Tractor', value: 'CR12' },
  { label: 'CR13 — A-frame & Mobile Gantry', value: 'CR13' },
  { label: 'CR14 — Tower Crane', value: 'CR14' },
];

// Canonical mapping from "Aramco Category & Equipment Types.xlsx"
const CATEGORY_EQUIPMENT_TYPES: Record<string, string[]> = {
  CR01: ['Mobile Crane - All Terrain', 'Mobile Crane - Rough Terrain', 'Mobile Crane - Truck Mounted Crane', 'Mobile Crane - Boom Truck', 'Crawler Crane'],
  CR02: ['Elevator', 'Escalator'],
  CR03: ['Manlift - Boom Supported EWP', 'Scissor Lift - Self Propelled EWP', 'Manually Propelled EWP', 'Mast Climbing Personal Platform'],
  CR04: ['Pedestal Crane', 'Pedestal Crane - Articulating Boom', 'Floating Crane - Articulating Boom', 'Floating Crane', 'Overhead Crane', 'Monorail Crane', 'Tower Crane', 'Portal Crane'],
  CR05: ['Storage Retrieval Machine (SRM)'],
  CR06: ['Articulating Boom Crane'],
  CR07: ['Lifting Beam', 'Spreader Beam'],
  CR08: ['Powered Platform / Sky Climber'],
  CR09: ['Bucket Truck'],
  CR10: ['Manbasket'],
  CR11: ['Overhead Crane', 'Monorail Crane', 'Jib Crane'],
  CR12: ['Side Boom Tractor'],
  CR13: ['A-frame', 'Gantry Crane'],
  CR14: ['Tower Crane'],
};

@Component({
  selector: 'tuv-aramco-form',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ButtonModule, InputTextModule, TextareaModule,
    DatePickerModule, SelectModule, CheckboxModule,
  ],
  template: `
    <div class="hint" *ngIf="!readonly">
      Fields below match the Aramco-approved Annex 1 (MS0053813) report. They appear on the
      generated Annex 1 PDF and on the public sticker QR view.
    </div>

    <div class="grid">
      <fieldset>
        <legend>Job order &amp; references</legend>
        <label>TUV Job Order No. <span class="auto">auto</span>
          <input pInputText [(ngModel)]="form.tuvJobOrderNo" [disabled]="true"
            placeholder="From linked job order" /></label>
        <label>Aramco Category No.
          <p-select [options]="aramcoCategoryOptions" [(ngModel)]="form.aramcoCategoryNo"
            optionLabel="label" optionValue="value" appendTo="body" [filter]="true"
            placeholder="Select Aramco category" [disabled]="readonly"
            (onChange)="onCategoryChange()" />
        </label>
        <label>Org. Code<input pInputText [(ngModel)]="form.orgCode" [disabled]="readonly" /></label>
        <label>RPO No.<input pInputText [(ngModel)]="form.rpoNo" [disabled]="readonly" /></label>
        <label>CRM No.<input pInputText [(ngModel)]="form.crmNo" [disabled]="readonly" /></label>
        <label>Report No. <span class="auto">auto</span>
          <input pInputText [(ngModel)]="form.reportNo" [disabled]="true"
            placeholder="IS-{empNo}-{year}-NNN" /></label>
      </fieldset>

      <fieldset>
        <legend>Site &amp; previous sticker</legend>
        <label>Department / Contractor<input pInputText [(ngModel)]="form.departmentContractor" [disabled]="readonly" /></label>
        <label>Inspection Time<input pInputText type="time" [(ngModel)]="form.inspectionTime" [disabled]="readonly" /></label>
        <label>Previous Sticker No.<input pInputText [(ngModel)]="form.previousStickerNo" [disabled]="readonly" /></label>
        <label>Previous Sticker Issued By
          <p-select [options]="issuedByOptions" [(ngModel)]="form.previousStickerIssuedBy"
            optionLabel="label" optionValue="value" appendTo="body"
            [disabled]="readonly" [filter]="true" [showClear]="true"
            placeholder="Select issuer" />
        </label>
        <label>Area of Inspection<input pInputText [(ngModel)]="form.areaOfInspection" [disabled]="readonly" /></label>
        <label>Equipment Location on Site<input pInputText [(ngModel)]="form.equipmentLocationOnSite" [disabled]="readonly" /></label>
      </fieldset>

      <fieldset>
        <legend>Equipment specifics</legend>
        <label>Capacity<input pInputText [(ngModel)]="form.capacity" placeholder="e.g. 5 t" [disabled]="readonly" /></label>
        <label>Manufacturer<input pInputText [(ngModel)]="form.manufacturer" [disabled]="readonly" /></label>
        <label>Model<input pInputText [(ngModel)]="form.model" [disabled]="readonly" /></label>
        <label>Equipment Type
          <p-select [options]="equipmentTypeOptions()" [(ngModel)]="form.equipmentType"
            appendTo="body" [filter]="true"
            placeholder="Select Aramco category first"
            [disabled]="readonly || !form.aramcoCategoryNo"
            (onChange)="emitSelection()" />
        </label>
        <label>Equipment Serial No.<input pInputText [(ngModel)]="form.equipmentSerialNo" [disabled]="readonly" /></label>
        <label>Sticker Expiration Date<input pInputText type="date" [(ngModel)]="form.stickerExpirationDate" [disabled]="readonly" /></label>
      </fieldset>

      <fieldset>
        <legend>Receiver &amp; review</legend>
        <label>Receiver Name<input pInputText [(ngModel)]="form.receiverName" [disabled]="readonly" /></label>
        <label>Receiver Badge No.<input pInputText [(ngModel)]="form.receiverBadgeNo" [disabled]="readonly" /></label>
        <label>Receiver Telephone<input pInputText [(ngModel)]="form.receiverTelephone" [disabled]="readonly" /></label>
        <label>Inspector Telephone<input pInputText [(ngModel)]="form.inspectorTelephone" [disabled]="readonly" /></label>
        <label>Received Date (auto)<input pInputText type="date" [(ngModel)]="form.receivedDate" [disabled]="true" /></label>
        <label>Reviewed Date<input pInputText type="date" [(ngModel)]="form.reviewedDate" [disabled]="readonly" /></label>
      </fieldset>
    </div>

    <fieldset class="full">
      <legend>Deficiencies &amp; corrective actions (structured)</legend>
      <table class="def-table" *ngIf="form.deficiencyItems.length > 0; else emptyDefs">
        <thead>
          <tr>
            <th>Deficiencies / Observations</th>
            <th>Corrective Action Taken</th>
            <th class="c-act" *ngIf="!readonly"></th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let row of form.deficiencyItems; let i = index">
            <td><input pInputText [(ngModel)]="row.description" [disabled]="readonly" placeholder="What was observed" /></td>
            <td><input pInputText [(ngModel)]="row.correctiveAction" [disabled]="readonly" placeholder="What was done" /></td>
            <td class="c-act" *ngIf="!readonly">
              <p-button icon="pi pi-trash" severity="danger" [text]="true" [rounded]="true"
                (onClick)="removeDeficiency(i)" />
            </td>
          </tr>
        </tbody>
      </table>
      <ng-template #emptyDefs>
        <p class="empty">No structured deficiencies recorded.</p>
      </ng-template>
      <div class="row-add" *ngIf="!readonly">
        <p-button label="Add deficiency row" icon="pi pi-plus" [text]="true" (onClick)="addDeficiency()" />
      </div>
    </fieldset>

    <div class="actions" *ngIf="!readonly">
      <p-button label="Save Annex 1 fields" icon="pi pi-save"
        [loading]="saving()" [disabled]="!dirty()" (onClick)="emit()" />
      <p-button *ngIf="canDownloadPdf" label="Download Annex 1 PDF" icon="pi pi-file-pdf"
        severity="danger" [text]="true" [disabled]="saving()"
        (onClick)="emitDownloadPdf()" />
    </div>
    <div class="actions" *ngIf="readonly && canDownloadPdf">
      <p-button label="Download Annex 1 PDF" icon="pi pi-file-pdf"
        severity="danger" [text]="true"
        (onClick)="emitDownloadPdf()" />
    </div>
  `,
  styles: [
    `
      :host { display: block; }
      .hint { color: #64748b; font-size: 0.82rem; margin-bottom: 0.75rem; }
      .grid {
        display: grid; gap: 1rem;
        grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
      }
      fieldset {
        border: 1px solid #e5e9f2; border-radius: 10px; padding: 0.75rem 1rem; margin: 0;
        display: flex; flex-direction: column; gap: 0.5rem;
      }
      fieldset.full { margin-top: 1rem; }
      legend { font-size: 0.78rem; font-weight: 600; color: #334155; padding: 0 0.4rem; }
      .auto {
        font-size: 0.62rem; font-weight: 600; text-transform: uppercase; letter-spacing: 0.04em;
        color: #0a64a4; background: #e0f2fe; padding: 0.05rem 0.35rem; border-radius: 999px;
      }
      :host ::ng-deep .p-select { width: 100%; }
      label {
        display: flex; flex-direction: column; gap: 0.25rem;
        font-size: 0.78rem; color: #475569;
      }
      input, textarea { width: 100%; }
      .two-col {
        display: grid; gap: 1rem;
        grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
      }
      .actions {
        margin-top: 1rem; display: flex; align-items: center; gap: 1rem; flex-wrap: wrap;
      }
      .pdf {
        color: #b91c1c; text-decoration: none; font-size: 0.85rem; display: inline-flex; gap: 0.4rem; align-items: center;
      }
      .pdf:hover { text-decoration: underline; }
      table.def-table { width: 100%; border-collapse: collapse; font-size: 0.82rem; }
      table.def-table th, table.def-table td {
        border-bottom: 1px solid #e5e9f2; padding: 0.4rem 0.5rem; text-align: left; vertical-align: middle;
      }
      table.def-table th { font-weight: 600; color: #334155; background: #f8fafc; }
      .c-code { width: 9rem; }
      .c-sev  { width: 8rem; }
      .c-due  { width: 9rem; }
      .c-done { width: 5.5rem; text-align: center; }
      .c-act  { width: 3rem; text-align: right; }
      .row-add { margin-top: 0.5rem; }
      .empty { color: #94a3b8; font-style: italic; font-size: 0.82rem; }
      details.legacy-defs { margin-top: 1rem; }
      details.legacy-defs summary {
        cursor: pointer; font-size: 0.78rem; color: #475569; padding: 0.25rem 0;
      }
    `,
  ],
})
export class AramcoFormComponent implements OnInit {
  @Input() set value(json: string | null | undefined) {
    if (!json) {
      this.form = { ...EMPTY, deficiencyItems: [] };
    } else {
      try {
        const parsed = JSON.parse(json) as Partial<AramcoFormDoc>;
        this.form = {
          ...EMPTY,
          ...parsed,
          deficiencyItems: Array.isArray(parsed.deficiencyItems) ? parsed.deficiencyItems : [],
        };
      } catch {
        this.form = { ...EMPTY, deficiencyItems: [] };
      }
    }
    // Received Date is system-set (the date the report is received) — never typed.
    if (!this.form.receivedDate) {
      this.form.receivedDate = new Date().toISOString().slice(0, 10);
    }
    this.original = JSON.stringify(this.form);
  }
  @Input() readonly = false;
  @Input() canDownloadPdf = false;
  @Input() aramcoPdfUrl = '';
  /** TÜV inspectors/users for the "Previous Sticker Issued By" dropdown (comment #5). */
  @Input() issuedByOptions: { label: string; value: string }[] = [];
  @Output() save = new EventEmitter<string>();
  @Output() downloadPdf = new EventEmitter<string>();
  /** Fires when the equipment selection that drives the SAIC checklist changes. */
  @Output() equipmentSelectionChange = new EventEmitter<{ category: string; equipmentType: string }>();

  protected form: AramcoFormDoc = { ...EMPTY, deficiencyItems: [] };
  protected saving = signal(false);
  protected severityOptions = SEVERITY_OPTIONS;
  protected aramcoCategoryOptions = ARAMCO_CATEGORY_OPTIONS;
  private original = JSON.stringify(EMPTY);

  protected dirty = () => JSON.stringify(this.form) !== this.original;

  protected equipmentTypeOptions = () =>
    this.form.aramcoCategoryNo
      ? (CATEGORY_EQUIPMENT_TYPES[this.form.aramcoCategoryNo] ?? [])
      : [];

  ngOnInit() {
    // Emit the saved selection on load (after the value setter has populated this.form
    // and the parent's output binding is established).
    this.emitSelection();
  }

  onCategoryChange() {
    if (
      this.form.equipmentType &&
      !this.equipmentTypeOptions().includes(this.form.equipmentType)
    ) {
      this.form.equipmentType = null;
    }
    this.emitSelection();
  }

  protected emitSelection() {
    const category = this.form.aramcoCategoryNo;
    const equipmentType = this.form.equipmentType;
    if (category && equipmentType) {
      this.equipmentSelectionChange.emit({ category, equipmentType });
    }
  }

  addDeficiency() {
    this.form.deficiencyItems = [
      ...this.form.deficiencyItems,
      {
        code: null,
        description: null,
        severity: DeficiencySeverity.Minor,
        correctiveAction: null,
        deadline: null,
        resolved: false,
      },
    ];
  }

  removeDeficiency(index: number) {
    this.form.deficiencyItems = this.form.deficiencyItems.filter((_, i) => i !== index);
  }

  private buildJson(): string {
    return JSON.stringify(this.form);
  }

  emit() {
    this.saving.set(true);
    const json = this.buildJson();
    this.original = json;
    this.save.emit(json);
    setTimeout(() => this.saving.set(false), 200);
  }

  emitDownloadPdf() {
    this.downloadPdf.emit(this.buildJson());
  }
}
