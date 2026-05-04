import { CommonModule } from '@angular/common';
import { Component, input } from '@angular/core';

@Component({
  selector: 'tuv-page-header',
  standalone: true,
  imports: [CommonModule],
  template: `
    <header class="page-header">
      <div class="titles">
        <i class="pi" [ngClass]="icon()" *ngIf="icon()"></i>
        <div>
          <h1>{{ title() }}</h1>
          <p *ngIf="subtitle()">{{ subtitle() }}</p>
        </div>
      </div>
      <div class="actions"><ng-content /></div>
    </header>
  `,
  styles: [
    `
      :host { display: block; margin-bottom: 1.25rem; }
      .page-header {
        display: flex; align-items: center; justify-content: space-between;
        gap: 1.5rem; flex-wrap: wrap;
      }
      .titles { display: flex; align-items: center; gap: 0.85rem; }
      .titles .pi { font-size: 1.5rem; color: #1d4ed8; }
      h1 { font-size: 1.55rem; margin: 0; color: #0f172a; }
      p  { margin: 0.2rem 0 0 0; color: #64748b; font-size: 0.9rem; }
      .actions { display: flex; gap: 0.5rem; align-items: center; }
    `,
  ],
})
export class PageHeader {
  readonly title = input.required<string>();
  readonly subtitle = input<string | undefined>();
  readonly icon = input<string | undefined>();
}
