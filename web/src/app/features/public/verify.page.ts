import { CommonModule, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { PublicStickerApi } from '../../core/api/stickers.api';
import { StickerPublicView, StickerStateName } from '../../core/models/sticker.models';

@Component({
  selector: 'tuv-verify',
  standalone: true,
  imports: [CommonModule, DatePipe, RouterLink, ProgressSpinnerModule],
  template: `
    <div class="page" [class.invalid]="loaded() && !view()?.isValidNow">
      <div class="brand">
        <div class="logo">T<span>R</span></div>
        <div class="name">
          <strong>TÜV Rheinland Arabia</strong>
          <span>Sticker verification</span>
        </div>
      </div>

      <main class="card">
        @if (loading()) {
          <div class="spinner"><p-progressSpinner ariaLabel="Loading" /></div>
        } @else if (notFound()) {
          <div class="status status--bad">
            <i class="pi pi-times-circle"></i>
            <h1>Sticker not found</h1>
            <p>No record matches <code>{{ stickerNo() }}</code>.</p>
            <p class="hint">If you believe this is a real TÜV Rheinland Arabia sticker, contact our office for verification.</p>
          </div>
        } @else if (view(); as v) {
          <div class="hero" [attr.data-tone]="hero().tone">
            <i class="pi" [ngClass]="hero().icon"></i>
            <div class="hero-text">
              <h1>{{ hero().title }}</h1>
              <p>{{ hero().subtitle }}</p>
            </div>
          </div>

          <dl>
            <div><dt>Sticker number</dt><dd><code>{{ v.stickerNo }}</code></dd></div>
            <div><dt>Status</dt><dd>{{ stateName(v.state) }}</dd></div>
            <div *ngIf="v.aramcoCategory">
              <dt>Aramco category</dt><dd>{{ v.aramcoCategory }}</dd>
            </div>
            <div *ngIf="v.equipmentTypeName">
              <dt>Equipment type</dt><dd>{{ v.equipmentTypeName }}</dd>
            </div>
            <div *ngIf="v.equipmentIdNo">
              <dt>Equipment ID</dt><dd><code>{{ v.equipmentIdNo }}</code></dd>
            </div>
            <div *ngIf="v.clientName">
              <dt>Client / contractor</dt><dd>{{ v.clientName }}</dd>
            </div>
            <div *ngIf="v.certificateNo">
              <dt>Certificate</dt><dd><code>{{ v.certificateNo }}</code></dd>
            </div>
            <div *ngIf="v.issuedAtUtc">
              <dt>Issued</dt><dd>{{ v.issuedAtUtc | date: 'dd MMM yyyy' }}</dd>
            </div>
            <div *ngIf="v.validUntil">
              <dt>Valid until</dt><dd>{{ v.validUntil | date: 'dd MMM yyyy' }}</dd>
            </div>
          </dl>
        }
      </main>

      <footer>
        <small>This page is provided by TÜV Rheinland Arabia LLC for sticker authenticity verification.</small>
        <small>For inspection records contact <a href="mailto:contact@tuv-arabia.local">contact@tuv-arabia.local</a>.</small>
      </footer>
    </div>
  `,
  styles: [
    `
      :host { display: block; min-height: 100vh; }
      .page {
        min-height: 100vh;
        background: linear-gradient(180deg, #06283d 0%, #0a3d62 60%, #f4f6fb 60%);
        display: flex; flex-direction: column; align-items: center;
        padding: 1rem;
      }
      .brand {
        display: flex; align-items: center; gap: 0.6rem;
        color: #fff; padding: 0.85rem 0 1.4rem 0;
      }
      .logo {
        width: 38px; height: 38px; border-radius: 10px;
        background: linear-gradient(135deg, #f97316, #ea580c);
        color: #fff; font-weight: 800;
        display: flex; align-items: center; justify-content: center;
        font-size: 1.05rem; letter-spacing: 0.04em;
      }
      .logo span { color: #fde68a; margin-left: 1px; }
      .name { display: flex; flex-direction: column; line-height: 1.1; }
      .name strong { font-size: 0.95rem; }
      .name span { font-size: 0.78rem; opacity: 0.85; }

      .card {
        width: 100%; max-width: 520px;
        background: #fff;
        border-radius: 18px;
        box-shadow: 0 30px 60px -30px rgba(0,0,0,0.35);
        overflow: hidden;
        margin-bottom: 1.6rem;
      }
      .spinner { display: flex; justify-content: center; padding: 2.5rem; }

      .hero {
        display: flex; align-items: center; gap: 0.85rem;
        padding: 1.4rem 1.5rem;
        color: #fff;
      }
      .hero[data-tone='good']    { background: linear-gradient(135deg, #047857, #10b981); }
      .hero[data-tone='warn']    { background: linear-gradient(135deg, #b45309, #f59e0b); }
      .hero[data-tone='bad']     { background: linear-gradient(135deg, #b91c1c, #ef4444); }
      .hero[data-tone='neutral'] { background: linear-gradient(135deg, #475569, #64748b); }
      .hero .pi { font-size: 2rem; }
      .hero h1 { margin: 0; font-size: 1.25rem; font-weight: 700; }
      .hero p  { margin: 0.2rem 0 0; opacity: 0.92; font-size: 0.9rem; }

      dl {
        display: flex; flex-direction: column;
        margin: 0; padding: 0.5rem 0;
      }
      dl > div {
        display: flex; justify-content: space-between; align-items: baseline;
        padding: 0.7rem 1.4rem;
        border-bottom: 1px solid #f1f5f9;
        gap: 0.6rem;
      }
      dl > div:last-child { border-bottom: 0; }
      dt { color: #64748b; font-size: 0.8rem; }
      dd { margin: 0; color: #0f172a; font-weight: 600; text-align: right; word-break: break-word; }
      code { font-family: ui-monospace, Menlo, monospace; font-size: 0.9rem; background: #f1f5f9; padding: 0.05rem 0.4rem; border-radius: 4px; }

      .status { padding: 2rem 1.5rem; text-align: center; }
      .status .pi { font-size: 2.2rem; }
      .status--bad { color: #b91c1c; }
      .status h1 { margin: 0.5rem 0 0.4rem; font-size: 1.15rem; }
      .status p  { margin: 0.2rem 0; color: #475569; }
      .status .hint { color: #94a3b8; font-size: 0.82rem; margin-top: 0.85rem; }

      footer { color: #94a3b8; text-align: center; max-width: 520px; }
      footer small { display: block; margin: 0.2rem 0; font-size: 0.75rem; }
      footer a { color: #93c5fd; }
    `,
  ],
})
export class VerifyPage implements OnInit {
  private route = inject(ActivatedRoute);
  private api = inject(PublicStickerApi);

  protected loading = signal(true);
  protected loaded = signal(false);
  protected notFound = signal(false);
  protected view = signal<StickerPublicView | null>(null);
  protected stickerNo = signal<string>('');

  protected stateName = (s: number) => StickerStateName[s] ?? 'Unknown';

  protected hero = computed(() => {
    const v = this.view();
    if (!v) return { tone: 'neutral', icon: 'pi-question', title: 'Loading', subtitle: '' };
    if (v.isValidNow) {
      return {
        tone: 'good',
        icon: 'pi-check-circle',
        title: 'Valid sticker',
        subtitle: 'This certification is currently in effect.',
      };
    }
    if (v.state === 4) {
      return {
        tone: 'bad',
        icon: 'pi-ban',
        title: 'Voided sticker',
        subtitle: 'This sticker has been withdrawn by TÜV.',
      };
    }
    if (v.state === 3) {
      return {
        tone: 'warn',
        icon: 'pi-replay',
        title: 'Replaced',
        subtitle: 'A newer sticker has been issued for this equipment.',
      };
    }
    if (v.state === 5) {
      return {
        tone: 'bad',
        icon: 'pi-hourglass',
        title: 'Expired',
        subtitle: 'This sticker is past its validity date.',
      };
    }
    return {
      tone: 'warn',
      icon: 'pi-info-circle',
      title: 'Not in service',
      subtitle: 'Sticker exists but is not currently issued.',
    };
  });

  ngOnInit() {
    const no = this.route.snapshot.paramMap.get('stickerNo') ?? '';
    this.stickerNo.set(no);
    if (!no) { this.notFound.set(true); this.loaded.set(true); this.loading.set(false); return; }
    this.api.verify(no).subscribe({
      next: (v) => { this.view.set(v); this.loading.set(false); this.loaded.set(true); },
      error: () => { this.notFound.set(true); this.loading.set(false); this.loaded.set(true); },
    });
  }
}
