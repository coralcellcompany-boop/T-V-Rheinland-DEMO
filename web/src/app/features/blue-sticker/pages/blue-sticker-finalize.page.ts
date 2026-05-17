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
  `],
})
export class BlueStickerFinalizePage {
  private api = inject(BlueStickerApi);
  private route = inject(ActivatedRoute);
  private notify = inject(NotifyService);
  protected S = BlueStickerState;
  protected r = signal<BlueStickerReportDetail | null>(null);
  protected busy = signal(false);
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
      next: (rep) => { this.r.set(rep); this.busy.set(false);
        this.notify.success('OTP emailed to the client'); },
      error: (e) => { this.busy.set(false); showHttpError(this.notify, e); },
    });
  }
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
