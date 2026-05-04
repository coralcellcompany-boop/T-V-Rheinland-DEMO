import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { MessageModule } from 'primeng/message';
import { CardModule } from 'primeng/card';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'tuv-login',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    ButtonModule,
    InputTextModule,
    PasswordModule,
    MessageModule,
    CardModule,
  ],
  template: `
    <div class="login-shell">
      <p-card header="TÜV Rheinland Arabia" subheader="Inspection Management System">
        <form [formGroup]="form" (ngSubmit)="submit()" class="form">
          <label for="username">Email or username</label>
          <input
            pInputText
            id="username"
            formControlName="userName"
            autocomplete="username"
            placeholder="admin@tuv-arabia.local"
          />

          <label for="password">Password</label>
          <p-password
            inputId="password"
            formControlName="password"
            [feedback]="false"
            [toggleMask]="true"
            autocomplete="current-password"
            styleClass="pw"
            inputStyleClass="pw-input"
          />

          <p-message *ngIf="error()" severity="error" [text]="error()!" />

          <p-button
            type="submit"
            label="Sign in"
            [loading]="loading()"
            [disabled]="form.invalid || loading()"
            severity="primary"
            styleClass="submit"
          />
        </form>
      </p-card>
    </div>
  `,
  styles: [
    `
      :host { display: block; }
      .login-shell {
        min-height: 100vh; display: grid; place-items: center;
        background: linear-gradient(135deg, #0a3d62, #1e6091);
      }
      :host ::ng-deep .p-card { width: 380px; }
      .form { display: flex; flex-direction: column; gap: 0.5rem; }
      .form label { font-size: 0.85rem; font-weight: 500; margin-top: 0.5rem; }
      .form input, .form .pw, .form .pw-input { width: 100%; }
      :host ::ng-deep .submit { margin-top: 0.75rem; }
      :host ::ng-deep .p-password, :host ::ng-deep .p-password input { width: 100%; }
    `,
  ],
})
export class LoginPage {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);

  protected loading = signal(false);
  protected error = signal<string | null>(null);

  protected form = this.fb.nonNullable.group({
    userName: ['', [Validators.required]],
    password: ['', [Validators.required]],
  });

  submit() {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.error.set(null);

    this.auth.login(this.form.getRawValue()).subscribe({
      next: () => {
        this.loading.set(false);
        // Client-only users land on their focused portal; staff see the full dashboard.
        const staffRoles = ['Manager', 'Coordinator', 'Inspector', 'TechReviewer'];
        const isStaff = this.auth.roles().some((r) => staffRoles.includes(r));
        this.router.navigate([isStaff ? '/dashboard' : '/my-certificates']);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.error?.detail ?? 'Sign-in failed.');
      },
    });
  }
}
