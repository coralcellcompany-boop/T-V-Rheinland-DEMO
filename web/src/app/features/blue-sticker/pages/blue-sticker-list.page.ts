import { CommonModule, DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { BlueStickerApi } from '../../../core/api/blue-sticker.api';
import { BlueStickerReportListItem, BlueStickerStateName } from '../../../core/models/blue-sticker.models';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';
import { AuthService } from '../../../core/auth/auth.service';
import { Roles } from '../../../core/models/auth.models';
import { BlueStickerCreateDialog } from '../components/blue-sticker-create-dialog.component';

@Component({
  standalone: true,
  imports: [CommonModule, DatePipe, ButtonModule, TableModule, BlueStickerCreateDialog],
  template: `
    <header class="hdr">
      <h2>Blue Sticker Inspections</h2>
      @if (canCreate()) {
        <p-button label="New batch" icon="pi pi-plus" (onClick)="openCreate()" />
      }
    </header>

    <div class="card">
      @if (loading()) { <div class="loader">Loading…</div> }
      @else {
        <p-table [value]="rows()" [rowHover]="true" styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr><th>Report</th><th>Job Order</th><th>Equipment</th>
              <th>Inspection date</th><th>State</th><th></th></tr>
          </ng-template>
          <ng-template pTemplate="body" let-r>
            <tr>
              <td>{{ r.reportNo }}</td>
              <td>{{ r.tuvJobOrderNo }}</td>
              <td>{{ r.equipmentIdNo }}</td>
              <td>{{ r.inspectionDate ? (r.inspectionDate | date: 'dd MMM yyyy') : '—' }}</td>
              <td>{{ stateName(r.state) }}</td>
              <td>
                <p-button label="Open" size="small" [text]="true"
                  (onClick)="open(r)" />
              </td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>

    <tuv-blue-sticker-create-dialog
      [open]="createOpen()"
      (created)="onCreated()"
      (cancel)="createOpen.set(false)" />
  `,
  styles: [`.card{background:#fff;border:1px solid #e5e9f2;border-radius:14px;padding:1rem}
    .loader{padding:2rem;text-align:center;color:#64748b}
    .hdr{display:flex;justify-content:space-between;align-items:center;
      margin-bottom:1rem;gap:1rem}
    .hdr h2{margin:0}`],
})
export class BlueStickerListPage {
  private api = inject(BlueStickerApi);
  private notify = inject(NotifyService);
  private router = inject(Router);
  private auth = inject(AuthService);
  protected loading = signal(true);
  protected rows = signal<BlueStickerReportListItem[]>([]);
  protected createOpen = signal(false);
  protected stateName = (s: number) => BlueStickerStateName[s];
  protected canCreate = () => this.auth.hasAnyRole([Roles.Coordinator, Roles.Manager]);

  constructor() { this.refresh(); }

  private refresh() {
    this.loading.set(true);
    this.api.list({ pageSize: 100 }).subscribe({
      next: (r) => { this.rows.set(r.items); this.loading.set(false); },
      error: (e) => { this.loading.set(false); showHttpError(this.notify, e); },
    });
  }
  open(r: BlueStickerReportListItem) {
    this.router.navigate(['/blue-sticker', r.id]);
  }
  openCreate() { this.createOpen.set(true); }
  onCreated() { this.createOpen.set(false); this.refresh(); }
}
