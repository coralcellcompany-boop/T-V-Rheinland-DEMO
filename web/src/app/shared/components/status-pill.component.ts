import { CommonModule } from '@angular/common';
import { Component, computed, input } from '@angular/core';

type Tone = 'neutral' | 'info' | 'success' | 'warning' | 'danger' | 'special';

const TONE_BY_LABEL: Record<string, Tone> = {
  // Certificate states
  Draft: 'neutral',
  Submitted: 'info',
  UnderReview: 'info',
  AwaitingApproval: 'warning',
  Approved: 'success',
  ClientSent: 'info',
  ClientAccepted: 'success',
  ClientRejected: 'danger',
  Rejected: 'danger',
  Voided: 'danger',
  Expired: 'danger',
  Archived: 'neutral',
  // Equipment / generic
  Active: 'success',
  Decommissioned: 'neutral',
  Sold: 'neutral',
  // Contract status
  Suspended: 'warning',
  Terminated: 'danger',
};

@Component({
  selector: 'tuv-status-pill',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span class="pill" [attr.data-tone]="tone()">
      <span class="dot"></span>
      <span class="label">{{ humanize() }}</span>
    </span>
  `,
  styles: [
    `
      :host { display: inline-flex; }
      .pill {
        display: inline-flex; align-items: center; gap: 0.45rem;
        padding: 0.2rem 0.65rem; border-radius: 999px;
        font-size: 0.78rem; font-weight: 600; line-height: 1.2;
        background: var(--bg, #eef2f7); color: var(--fg, #2c3e50);
        border: 1px solid var(--border, transparent);
        white-space: nowrap;
      }
      .dot {
        width: 6px; height: 6px; border-radius: 50%;
        background: currentColor; box-shadow: 0 0 0 3px rgba(255,255,255,0.55) inset;
      }
      .pill[data-tone='neutral']  { --bg: #eef2f7; --fg: #475569; --border: #d8dee9; }
      .pill[data-tone='info']     { --bg: #e8f2ff; --fg: #1d4ed8; --border: #bfdbfe; }
      .pill[data-tone='success']  { --bg: #e6f7ee; --fg: #047857; --border: #bbf7d0; }
      .pill[data-tone='warning']  { --bg: #fff7e6; --fg: #b45309; --border: #fde68a; }
      .pill[data-tone='danger']   { --bg: #fdecec; --fg: #b91c1c; --border: #fecaca; }
      .pill[data-tone='special']  { --bg: #f3e8ff; --fg: #6d28d9; --border: #e9d5ff; }
    `,
  ],
})
export class StatusPill {
  readonly value = input.required<string>();
  readonly toneOverride = input<Tone | undefined>();

  protected tone = computed<Tone>(
    () => this.toneOverride() ?? TONE_BY_LABEL[this.value()] ?? 'neutral'
  );

  protected humanize = computed(() => splitCamel(this.value()));
}

function splitCamel(s: string): string {
  return s
    .replace(/([A-Z])/g, ' $1')
    .replace(/_/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}
