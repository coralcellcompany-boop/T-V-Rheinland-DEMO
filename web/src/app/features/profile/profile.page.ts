import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { ProfileApi } from '../../core/api/profile.api';
import { Profile } from '../../core/models/user.models';
import { SignaturePad } from '../certificates/components/signature-pad.component';
import { NotifyService } from '../../shared/services/notify.service';
import { showHttpError } from '../../shared/services/api-error.handler';

@Component({
  selector: 'tuv-profile-page',
  standalone: true,
  imports: [CommonModule, ButtonModule, TagModule, SignaturePad],
  template: `
    <div class="page">
      <header>
        <h1>My profile</h1>
        <p class="hint">Your stored signature is applied automatically whenever you sign a
          Blue Sticker report — there's no need to redraw it on every report.</p>
      </header>

      @if (me(); as p) {
        <section class="card">
          <h2>Account</h2>
          <dl>
            <div><dt>Name</dt><dd>{{ p.fullName || '—' }}</dd></div>
            <div><dt>Email</dt><dd>{{ p.email || '—' }}</dd></div>
            <div><dt>SAP No.</dt><dd>{{ p.sapNo || '—' }}</dd></div>
            <div><dt>Roles</dt>
              <dd><span class="chip" *ngFor="let r of p.roles">{{ r }}</span></dd>
            </div>
          </dl>
        </section>

        <section class="card">
          <header class="card-hdr">
            <h2>Signature</h2>
            @if (p.signaturePng) {
              <p-tag value="On file" severity="success" />
            } @else {
              <p-tag value="Not set" severity="warn" />
            }
          </header>

          @if (p.signaturePng) {
            <div class="current">
              <div class="sig-label">Current signature</div>
              <div class="sig-box">
                <img [src]="p.signaturePng" alt="Current signature" />
              </div>
            </div>
          }

          <h3>{{ p.signaturePng ? 'Replace' : 'Capture' }} signature</h3>
          <p class="hint">Sign with a finger or stylus. The drawing is stored as a PNG image and
            applied to every Blue Sticker report you sign from now on.</p>
          <tuv-signature-pad (commitSignature)="onCaptured($event)" />

          @if (pending()) {
            <div class="preview">
              <div class="sig-label">Preview</div>
              <div class="sig-box">
                <img [src]="pending()" alt="New signature preview" />
              </div>
              <div class="actions">
                <p-button label="Discard" severity="secondary" [text]="true"
                  (onClick)="pending.set(null)" />
                <p-button label="Save signature" icon="pi pi-save"
                  [loading]="saving()" (onClick)="save()" />
              </div>
            </div>
          }
        </section>
      } @else { <p class="loader">Loading…</p> }
    </div>
  `,
  styles: [`
    .page{max-width:840px;margin:0 auto;padding:1.5rem;display:flex;flex-direction:column;gap:1.2rem}
    h1{margin:0;font-size:1.6rem}
    h2{margin:0 0 .6rem 0;font-size:1.05rem}
    h3{margin:1rem 0 .3rem 0;font-size:.95rem}
    .hint{color:#64748b;font-size:.9rem;margin:.25rem 0}
    .card{background:#fff;border:1px solid #e5e9f2;border-radius:14px;padding:1rem 1.25rem}
    .card-hdr{display:flex;justify-content:space-between;align-items:center}
    dl{display:grid;grid-template-columns:1fr 1fr;gap:.5rem 1.5rem;margin:0}
    dl > div{display:flex;flex-direction:column}
    dt{font-size:.72rem;color:#64748b;text-transform:uppercase;letter-spacing:.05em}
    dd{margin:0;font-size:.95rem;color:#0f172a}
    .chip{display:inline-block;padding:.1rem .55rem;border-radius:999px;font-size:.72rem;
      font-weight:500;background:#eef2ff;color:#4338ca;border:1px solid #c7d2fe;margin-right:.3rem}
    .sig-label{font-size:.72rem;color:#64748b;text-transform:uppercase;letter-spacing:.05em;
      margin-bottom:.25rem}
    .sig-box{height:120px;border:1px solid #cbd5e1;border-radius:8px;display:flex;
      align-items:center;justify-content:center;background:#f8fafc;overflow:hidden;
      padding:.5rem;max-width:340px}
    .sig-box img{max-height:100%;max-width:100%;object-fit:contain}
    .current,.preview{margin-bottom:.8rem}
    .actions{display:flex;gap:.5rem;margin-top:.6rem}
    .loader{padding:2rem;text-align:center;color:#64748b}
  `],
})
export class ProfilePage {
  private api = inject(ProfileApi);
  private notify = inject(NotifyService);

  protected me = signal<Profile | null>(null);
  protected pending = signal<string | null>(null);
  protected saving = signal(false);

  constructor() { this.load(); }

  private load() {
    this.api.me().subscribe({
      next: (p) => this.me.set(p),
      error: (e) => showHttpError(this.notify, e),
    });
  }

  onCaptured(dataUrl: string) { this.pending.set(dataUrl); }

  save() {
    const sig = this.pending();
    if (!sig) return;
    this.saving.set(true);
    this.api.updateSignature(sig).subscribe({
      next: (p) => {
        this.saving.set(false);
        this.me.set(p);
        this.pending.set(null);
        this.notify.success('Signature saved');
      },
      error: (e) => { this.saving.set(false); showHttpError(this.notify, e); },
    });
  }
}
