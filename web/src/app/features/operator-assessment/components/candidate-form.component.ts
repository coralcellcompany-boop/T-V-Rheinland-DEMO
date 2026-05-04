import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { CheckboxModule } from 'primeng/checkbox';
import { CandidateDetail } from '../../../core/models/assessment.models';
import { ClientListItem } from '../../../core/models/client.models';

@Component({
  selector: 'tuv-candidate-form',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ReactiveFormsModule,
    InputTextModule, SelectModule, CheckboxModule, ButtonModule,
  ],
  template: `
    <form [formGroup]="form" (ngSubmit)="onSave()" class="form">
      <div class="row">
        <label>Client<span class="req">*</span></label>
        <p-select [options]="clients" optionLabel="name" optionValue="id"
          formControlName="clientId" appendTo="body" [filter]="true" filterBy="name,code"
          placeholder="Select client" [disabled]="!!editing" />
      </div>

      <div class="row two">
        <div>
          <label>Full name<span class="req">*</span></label>
          <input pInputText formControlName="fullName" placeholder="As shown on ID" />
        </div>
        <div>
          <label>Identification (ID/Iqama)<span class="req">*</span></label>
          <input pInputText formControlName="identificationNumber" />
        </div>
      </div>

      <div class="row two">
        <div>
          <label>Phone</label>
          <input pInputText formControlName="phone" />
        </div>
        <div>
          <label>Email</label>
          <input pInputText formControlName="email" type="email" />
        </div>
      </div>

      <div class="row two">
        <div>
          <label>Employee No.</label>
          <input pInputText formControlName="employeeNo" />
        </div>
        <div>
          <label>Nationality</label>
          <input pInputText formControlName="nationality" />
        </div>
      </div>

      <div class="row two">
        <div>
          <label>Date of birth</label>
          <input pInputText type="date" formControlName="dateOfBirth" />
        </div>
        <div *ngIf="editing">
          <label>&nbsp;</label>
          <label class="check">
            <p-checkbox [binary]="true" formControlName="isActive" />
            <span>Active</span>
          </label>
        </div>
      </div>

      <div class="actions">
        <p-button type="button" severity="secondary" label="Cancel" (onClick)="cancel.emit()" />
        <p-button type="submit" label="{{ editing ? 'Save changes' : 'Create candidate' }}"
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
      input { width: 100%; }
      :host ::ng-deep .p-select { width: 100%; }
      .check { display: flex; align-items: center; gap: 0.55rem; cursor: pointer; }
      .actions { display: flex; justify-content: flex-end; gap: 0.6rem; margin-top: 0.5rem; }
    `,
  ],
})
export class CandidateForm {
  @Input() editing: CandidateDetail | null = null;
  @Input({ required: true }) clients: ClientListItem[] = [];
  @Output() save = new EventEmitter<any>();
  @Output() cancel = new EventEmitter<void>();

  protected saving = signal(false);
  private fb = inject(FormBuilder);
  protected form = this.fb.nonNullable.group({
    clientId: ['', [Validators.required]],
    fullName: ['', [Validators.required, Validators.maxLength(200)]],
    identificationNumber: ['', [Validators.required, Validators.maxLength(50)]],
    phone: [null as string | null],
    email: [null as string | null, [Validators.email]],
    employeeNo: [null as string | null],
    nationality: [null as string | null],
    dateOfBirth: [null as string | null],
    isActive: [true],
  });

  ngOnChanges() {
    if (this.editing) {
      this.form.patchValue({
        clientId: this.editing.clientId,
        fullName: this.editing.fullName,
        identificationNumber: this.editing.identificationNumber,
        phone: this.editing.phone,
        email: this.editing.email,
        employeeNo: this.editing.employeeNo,
        nationality: this.editing.nationality,
        dateOfBirth: this.editing.dateOfBirth,
        isActive: this.editing.isActive,
      });
    }
  }

  onSave() {
    if (this.form.invalid) return;
    this.saving.set(true);
    this.save.emit(this.form.getRawValue());
    setTimeout(() => this.saving.set(false), 800);
  }
}
