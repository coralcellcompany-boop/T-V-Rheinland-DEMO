import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
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
  manufacturer: null, model: null, equipmentSerialNo: null, stickerExpirationDate: null,
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
        <label>Aramco Category No.<input pInputText [(ngModel)]="form.aramcoCategoryNo" [disabled]="readonly" /></label>
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
        <label>Inspection Time (HH:mm)<input pInputText [(ngModel)]="form.inspectionTime" placeholder="e.g. 09:30" [disabled]="readonly" /></label>
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
        <label>Equipment Serial No.<input pInputText [(ngModel)]="form.equipmentSerialNo" [disabled]="readonly" /></label>
        <label>Sticker Expiration Date<input pInputText type="date" [(ngModel)]="form.stickerExpirationDate" [disabled]="readonly" /></label>
      </fieldset>

      <fieldset>
        <legend>Receiver &amp; review</legend>
        <label>Receiver Name<input pInputText [(ngModel)]="form.receiverName" [disabled]="readonly" /></label>
        <label>Receiver Badge No.<input pInputText [(ngModel)]="form.receiverBadgeNo" [disabled]="readonly" /></label>
        <label>Receiver Telephone<input pInputText [(ngModel)]="form.receiverTelephone" [disabled]="readonly" /></label>
        <label>Inspector Telephone<input pInputText [(ngModel)]="form.inspectorTelephone" [disabled]="readonly" /></label>
        <label>Received Date<input pInputText type="date" [(ngModel)]="form.receivedDate" [disabled]="readonly" /></label>
        <label>Reviewed Date<input pInputText type="date" [(ngModel)]="form.reviewedDate" [disabled]="readonly" /></label>
      </fieldset>
    </div>

    <fieldset class="full">
      <legend>Deficiencies &amp; corrective actions (structured)</legend>
      <table class="def-table" *ngIf="form.deficiencyItems.length > 0; else emptyDefs">
        <thead>
          <tr>
            <th class="c-code">Code</th>
            <th>Description</th>
            <th class="c-sev">Severity</th>
            <th>Corrective Action</th>
            <th class="c-due">Deadline</th>
            <th class="c-done">Resolved</th>
            <th class="c-act" *ngIf="!readonly"></th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let row of form.deficiencyItems; let i = index">
            <td><input pInputText [(ngModel)]="row.code" [disabled]="readonly" placeholder="e.g. SAIC-7001-3" /></td>
            <td><input pInputText [(ngModel)]="row.description" [disabled]="readonly" placeholder="What was observed" /></td>
            <td>
              <p-select [options]="severityOptions" [(ngModel)]="row.severity"
                optionLabel="label" optionValue="value" appendTo="body" [disabled]="readonly" />
            </td>
            <td><input pInputText [(ngModel)]="row.correctiveAction" [disabled]="readonly" placeholder="What was done" /></td>
            <td><input pInputText type="date" [(ngModel)]="row.deadline" [disabled]="readonly" /></td>
            <td class="c-done"><p-checkbox [(ngModel)]="row.resolved" [binary]="true" [disabled]="readonly" /></td>
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

      <details class="legacy-defs">
        <summary>Free-text notes (legacy)</summary>
        <div class="two-col">
          <label>Deficiencies / Observations
            <textarea pTextarea rows="3" [(ngModel)]="form.deficiencies" [disabled]="readonly"
              placeholder="Optional free-text notes."></textarea>
          </label>
          <label>Corrective Action Taken
            <textarea pTextarea rows="3" [(ngModel)]="form.correctiveActionsTaken" [disabled]="readonly"
              placeholder="Optional free-text notes."></textarea>
          </label>
        </div>
      </details>
    </fieldset>

    <div class="actions" *ngIf="!readonly">
      <p-button label="Save Annex 1 fields" icon="pi pi-save"
        [loading]="saving()" [disabled]="!dirty()" (onClick)="emit()" />
      <a *ngIf="canDownloadPdf" class="pdf" [href]="aramcoPdfUrl" target="_blank" rel="noopener">
        <i class="pi pi-file-pdf"></i> Download Annex 1 PDF
      </a>
    </div>
    <div class="actions" *ngIf="readonly && canDownloadPdf">
      <a class="pdf" [href]="aramcoPdfUrl" target="_blank" rel="noopener">
        <i class="pi pi-file-pdf"></i> Download Annex 1 PDF
      </a>
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
export class AramcoFormComponent {
  @Input() set value(json: string | null | undefined) {
    if (!json) {
      this.form = { ...EMPTY, deficiencyItems: [] };
      this.original = JSON.stringify(this.form);
      return;
    }
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
    this.original = JSON.stringify(this.form);
  }
  @Input() readonly = false;
  @Input() canDownloadPdf = false;
  @Input() aramcoPdfUrl = '';
  /** TÜV inspectors/users for the "Previous Sticker Issued By" dropdown (comment #5). */
  @Input() issuedByOptions: { label: string; value: string }[] = [];
  @Output() save = new EventEmitter<string>();

  protected form: AramcoFormDoc = { ...EMPTY, deficiencyItems: [] };
  protected saving = signal(false);
  protected severityOptions = SEVERITY_OPTIONS;
  private original = JSON.stringify(EMPTY);

  protected dirty = () => JSON.stringify(this.form) !== this.original;

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

  emit() {
    this.saving.set(true);
    const json = JSON.stringify(this.form);
    this.original = json;
    this.save.emit(json);
    setTimeout(() => this.saving.set(false), 200);
  }
}
