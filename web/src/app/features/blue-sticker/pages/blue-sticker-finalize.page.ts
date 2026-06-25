import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { BlueStickerApi } from '../../../core/api/blue-sticker.api';
import { BlueStickerReportDetail, BlueStickerState } from '../../../core/models/blue-sticker.models';
import { SignaturePad } from '../../certificates/components/signature-pad.component';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonModule, InputTextModule, SignaturePad],
  template: `
    <div class="finalize">
      @if (r(); as rep) {
        <h1>{{ rep.reportNo }}</h1>
        <p class="sub">{{ rep.equipmentIdNo }} · {{ rep.tuvJobOrderNo }}</p>

        @if (rep.state === S.Approved) {
          <p>Step 1 — send the verification code to the client's email.</p>
          <p-button label="Send OTP to client" icon="pi pi-envelope" size="large"
            [loading]="busy()" (onClick)="requestOtp()" />
        }

        @if (rep.state === S.AwaitingClientSignature) {
          <div class="otp">
            <label>Step 2 — enter the code the client received</label>
            <input pInputText inputmode="numeric" maxlength="6"
              [(ngModel)]="otp" placeholder="••••••" class="otp-input" />
            <p-button label="Resend" [text]="true" size="small"
              (onClick)="requestOtp()" [loading]="busy()" />
          </div>
          <div class="sign">
            <label>Step 3 — hand the tablet to the client to sign</label>
            <tuv-signature-pad (commitSignature)="onClientSign($event)" />
          </div>
        }

        @if (rep.state === S.ClientSigned) {
          <div class="done">
            <i class="pi pi-check-circle"></i>
            <h2>Signed & submitted</h2>
            <p-button label="Download Annex 1 PDF" icon="pi pi-file-pdf"
              size="large" (onClick)="download()" />
          </div>
        }
      } @else { <p>Loading…</p> }
    </div>

    <!-- ── DEV: side panel that surfaces the OTP without checking MailHog ── -->
    @if (devOtp(); as code) {
      <aside class="dev-otp">
        <div class="dev-otp-label">DEV OTP</div>
        <div class="dev-otp-code">{{ code }}</div>
        <p-button label="Copy" icon="pi pi-copy" size="small" [text]="true"
          (onClick)="copyOtp(code)" />
        <p-button label="Use" icon="pi pi-arrow-right" size="small"
          (onClick)="useOtp(code)" />
        <p-button icon="pi pi-times" size="small" [text]="true"
          (onClick)="devOtp.set(null)" />
      </aside>
    }
  `,
  styles: [`
    .finalize{max-width:760px;margin:0 auto;padding:1.5rem;display:flex;
      flex-direction:column;gap:1.1rem}
    h1{font-size:1.6rem;margin:0}
    .sub{color:#64748b;margin:0}
    .otp{display:flex;flex-direction:column;gap:.5rem}
    .otp-input{font-size:1.8rem;letter-spacing:.4rem;text-align:center;
      padding:.7rem;width:220px}
    .sign{display:flex;flex-direction:column;gap:.5rem}
    label{font-weight:600;color:#0f172a}
    .done{text-align:center;color:#16a34a}
    .done .pi{font-size:3rem}
    :host ::ng-deep p-button button{min-height:48px;font-size:1.05rem}
    .dev-otp{position:fixed;right:1rem;top:6rem;z-index:1000;
      background:#fef3c7;border:2px dashed #d97706;border-radius:10px;
      padding:.85rem 1rem;display:flex;flex-direction:column;gap:.4rem;
      align-items:flex-start;box-shadow:0 4px 16px rgba(0,0,0,.12);min-width:160px}
    .dev-otp-label{font-size:.7rem;color:#92400e;font-weight:700;letter-spacing:.1em}
    .dev-otp-code{font-size:1.9rem;font-weight:700;letter-spacing:.35rem;
      color:#7c2d12;font-family:ui-monospace,Menlo,monospace}
  `],
})
export class BlueStickerFinalizePage {
  private api = inject(BlueStickerApi);
  private route = inject(ActivatedRoute);
  private notify = inject(NotifyService);
  protected S = BlueStickerState;
  protected r = signal<BlueStickerReportDetail | null>(null);
  protected busy = signal(false);
  protected devOtp = signal<string | null>(null);
  protected otp = '';
  private id = this.route.snapshot.paramMap.get('id')!;

  constructor() { this.load(); }

  private load() {
    this.api.get(this.id).subscribe({
      next: (rep) => this.r.set(rep),
      error: (e) => showHttpError(this.notify, e),
    });
  }
  requestOtp() {
    this.busy.set(true);
    this.api.requestOtp(this.id).subscribe({
      next: (res) => { this.r.set(res.report); this.busy.set(false);
        if (res.devOtp) {
          this.devOtp.set(res.devOtp);
          this.notify.success(`OTP ready (dev): ${res.devOtp}`);
        } else {
          this.notify.success('OTP emailed to the client');
        }
      },
      error: (e) => { this.busy.set(false); showHttpError(this.notify, e); },
    });
  }
  copyOtp(code: string) {
    navigator.clipboard?.writeText(code);
    this.notify.success('Copied');
  }
  useOtp(code: string) { this.otp = code; }
  onClientSign(dataUrl: string) {
    if (this.busy()) return; // guard against a double signature commit while verify is in-flight
    if (!/^\d{6}$/.test(this.otp.trim())) {
      this.notify.error('Enter the 6-digit OTP before the client signs.');
      return;
    }
    this.busy.set(true);
    this.api.verifyAndSign(this.id, this.otp.trim(), dataUrl).subscribe({
      next: (rep) => { this.r.set(rep); this.busy.set(false);
        this.notify.success('Report signed & submitted'); },
      error: (e) => { this.busy.set(false); showHttpError(this.notify, e); },
    });
  }
  download() {
    this.api.pdf(this.id).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        window.open(url, '_blank');
        setTimeout(() => window.URL.revokeObjectURL(url), 60_000);
      },
      error: (e) => showHttpError(this.notify, e),
    });
  }
}
