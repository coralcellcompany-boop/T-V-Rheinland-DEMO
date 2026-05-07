import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { DatePickerModule } from 'primeng/datepicker';

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
};

@Component({
  selector: 'tuv-aramco-form',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonModule, InputTextModule, TextareaModule, DatePickerModule],
  template: `
    <div class="hint" *ngIf="!readonly">
      Fields below match the Aramco-approved Annex 1 (MS0053813) report. They appear on the
      generated Annex 1 PDF and on the public sticker QR view.
    </div>

    <div class="grid">
      <fieldset>
        <legend>Job order &amp; references</legend>
        <label>TUV Job Order No.<input pInputText [(ngModel)]="form.tuvJobOrderNo" [disabled]="readonly" /></label>
        <label>Aramco Category No.<input pInputText [(ngModel)]="form.aramcoCategoryNo" [disabled]="readonly" /></label>
        <label>Org. Code<input pInputText [(ngModel)]="form.orgCode" [disabled]="readonly" /></label>
        <label>RPO No.<input pInputText [(ngModel)]="form.rpoNo" [disabled]="readonly" /></label>
        <label>CRM No.<input pInputText [(ngModel)]="form.crmNo" [disabled]="readonly" /></label>
        <label>Report No.<input pInputText [(ngModel)]="form.reportNo" [disabled]="readonly" /></label>
      </fieldset>

      <fieldset>
        <legend>Site &amp; previous sticker</legend>
        <label>Department / Contractor<input pInputText [(ngModel)]="form.departmentContractor" [disabled]="readonly" /></label>
        <label>Inspection Time (HH:mm)<input pInputText [(ngModel)]="form.inspectionTime" placeholder="e.g. 09:30" [disabled]="readonly" /></label>
        <label>Previous Sticker No.<input pInputText [(ngModel)]="form.previousStickerNo" [disabled]="readonly" /></label>
        <label>Previous Sticker Issued By<input pInputText [(ngModel)]="form.previousStickerIssuedBy" [disabled]="readonly" /></label>
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
      <legend>Deficiencies &amp; corrective actions</legend>
      <div class="two-col">
        <label>Deficiencies / Observations
          <textarea pTextarea rows="4" [(ngModel)]="form.deficiencies" [disabled]="readonly"
            placeholder="List defects observed during inspection."></textarea>
        </label>
        <label>Corrective Action Taken
          <textarea pTextarea rows="4" [(ngModel)]="form.correctiveActionsTaken" [disabled]="readonly"
            placeholder="Repairs done to address each deficiency."></textarea>
        </label>
      </div>
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
    `,
  ],
})
export class AramcoFormComponent {
  @Input() set value(json: string | null | undefined) {
    if (!json) {
      this.form = { ...EMPTY };
      this.original = JSON.stringify(this.form);
      return;
    }
    try {
      const parsed = JSON.parse(json) as Partial<AramcoFormDoc>;
      this.form = { ...EMPTY, ...parsed };
    } catch {
      this.form = { ...EMPTY };
    }
    this.original = JSON.stringify(this.form);
  }
  @Input() readonly = false;
  @Input() canDownloadPdf = false;
  @Input() aramcoPdfUrl = '';
  @Output() save = new EventEmitter<string>();

  protected form: AramcoFormDoc = { ...EMPTY };
  protected saving = signal(false);
  private original = JSON.stringify(EMPTY);

  protected dirty = () => JSON.stringify(this.form) !== this.original;

  emit() {
    this.saving.set(true);
    const json = JSON.stringify(this.form);
    this.original = json;
    this.save.emit(json);
    setTimeout(() => this.saving.set(false), 200);
  }
}
