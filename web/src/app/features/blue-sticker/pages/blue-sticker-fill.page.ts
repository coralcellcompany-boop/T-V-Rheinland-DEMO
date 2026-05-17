import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { BlueStickerApi } from '../../../core/api/blue-sticker.api';
import {
  BlueStickerReportDetail, BlueStickerResult, BlueStickerState,
} from '../../../core/models/blue-sticker.models';
import { SignaturePad } from '../../certificates/components/signature-pad.component';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonModule, InputTextModule, SelectModule, SignaturePad],
  template: `
    @if (r(); as rep) {
      <h2>{{ rep.reportNo }} — {{ rep.equipmentIdNo }}</h2>
      <p>State: <strong>{{ rep.state }}</strong></p>

      @if (rep.state === S.Draft) {
        <p-button label="Start inspection" icon="pi pi-play"
          (onClick)="fire('StartInspection')" [loading]="busy()" />
      }

      @if (rep.state === S.InProgress) {
        <div class="form">
          <label>Area of inspection</label>
          <input pInputText [(ngModel)]="form.areaOfInspection" />
          <label>Result</label>
          <p-select [options]="resultOptions" optionLabel="label" optionValue="value"
            [(ngModel)]="form.result" appendTo="body" />
          <label>Deficiencies / observations</label>
          <input pInputText [(ngModel)]="form.deficiencies" />
          <label>Corrective action taken</label>
          <input pInputText [(ngModel)]="form.correctiveActionsTaken" />
          <label>Equipment location</label>
          <input pInputText [(ngModel)]="form.equipmentLocation" />
          <label>Receiver name</label>
          <input pInputText [(ngModel)]="form.receiverName" />
          <label>Receiver badge No.</label>
          <input pInputText [(ngModel)]="form.receiverBadgeNo" />
          <label>Receiver telephone</label>
          <input pInputText [(ngModel)]="form.receiverTelephone" />
          <label>Inspector telephone</label>
          <input pInputText [(ngModel)]="form.inspectorTelephone" />
          <p-button label="Save" icon="pi pi-save" (onClick)="save()" [loading]="busy()" />

          <h3>Inspector signature (sign before submitting)</h3>
          <tuv-signature-pad (commitSignature)="inspectorSig.set($event)" />
          <p-button label="Submit to technical reviewer" icon="pi pi-send"
            [disabled]="!inspectorSig()" [loading]="busy()"
            (onClick)="submit()" />
        </div>
      }

      @if (rep.state === S.UnderReview) {
        <p>Submitted — awaiting technical reviewer.</p>
      }
      @if (rep.state === S.Approved || rep.state === S.AwaitingClientSignature) {
        <p-button label="Go to client signing" icon="pi pi-pencil"
          (onClick)="goFinalize()" />
      }
      @if (rep.state === S.ClientSigned) {
        <p-button label="Download Annex 1 PDF" icon="pi pi-file-pdf"
          (onClick)="download()" />
      }
    } @else { <p>Loading…</p> }
  `,
  styles: [`.form{display:flex;flex-direction:column;gap:.5rem;max-width:560px}
    label{font-size:.85rem;color:#334155;margin-top:.3rem}`],
})
export class BlueStickerFillPage {
  private api = inject(BlueStickerApi);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private notify = inject(NotifyService);

  protected S = BlueStickerState;
  protected r = signal<BlueStickerReportDetail | null>(null);
  protected busy = signal(false);
  protected inspectorSig = signal<string | null>(null);
  private id = this.route.snapshot.paramMap.get('id')!;

  protected resultOptions = [
    { label: 'Pass', value: BlueStickerResult.Pass },
    { label: 'Fail', value: BlueStickerResult.Fail },
  ];
  protected form: any = { result: BlueStickerResult.Pass };

  constructor() { this.load(); }

  private load() {
    this.api.get(this.id).subscribe({
      next: (rep) => {
        this.r.set(rep);
        this.form = {
          areaOfInspection: rep.areaOfInspection ?? '',
          result: rep.result || BlueStickerResult.Pass,
          deficiencies: rep.deficiencies ?? '',
          correctiveActionsTaken: rep.correctiveActionsTaken ?? '',
          equipmentLocation: rep.equipmentLocation ?? '',
          receiverName: rep.receiverName ?? '',
          receiverBadgeNo: rep.receiverBadgeNo ?? '',
          receiverTelephone: rep.receiverTelephone ?? '',
          inspectorTelephone: rep.inspectorTelephone ?? '',
        };
      },
      error: (e) => showHttpError(this.notify, e),
    });
  }

  fire(trigger: any) {
    this.busy.set(true);
    this.api.transition(this.id, trigger).subscribe({
      next: (rep) => { this.r.set(rep); this.busy.set(false); },
      error: (e) => { this.busy.set(false); showHttpError(this.notify, e); },
    });
  }

  save() {
    this.busy.set(true);
    this.api.updateInspection(this.id, this.form).subscribe({
      next: (rep) => { this.r.set(rep); this.busy.set(false);
        this.notify.success('Saved'); },
      error: (e) => { this.busy.set(false); showHttpError(this.notify, e); },
    });
  }

  submit() {
    this.busy.set(true);
    this.api.updateInspection(this.id, this.form).subscribe({
      next: () => this.api.transition(this.id, 'SubmitForReview', undefined,
        this.inspectorSig() ?? undefined).subscribe({
          next: (rep) => { this.r.set(rep); this.busy.set(false);
            this.notify.success('Submitted'); },
          error: (e) => { this.busy.set(false); showHttpError(this.notify, e); },
        }),
      error: (e) => { this.busy.set(false); showHttpError(this.notify, e); },
    });
  }

  goFinalize() { this.router.navigate(['/blue-sticker', this.id, 'finalize']); }

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
