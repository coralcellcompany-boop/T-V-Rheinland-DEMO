import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { CheckboxModule } from 'primeng/checkbox';
import { PasswordModule } from 'primeng/password';
import { ClientListItem } from '../../../core/models/client.models';
import { UserListItem } from '../../../core/models/user.models';

@Component({
  selector: 'tuv-user-form',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ReactiveFormsModule,
    InputTextModule, MultiSelectModule, CheckboxModule, PasswordModule, ButtonModule,
  ],
  template: `
    <form [formGroup]="form" (ngSubmit)="onSave()" class="form">
      <div class="row">
        <label>Email<span class="req">*</span></label>
        <input pInputText formControlName="email" type="email" [readonly]="!!editing"
          placeholder="user@tuv-arabia.local" />
      </div>

      <div class="row two">
        <div>
          <label>Full name<span class="req">*</span></label>
          <input pInputText formControlName="fullName" />
        </div>
        <div>
          <label>SAP no.</label>
          <input pInputText formControlName="sapNo" />
        </div>
      </div>

      <div class="row two">
        <div>
          <label>Certification no.</label>
          <input pInputText formControlName="certNo" />
        </div>
        <div *ngIf="editing">
          <label>&nbsp;</label>
          <label class="check">
            <p-checkbox [binary]="true" formControlName="isActive" />
            <span>Active</span>
          </label>
        </div>
      </div>

      <div class="row" *ngIf="!editing">
        <label>Initial password<span class="req">*</span></label>
        <p-password formControlName="password" [feedback]="true" [toggleMask]="true"
          inputStyleClass="pw-input" styleClass="pw" />
        <small>Min 12 characters with upper, lower, digit, and special.</small>
      </div>

      <div class="row">
        <label>Roles<span class="req">*</span></label>
        <p-multiSelect [options]="roleOptions" formControlName="roles"
          appendTo="body" [showHeader]="false" placeholder="Select roles" display="chip" />
      </div>

      <div class="row">
        <label>Assigned clients</label>
        <p-multiSelect [options]="clientOptions" optionLabel="name" optionValue="id"
          formControlName="assignedClientIds" appendTo="body" placeholder="All clients (manager bypass)"
          display="chip" [filter]="true" filterBy="name,code" />
        <small>Empty = none. Managers bypass client scoping regardless.</small>
      </div>

      <div class="actions">
        <p-button type="button" severity="secondary" label="Cancel" (onClick)="cancel.emit()" />
        <p-button type="submit" label="{{ editing ? 'Save changes' : 'Create user' }}"
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
      :host ::ng-deep .p-multiselect, :host ::ng-deep .p-password { width: 100%; }
      :host ::ng-deep .pw-input { width: 100%; }
      .check { display: flex; align-items: center; gap: 0.55rem; cursor: pointer; }
      small { color: #94a3b8; font-size: 0.75rem; }
      .actions { display: flex; justify-content: flex-end; gap: 0.6rem; margin-top: 0.5rem; }
    `,
  ],
})
export class UserForm {
  @Input() editing: UserListItem | null = null;
  @Input({ required: true }) roleOptions: string[] = [];
  @Input({ required: true }) clientOptions: ClientListItem[] = [];

  @Output() save = new EventEmitter<any>();
  @Output() cancel = new EventEmitter<void>();

  protected saving = signal(false);

  private fb = inject(FormBuilder);
  protected form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    fullName: ['', [Validators.required]],
    sapNo: [null as string | null],
    certNo: [null as string | null],
    password: ['', [Validators.minLength(12)]],
    roles: [[] as string[], [Validators.required]],
    assignedClientIds: [[] as string[]],
    isActive: [true],
  });

  ngOnChanges() {
    if (this.editing) {
      this.form.patchValue({
        email: this.editing.email ?? this.editing.userName,
        fullName: this.editing.fullName ?? '',
        sapNo: this.editing.sapNo,
        certNo: this.editing.certNo,
        roles: this.editing.roles,
        assignedClientIds: this.editing.assignedClientIds,
        isActive: this.editing.isActive,
      });
      this.form.controls.password.clearValidators();
      this.form.controls.password.updateValueAndValidity();
    }
  }

  onSave() {
    if (this.form.invalid) return;
    this.saving.set(true);
    this.save.emit(this.form.getRawValue());
    setTimeout(() => this.saving.set(false), 800);
  }
}
