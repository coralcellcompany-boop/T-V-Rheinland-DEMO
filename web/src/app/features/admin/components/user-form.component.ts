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
import { SignaturePad } from '../../certificates/components/signature-pad.component';

@Component({
  selector: 'tuv-user-form',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ReactiveFormsModule,
    InputTextModule, MultiSelectModule, CheckboxModule, PasswordModule, ButtonModule,
    SignaturePad,
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

      <!-- ── Signature (auto-applied when this user signs Blue Sticker reports) ── -->
      <div class="row sig-row">
        <label>Signature
          @if (editing?.hasSignature && !pendingSignature()) {
            <span class="badge ok">on file</span>
          } @else if (pendingSignature()) {
            <span class="badge ok">captured</span>
          } @else {
            <span class="badge warn">not set</span>
          }
        </label>
        <small>Captured once; applied automatically whenever this user signs a Blue Sticker
          report. Sign with a finger / stylus.</small>
        <tuv-signature-pad (commitSignature)="onSignatureCaptured($event)" />
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
      .sig-row label { display: flex; align-items: center; gap: 0.4rem; }
      .badge { font-size: 0.7rem; padding: 0.05rem 0.45rem; border-radius: 999px; font-weight: 600;
        text-transform: uppercase; letter-spacing: 0.05em; }
      .badge.ok { background: #dcfce7; color: #047857; }
      .badge.warn { background: #fef3c7; color: #92400e; }
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
  protected pendingSignature = signal<string | null>(null);

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

  onSignatureCaptured(dataUrl: string) { this.pendingSignature.set(dataUrl); }

  onSave() {
    if (this.form.invalid) return;
    this.saving.set(true);
    const sig = this.pendingSignature();
    // For new users we require a signature — they need it to sign reports.
    if (!this.editing && !sig) {
      this.saving.set(false);
      alert('Please capture the user\'s signature before creating their account.');
      return;
    }
    this.save.emit({
      ...this.form.getRawValue(),
      // null means "leave existing signature untouched" on the backend.
      signaturePng: sig ?? null,
    });
    setTimeout(() => this.saving.set(false), 800);
  }
}
