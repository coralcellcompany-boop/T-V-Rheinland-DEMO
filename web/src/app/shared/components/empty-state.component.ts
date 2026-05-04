import { CommonModule } from '@angular/common';
import { Component, input } from '@angular/core';

@Component({
  selector: 'tuv-empty-state',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="empty">
      <div class="icon-wrap">
        <i class="pi" [ngClass]="icon()"></i>
      </div>
      <h3>{{ title() }}</h3>
      <p *ngIf="message()">{{ message() }}</p>
      <div class="actions"><ng-content /></div>
    </div>
  `,
  styles: [
    `
      .empty {
        text-align: center;
        padding: 3rem 1.5rem;
        color: #64748b;
      }
      .icon-wrap {
        width: 64px; height: 64px;
        display: inline-flex; align-items: center; justify-content: center;
        background: #eef2ff; border-radius: 50%; color: #4f46e5;
        margin-bottom: 1rem;
      }
      .icon-wrap .pi { font-size: 1.65rem; }
      h3 { margin: 0; color: #0f172a; }
      p { margin: 0.4rem auto 1.2rem; max-width: 32ch; }
      .actions { display: inline-flex; gap: 0.5rem; }
    `,
  ],
})
export class EmptyState {
  readonly title = input.required<string>();
  readonly message = input<string | undefined>();
  readonly icon = input<string>('pi-inbox');
}
