import { CommonModule } from '@angular/common';
import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

type Tone = 'primary' | 'positive' | 'warn' | 'danger' | 'neutral';

@Component({
  selector: 'tuv-kpi-card',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <a class="card" [attr.data-tone]="tone()" [routerLink]="link() || null">
      <div class="header">
        <i class="pi" [ngClass]="icon()"></i>
        <span class="label">{{ label() }}</span>
      </div>
      <div class="value">
        @if (loading()) { <span class="skeleton">—</span> }
        @else { {{ value() ?? '—' }} }
      </div>
      <div class="hint" *ngIf="hint()">{{ hint() }}</div>
    </a>
  `,
  styles: [
    `
      :host { display: block; }
      .card {
        display: block;
        position: relative;
        padding: 1.1rem 1.2rem;
        border-radius: 14px;
        background: #ffffff;
        border: 1px solid #e5e9f2;
        text-decoration: none;
        color: inherit;
        transition: transform 0.18s ease, box-shadow 0.18s ease, border-color 0.18s ease;
        overflow: hidden;
      }
      .card::after {
        content: '';
        position: absolute; left: 0; top: 0; bottom: 0; width: 4px;
        background: var(--accent, #1d4ed8);
      }
      .card:hover {
        transform: translateY(-1px);
        box-shadow: 0 8px 24px -10px rgba(15, 23, 42, 0.18);
        border-color: var(--accent, #1d4ed8);
      }
      .header { display: flex; align-items: center; gap: 0.55rem; color: #64748b; font-size: 0.85rem; }
      .header .pi { font-size: 1rem; color: var(--accent, #1d4ed8); }
      .label { font-weight: 500; letter-spacing: 0.01em; }
      .value { font-size: 1.85rem; font-weight: 700; color: #0f172a; margin-top: 0.4rem; }
      .hint { font-size: 0.75rem; color: #94a3b8; margin-top: 0.25rem; }
      .skeleton {
        display: inline-block;
        width: 4ch; height: 1em;
        background: linear-gradient(90deg, #eef2f7, #e2e8f0, #eef2f7);
        background-size: 200% 100%;
        animation: shimmer 1.4s infinite;
        border-radius: 4px;
        color: transparent;
      }
      @keyframes shimmer { from { background-position: 0 0; } to { background-position: -200% 0; } }

      :host-context(.primary) .card    { --accent: #1d4ed8; }
      .card[data-tone='primary']  { --accent: #1d4ed8; }
      .card[data-tone='positive'] { --accent: #047857; }
      .card[data-tone='warn']     { --accent: #b45309; }
      .card[data-tone='danger']   { --accent: #b91c1c; }
      .card[data-tone='neutral']  { --accent: #64748b; }
    `,
  ],
})
export class KpiCard {
  readonly label = input.required<string>();
  readonly value = input<string | number | null | undefined>(undefined);
  readonly icon = input<string>('pi-chart-bar');
  readonly tone = input<Tone>('primary');
  readonly hint = input<string | undefined>();
  readonly link = input<string | undefined>();
  readonly loading = input<boolean>(false);
}
