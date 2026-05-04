import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { SelectModule } from 'primeng/select';
import { InputNumberModule } from 'primeng/inputnumber';
import {
  AramcoCategoryName,
  EquipmentDetail,
  EquipmentStatus,
  EquipmentStatusLabel,
  EquipmentType,
} from '../../../core/models/equipment.models';
import { ClientListItem } from '../../../core/models/client.models';
import { PhotoUploader } from '../../../shared/components/photo-uploader.component';

@Component({
  selector: 'tuv-equipment-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    InputTextModule,
    TextareaModule,
    InputNumberModule,
    SelectModule,
    ButtonModule,
    PhotoUploader,
  ],
  template: `
    <form [formGroup]="form" (ngSubmit)="onSave()" class="form">
      <div class="row two">
        <div>
          <label>Client<span class="req">*</span></label>
          <p-select
            [options]="clients"
            optionLabel="name" optionValue="id"
            formControlName="clientId" appendTo="body"
            [filter]="true" filterBy="name,code"
            placeholder="Select client" [disabled]="!!editing" />
        </div>
        <div>
          <label>Equipment type<span class="req">*</span></label>
          <p-select
            [options]="types"
            optionLabel="name" optionValue="id"
            formControlName="equipmentTypeId" appendTo="body"
            [filter]="true" filterBy="name"
            placeholder="Select type" />
        </div>
      </div>

      <div class="row two">
        <div>
          <label>Equipment ID No.<span class="req">*</span></label>
          <input pInputText formControlName="idNo" placeholder="e.g. CT-BL-12" />
        </div>
        <div>
          <label>Serial No.</label>
          <input pInputText formControlName="serialNo" placeholder="Manufacturer serial" />
        </div>
      </div>

      <div class="row two">
        <div>
          <label>Aramco category</label>
          <p-select
            [options]="categoryOptions"
            optionLabel="label" optionValue="value"
            formControlName="aramcoCategory" appendTo="body"
            [showClear]="true" placeholder="Not Aramco-tracked" />
        </div>
        <div *ngIf="editing">
          <label>Status</label>
          <p-select
            [options]="statusOptions"
            optionLabel="label" optionValue="value"
            formControlName="status" appendTo="body" />
        </div>
      </div>

      <div class="row two">
        <div>
          <label>Manufacturer</label>
          <input pInputText formControlName="manufacturer" />
        </div>
        <div>
          <label>Model</label>
          <input pInputText formControlName="model" />
        </div>
      </div>

      <div class="row two">
        <div>
          <label>Year of manufacture</label>
          <p-inputNumber formControlName="yearOfManufacture" [showButtons]="false"
            [useGrouping]="false" [min]="1900" [max]="thisYear + 1" placeholder="2024" />
        </div>
        <div>
          <label>Safe Working Load (SWL)</label>
          <input pInputText formControlName="swl" placeholder="e.g. 25 t" />
        </div>
      </div>

      <div class="row">
        <label>Location</label>
        <textarea pTextarea rows="2" formControlName="location" placeholder="Site / area"></textarea>
      </div>

      <div class="row">
        <label>Reference photo</label>
        <tuv-photo-uploader
          [photoKey]="form.controls.photoKey.value"
          (photoKeyChange)="form.controls.photoKey.setValue($event)" />
      </div>

      <div class="actions">
        <p-button type="button" severity="secondary" label="Cancel" (onClick)="cancel.emit()" />
        <p-button type="submit" label="{{ editing ? 'Save changes' : 'Create equipment' }}"
          [loading]="saving()" [disabled]="form.invalid || saving()" />
      </div>
    </form>
  `,
  styles: [
    `
      :host { display: block; max-width: 720px; }
      .form { display: flex; flex-direction: column; gap: 1rem; }
      .row { display: flex; flex-direction: column; gap: 0.4rem; }
      .row.two { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
      label { font-size: 0.85rem; font-weight: 500; color: #334155; }
      .req { color: #dc2626; margin-left: 0.15rem; }
      input, textarea, p-select, p-inputNumber { width: 100%; }
      .actions { display: flex; justify-content: flex-end; gap: 0.6rem; margin-top: 0.5rem; }
    `,
  ],
})
export class EquipmentForm {
  @Input() editing: EquipmentDetail | null = null;
  @Input({ required: true }) clients: ClientListItem[] = [];
  @Input({ required: true }) types: EquipmentType[] = [];
  @Output() save = new EventEmitter<any>();
  @Output() cancel = new EventEmitter<void>();

  protected saving = signal(false);
  protected thisYear = new Date().getFullYear();

  protected categoryOptions = Object.entries(AramcoCategoryName).map(([value, label]) => ({
    value: Number(value),
    label,
  }));

  protected statusOptions = [
    { label: EquipmentStatusLabel[EquipmentStatus.Active], value: EquipmentStatus.Active },
    { label: EquipmentStatusLabel[EquipmentStatus.Decommissioned], value: EquipmentStatus.Decommissioned },
    { label: EquipmentStatusLabel[EquipmentStatus.Sold], value: EquipmentStatus.Sold },
  ];

  private fb = inject(FormBuilder);
  protected form = this.fb.nonNullable.group({
    clientId: ['', [Validators.required]],
    equipmentTypeId: ['', [Validators.required]],
    aramcoCategory: [null as number | null],
    idNo: ['', [Validators.required, Validators.maxLength(100)]],
    serialNo: [null as string | null],
    manufacturer: [null as string | null],
    model: [null as string | null],
    yearOfManufacture: [null as number | null],
    swl: [null as string | null],
    location: [null as string | null],
    status: [EquipmentStatus.Active as number],
    photoKey: [null as string | null],
  });

  ngOnChanges() {
    if (this.editing) {
      this.form.patchValue({
        clientId: this.editing.clientId,
        equipmentTypeId: this.editing.equipmentTypeId,
        aramcoCategory: this.editing.aramcoCategory,
        idNo: this.editing.idNo,
        serialNo: this.editing.serialNo,
        manufacturer: this.editing.manufacturer,
        model: this.editing.model,
        yearOfManufacture: this.editing.yearOfManufacture,
        swl: this.editing.swl,
        location: this.editing.location,
        status: this.editing.status,
        photoKey: this.editing.photoKey,
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
