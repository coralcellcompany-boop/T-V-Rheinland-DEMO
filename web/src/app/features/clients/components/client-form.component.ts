import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { SelectModule } from 'primeng/select';
import { CheckboxModule } from 'primeng/checkbox';
import { ClientDetail, ContractStatus } from '../../../core/models/client.models';

const SERVICE_OPTIONS = [
  { label: 'Third Party Inspection',  bit: 1 },
  { label: 'Blue Sticker',            bit: 2 },
  { label: 'Operator Assessment',     bit: 4 },
];

@Component({
  selector: 'tuv-client-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    InputTextModule,
    TextareaModule,
    ButtonModule,
    SelectModule,
    CheckboxModule,
  ],
  template: `
    <form [formGroup]="form" (ngSubmit)="onSave()" class="form">
      <div class="row">
        <label>Client name<span class="req">*</span></label>
        <input pInputText formControlName="name" placeholder="e.g. Asconcom Contracting Co." />
      </div>

      <div class="row two">
        <div>
          <label>Code<span class="req">*</span></label>
          <input pInputText formControlName="code"
            [readonly]="!!editing"
            placeholder="ASCO" style="text-transform: uppercase" />
          <small *ngIf="!editing">Letters, digits, dot, dash, underscore. Cannot be changed later.</small>
        </div>
        <div>
          <label>Contract status</label>
          <p-select [options]="statusOptions" formControlName="contractStatus" appendTo="body"
            optionLabel="label" optionValue="value" />
        </div>
      </div>

      <div class="row">
        <label>Address</label>
        <textarea pTextarea rows="2" formControlName="address" placeholder="Optional"></textarea>
      </div>

      <fieldset class="contact">
        <legend>Primary contact</legend>
        <div class="row two">
          <div>
            <label>Name</label>
            <input pInputText formControlName="contactName" />
          </div>
          <div>
            <label>Phone</label>
            <input pInputText formControlName="contactPhone" />
          </div>
        </div>
        <div class="row">
          <label>Email</label>
          <input pInputText formControlName="contactEmail" type="email" />
        </div>
      </fieldset>

      <fieldset class="services">
        <legend>Allowed services</legend>
        <ng-container *ngFor="let s of serviceOptions">
          <label class="check">
            <p-checkbox [binary]="true" [ngModel]="hasService(s.bit)"
                       (ngModelChange)="toggleService(s.bit, $event)"
                       [ngModelOptions]="{ standalone: true }" />
            <span>{{ s.label }}</span>
          </label>
        </ng-container>
      </fieldset>

      <div class="actions">
        <p-button type="button" severity="secondary" label="Cancel" (onClick)="cancel.emit()" />
        <p-button type="submit" label="{{ editing ? 'Save changes' : 'Create client' }}"
                  [loading]="saving()" [disabled]="form.invalid || saving()" />
      </div>
    </form>
  `,
  styles: [
    `
      :host { display: block; max-width: 640px; }
      .form { display: flex; flex-direction: column; gap: 1rem; }
      .row { display: flex; flex-direction: column; gap: 0.4rem; }
      .row.two { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
      label { font-size: 0.85rem; font-weight: 500; color: #334155; }
      .req { color: #dc2626; margin-left: 0.15rem; }
      input, textarea { width: 100%; }
      small { color: #94a3b8; font-size: 0.75rem; }
      fieldset {
        border: 1px solid #e5e9f2; border-radius: 12px; padding: 0.75rem 1rem;
        display: flex; flex-direction: column; gap: 0.65rem;
      }
      fieldset legend { padding: 0 0.4rem; font-size: 0.78rem; font-weight: 600; color: #475569; }
      .check { display: flex; align-items: center; gap: 0.55rem; cursor: pointer; }
      .actions { display: flex; justify-content: flex-end; gap: 0.6rem; margin-top: 0.5rem; }
    `,
  ],
})
export class ClientForm {
  @Input() editing: ClientDetail | null = null;
  @Output() save = new EventEmitter<{ name: string; code: string; address: string | null; contactName: string | null; contactPhone: string | null; contactEmail: string | null; contractStatus: number; allowedServices: number }>();
  @Output() cancel = new EventEmitter<void>();

  protected saving = signal(false);

  protected statusOptions = [
    { label: 'Active', value: ContractStatus.Active },
    { label: 'Suspended', value: ContractStatus.Suspended },
    { label: 'Terminated', value: ContractStatus.Terminated },
  ];

  protected serviceOptions = SERVICE_OPTIONS;

  private fb = inject(FormBuilder);
  protected form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    code: ['', [Validators.required, Validators.pattern(/^[A-Za-z0-9._-]+$/)]],
    address: [null as string | null],
    contactName: [null as string | null],
    contactPhone: [null as string | null],
    contactEmail: [null as string | null, [Validators.email]],
    contractStatus: [ContractStatus.Active as number],
    allowedServices: [7], // default = All
  });

  ngOnChanges() {
    if (this.editing) {
      this.form.patchValue({
        name: this.editing.name,
        code: this.editing.code,
        address: this.editing.address,
        contactName: this.editing.contactName,
        contactPhone: this.editing.contactPhone,
        contactEmail: this.editing.contactEmail,
        contractStatus: this.editing.contractStatus,
        allowedServices: this.editing.allowedServices,
      });
    }
  }

  hasService(bit: number): boolean {
    return (this.form.controls.allowedServices.value & bit) !== 0;
  }

  toggleService(bit: number, on: boolean): void {
    const current = this.form.controls.allowedServices.value;
    this.form.controls.allowedServices.setValue(on ? current | bit : current & ~bit);
  }

  onSave() {
    if (this.form.invalid) return;
    this.saving.set(true);
    const v = this.form.getRawValue();
    this.save.emit({
      ...v,
      code: v.code.toUpperCase(),
    });
    // parent decides when to clear `saving` via re-render
    setTimeout(() => this.saving.set(false), 800);
  }
}
