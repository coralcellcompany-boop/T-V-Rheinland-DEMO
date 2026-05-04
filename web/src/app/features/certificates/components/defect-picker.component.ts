import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { DefectCode, DefectsApi } from '../../../core/api/defects.api';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';

/**
 * Modal dialog that shows the defect catalogue scoped to the certificate's equipment type.
 * The user picks one or more defects; the parent component decides where to apply them
 * (e.g. into the checklist row's remark, or into FindingsJson). Generic defects (not tied to
 * any equipment type) always appear at the top.
 */
@Component({
  selector: 'tuv-defect-picker',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonModule, DialogModule, InputTextModule, TagModule],
  template: `
    <p-dialog
      [(visible)]="open"
      (visibleChange)="visibleChange($event)"
      header="Defect catalogue"
      [modal]="true"
      [style]="{ width: '720px' }"
      [draggable]="false"
      [resizable]="false"
      styleClass="defect-picker-dialog">
      <div class="search">
        <span class="p-input-icon-left">
          <i class="pi pi-search"></i>
          <input pInputText [(ngModel)]="search" placeholder="Search code or description"
            (input)="onSearch()" />
        </span>
        <div class="legend">
          <span class="sev sev-Critical">Critical</span>
          <span class="sev sev-Major">Major</span>
          <span class="sev sev-Minor">Minor</span>
        </div>
      </div>

      @if (loading()) {
        <div class="state"><i class="pi pi-spin pi-spinner"></i> Loading defects…</div>
      } @else if (filtered().length === 0) {
        <div class="state empty">
          <i class="pi pi-inbox"></i>
          <p>No defects in the catalogue match your search.</p>
        </div>
      } @else {
        <ul class="list">
          @for (d of filtered(); track d.id) {
            <li>
              <button type="button" class="row" (click)="pick(d)">
                <span class="code">{{ d.code }}</span>
                <span class="desc">{{ d.description }}</span>
                <span class="sev" [attr.data-sev]="d.severity">{{ d.severity }}</span>
                <span class="scope">{{ d.equipmentTypeName ?? 'Generic' }}</span>
              </button>
            </li>
          }
        </ul>
      }
    </p-dialog>
  `,
  styles: [
    `
      :host ::ng-deep .defect-picker-dialog .p-dialog-content { padding: 0 1rem 1rem; }
      .search { display: flex; align-items: center; gap: 1rem; padding: 0.6rem 0; }
      .search input { width: 100%; }
      .search .p-input-icon-left { flex: 1; position: relative; }
      .search .p-input-icon-left i { position: absolute; left: 0.6rem; top: 50%; transform: translateY(-50%); color: #94a3b8; }
      .search .p-input-icon-left input { padding-left: 2rem; }
      .legend { display: flex; gap: 0.4rem; font-size: 0.72rem; }
      .legend .sev { padding: 0.18rem 0.5rem; border-radius: 999px; font-weight: 600; }
      .sev-Critical { background: #fee2e2; color: #b91c1c; }
      .sev-Major { background: #fef3c7; color: #b45309; }
      .sev-Minor { background: #e0f2fe; color: #075985; }

      .state { padding: 1.5rem; text-align: center; color: #64748b; }
      .state.empty .pi { font-size: 1.6rem; color: #cbd5e1; display: block; margin-bottom: 0.4rem; }

      .list { list-style: none; margin: 0; padding: 0; max-height: 50vh; overflow: auto; border: 1px solid #e5e9f2; border-radius: 10px; }
      .list li + li { border-top: 1px solid #f1f5f9; }
      .row {
        display: grid; grid-template-columns: 80px 1fr 80px 140px;
        align-items: center; gap: 0.6rem;
        width: 100%; padding: 0.55rem 0.85rem;
        text-align: left; background: #fff; border: 0; cursor: pointer;
        font-size: 0.85rem;
      }
      .row:hover { background: #f8fafc; }
      .row .code { font-family: 'JetBrains Mono', ui-monospace, monospace; font-weight: 700; color: #1e293b; }
      .row .desc { color: #334155; }
      .row .sev {
        display: inline-flex; justify-content: center;
        padding: 0.18rem 0.4rem; border-radius: 999px; font-size: 0.72rem; font-weight: 600;
      }
      .row .sev[data-sev='Critical'] { background: #fee2e2; color: #b91c1c; }
      .row .sev[data-sev='Major']    { background: #fef3c7; color: #b45309; }
      .row .sev[data-sev='Minor']    { background: #e0f2fe; color: #075985; }
      .row .scope { color: #64748b; font-size: 0.72rem; text-align: right; }
    `,
  ],
})
export class DefectPicker implements OnChanges {
  private api = inject(DefectsApi);
  private notify = inject(NotifyService);

  @Input() open = false;
  @Input() equipmentTypeId: string | null = null;
  @Output() openChange = new EventEmitter<boolean>();
  @Output() picked = new EventEmitter<DefectCode>();

  protected loading = signal(false);
  protected items = signal<DefectCode[]>([]);
  protected filtered = signal<DefectCode[]>([]);
  protected search = '';

  ngOnChanges(c: SimpleChanges) {
    if ((c['open'] && this.open) || c['equipmentTypeId']) {
      if (this.open) this.load();
    }
  }

  visibleChange(v: boolean) {
    this.open = v;
    this.openChange.emit(v);
  }

  private load() {
    this.loading.set(true);
    this.api.list(this.equipmentTypeId).subscribe({
      next: (rows) => {
        this.loading.set(false);
        this.items.set(rows);
        this.applyFilter();
      },
      error: (err) => {
        this.loading.set(false);
        showHttpError(this.notify, err);
      },
    });
  }

  onSearch() { this.applyFilter(); }

  private applyFilter() {
    const q = this.search.trim().toLowerCase();
    if (!q) { this.filtered.set(this.items()); return; }
    this.filtered.set(this.items().filter((d) =>
      d.code.toLowerCase().includes(q) || d.description.toLowerCase().includes(q)));
  }

  pick(d: DefectCode) {
    this.picked.emit(d);
    this.visibleChange(false);
  }
}
