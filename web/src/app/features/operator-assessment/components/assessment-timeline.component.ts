import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import {
  AssessmentStateName,
  AssessmentTransition,
} from '../../../core/models/assessment.models';

@Component({
  selector: 'tuv-assessment-timeline',
  standalone: true,
  imports: [CommonModule, DatePipe],
  template: `
    <div class="timeline">
      @for (step of items(); track step.id; let last = $last) {
        <div class="step" [class.last]="last">
          <div class="bullet" [attr.data-tone]="toneFor(step.toState)">
            <i class="pi" [ngClass]="iconFor(step.toState)"></i>
          </div>
          <div class="content">
            <div class="line1">
              <span class="from">{{ stateName(step.fromState) }}</span>
              <i class="pi pi-arrow-right sep"></i>
              <span class="to">{{ stateName(step.toState) }}</span>
            </div>
            <div class="line2">
              <span class="actor">{{ step.actorRole }}</span>
              <span class="dot">·</span>
              <span class="when">{{ step.atUtc | date: 'dd MMM yyyy HH:mm' }}</span>
            </div>
            <div class="comment" *ngIf="step.comments">{{ step.comments }}</div>
          </div>
        </div>
      } @empty {
        <div class="empty">No transitions yet — submit the assessment to start the workflow.</div>
      }
    </div>
  `,
  styles: [
    `
      :host { display: block; }
      .timeline { display: flex; flex-direction: column; position: relative; }
      .step { position: relative; display: grid; grid-template-columns: 40px 1fr; gap: 0.85rem; padding-bottom: 1.4rem; }
      .step:not(.last)::before { content: ''; position: absolute; left: 19px; top: 36px; bottom: -2px; width: 2px; background: linear-gradient(180deg, #cbd5e1, #e2e8f0); }
      .bullet { width: 38px; height: 38px; border-radius: 50%; display: flex; align-items: center; justify-content: center; background: var(--bg, #eef2ff); color: var(--fg, #4338ca); border: 2px solid #fff; box-shadow: 0 0 0 2px var(--border, #c7d2fe); font-size: 1rem; z-index: 1; }
      .bullet[data-tone='success'] { --bg: #dcfce7; --fg: #047857; --border: #86efac; }
      .bullet[data-tone='danger']  { --bg: #fee2e2; --fg: #b91c1c; --border: #fca5a5; }
      .bullet[data-tone='warning'] { --bg: #fef3c7; --fg: #b45309; --border: #fde68a; }
      .bullet[data-tone='info']    { --bg: #dbeafe; --fg: #1d4ed8; --border: #93c5fd; }
      .bullet[data-tone='neutral'] { --bg: #f1f5f9; --fg: #475569; --border: #cbd5e1; }
      .content { padding-top: 0.2rem; }
      .line1 { display: flex; align-items: center; gap: 0.45rem; font-weight: 600; color: #0f172a; font-size: 0.95rem; }
      .line1 .from { color: #94a3b8; font-weight: 500; }
      .line1 .sep { color: #94a3b8; font-size: 0.7rem; }
      .line2 { display: flex; gap: 0.4rem; align-items: center; font-size: 0.8rem; color: #64748b; margin-top: 0.15rem; }
      .line2 .actor { background: #f1f5f9; padding: 0.05rem 0.45rem; border-radius: 999px; font-weight: 500; color: #475569; }
      .line2 .dot { color: #cbd5e1; }
      .comment { margin-top: 0.5rem; padding: 0.55rem 0.75rem; background: #f8fafc; border-left: 3px solid #cbd5e1; border-radius: 4px; color: #334155; font-size: 0.85rem; }
      .empty { color: #94a3b8; font-style: italic; padding: 0.5rem 0; }
    `,
  ],
})
export class AssessmentTimeline {
  readonly transitions = input.required<AssessmentTransition[]>();
  protected items = computed(() =>
    [...this.transitions()].sort(
      (a, b) => new Date(a.atUtc).getTime() - new Date(b.atUtc).getTime()
    )
  );
  protected stateName = (s: number) => AssessmentStateName[s] ?? 'Unknown';

  protected iconFor(state: number): string {
    const map: Record<number, string> = {
      0: 'pi-pencil',         // Draft
      1: 'pi-send',           // Submitted
      2: 'pi-check',          // Approved
      3: 'pi-times',          // Rejected
      4: 'pi-hourglass',      // Expired
    };
    return map[state] ?? 'pi-circle';
  }

  protected toneFor(state: number): string {
    if (state === 2) return 'success';
    if (state === 3 || state === 4) return 'danger';
    if (state === 1) return 'info';
    return 'neutral';
  }
}
