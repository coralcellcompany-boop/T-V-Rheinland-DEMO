import { CommonModule, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { forkJoin } from 'rxjs';

import { CertificatesApi } from '../../../core/api/certificates.api';
import {
  CertificateDetail,
  CertificateInspectionTypeLabel,
  CertificateStateName,
  InspectionResultLabel,
  LoadTestKindLabel,
} from '../../../core/models/certificate.models';
import { PageHeader } from '../../../shared/components/page-header.component';
import { StatusPill } from '../../../shared/components/status-pill.component';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';

interface ChecklistRow {
  itemNo: string;
  acceptanceCriteria: string;
  referenceStandard: string;
  result: string;
  remark: string;
}

interface DiffField {
  label: string;
  earlier: string;
  later: string;
  changed: boolean;
}

interface ChecklistDiffRow {
  key: string;
  earlier: ChecklistRow | null;
  later: ChecklistRow | null;
  status: 'unchanged' | 'changed' | 'added' | 'removed';
}

@Component({
  selector: 'tuv-certificate-diff',
  standalone: true,
  imports: [CommonModule, DatePipe, RouterLink, ButtonModule, TagModule, PageHeader, StatusPill],
  template: `
    @if (loading()) {
      <div class="loader">Loading certificates…</div>
    } @else if (later() && earlier(); as _) {
      <tuv-page-header
        [title]="'Compare ' + later()!.certificateNo + ' ↔ ' + earlier()!.certificateNo"
        icon="pi-arrows-h"
        [subtitle]="later()!.equipmentTypeName + ' · ' + later()!.equipmentIdNo + ' · ' + later()!.clientName">
        <a [routerLink]="['/certificates', later()!.id]" class="back">
          <i class="pi pi-arrow-left"></i> Back to certificate
        </a>
      </tuv-page-header>

      <div class="grid">
        <section class="card summary">
          <h3>Summary differences</h3>
          <table>
            <thead>
              <tr>
                <th>Field</th>
                <th>{{ earlier()!.certificateNo }}<br><small>{{ earlier()!.inspectionDate | date: 'dd MMM yyyy' }}</small></th>
                <th>{{ later()!.certificateNo }}<br><small>{{ later()!.inspectionDate | date: 'dd MMM yyyy' }}</small></th>
              </tr>
            </thead>
            <tbody>
              @for (f of fieldDiffs(); track f.label) {
                <tr [class.changed]="f.changed">
                  <td class="label">{{ f.label }}</td>
                  <td>{{ f.earlier }}</td>
                  <td>{{ f.later }}</td>
                </tr>
              }
            </tbody>
          </table>
          <div class="legend">
            <span class="dot dot-changed"></span> changed
            <span class="dot dot-unchanged"></span> unchanged
          </div>
        </section>

        <section class="card checklist">
          <h3>Checklist differences ({{ checklistChangeCount() }} change<ng-container *ngIf="checklistChangeCount() !== 1">s</ng-container>)</h3>
          @if (checklistDiffs().length === 0) {
            <p class="muted">Both certificates have empty checklists.</p>
          } @else {
            <ul class="rows">
              @for (row of checklistDiffs(); track row.key) {
                <li [attr.data-status]="row.status">
                  <header>
                    <span class="status">
                      <p-tag [value]="row.status" [severity]="severity(row.status)" />
                    </span>
                    <span class="item-no">#{{ row.earlier?.itemNo ?? row.later?.itemNo ?? row.key }}</span>
                    <span class="criteria">
                      {{ row.later?.acceptanceCriteria ?? row.earlier?.acceptanceCriteria ?? '(unnamed)' }}
                    </span>
                  </header>
                  @if (row.status !== 'unchanged') {
                    <div class="cells">
                      <div class="cell earlier">
                        @if (row.earlier; as e) {
                          <div><strong>Result:</strong> {{ e.result }}</div>
                          <div *ngIf="e.referenceStandard"><strong>Ref:</strong> {{ e.referenceStandard }}</div>
                          <div *ngIf="e.remark"><strong>Remark:</strong> {{ e.remark }}</div>
                        } @else {
                          <em class="muted">— absent —</em>
                        }
                      </div>
                      <div class="cell later">
                        @if (row.later; as l) {
                          <div><strong>Result:</strong> {{ l.result }}</div>
                          <div *ngIf="l.referenceStandard"><strong>Ref:</strong> {{ l.referenceStandard }}</div>
                          <div *ngIf="l.remark"><strong>Remark:</strong> {{ l.remark }}</div>
                        } @else {
                          <em class="muted">— absent —</em>
                        }
                      </div>
                    </div>
                  }
                </li>
              }
            </ul>
          }
        </section>
      </div>
    }
  `,
  styles: [
    `
      :host { display: block; }
      .loader { padding: 3rem; text-align: center; color: #64748b; }
      .back { color: #1d4ed8; text-decoration: none; font-size: 0.85rem; }
      .back:hover { text-decoration: underline; }
      .grid { display: grid; gap: 1rem; }
      .card { background: #fff; border: 1px solid #e5e9f2; border-radius: 14px; padding: 1.2rem 1.4rem; }
      .card h3 { margin: 0 0 0.85rem; font-size: 0.95rem; }
      .muted { color: #94a3b8; font-style: italic; }

      table { width: 100%; border-collapse: collapse; font-size: 0.85rem; }
      th, td { padding: 0.5rem 0.6rem; text-align: left; border-top: 1px solid #f1f5f9; vertical-align: top; }
      thead th { font-size: 0.75rem; color: #64748b; text-transform: uppercase; letter-spacing: 0.04em; border-top: 0; }
      thead small { color: #94a3b8; font-weight: 400; }
      tbody tr.changed { background: #fffbeb; }
      .label { color: #475569; font-weight: 500; width: 22%; }

      .legend { display: flex; gap: 1rem; align-items: center; padding: 0.6rem 0 0; color: #64748b; font-size: 0.78rem; }
      .legend .dot { width: 10px; height: 10px; border-radius: 50%; display: inline-block; margin-right: 0.25rem; }
      .legend .dot-changed { background: #f59e0b; }
      .legend .dot-unchanged { background: #cbd5e1; }

      .rows { list-style: none; margin: 0; padding: 0; }
      .rows li { border-top: 1px dashed #f1f5f9; padding: 0.6rem 0; }
      .rows li:first-child { border-top: 0; }
      .rows li[data-status='unchanged'] { opacity: 0.55; }
      .rows header { display: flex; gap: 0.5rem; align-items: center; flex-wrap: wrap; font-size: 0.85rem; }
      .item-no { font-family: ui-monospace, Menlo, monospace; color: #94a3b8; min-width: 36px; }
      .criteria { font-weight: 500; color: #0f172a; }
      .cells { display: grid; grid-template-columns: 1fr 1fr; gap: 0.6rem; padding: 0.5rem 0 0; font-size: 0.78rem; }
      .cell { background: #f8fafc; border-radius: 8px; padding: 0.55rem 0.7rem; color: #334155; }
      .cell.later { background: #ecfdf5; }
      .cell.earlier { background: #fef2f2; }
    `,
  ],
})
export class CertificateDiffPage implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private api = inject(CertificatesApi);
  private notify = inject(NotifyService);

  protected loading = signal(true);
  protected later = signal<CertificateDetail | null>(null);
  protected earlier = signal<CertificateDetail | null>(null);
  protected fieldDiffs = signal<DiffField[]>([]);
  protected checklistDiffs = signal<ChecklistDiffRow[]>([]);
  protected checklistChangeCount = signal(0);

  ngOnInit() {
    const laterId = this.route.snapshot.paramMap.get('id');
    const earlierId = this.route.snapshot.queryParamMap.get('vs');
    if (!laterId || !earlierId) {
      this.notify.error('Compare requires two certificate IDs.');
      this.router.navigate(['/certificates', laterId ?? '']);
      return;
    }

    forkJoin({
      later: this.api.get(laterId),
      earlier: this.api.get(earlierId),
    }).subscribe({
      next: ({ later, earlier }) => {
        this.later.set(later);
        this.earlier.set(earlier);
        this.computeDiffs(earlier, later);
        this.loading.set(false);
      },
      error: (err) => { this.loading.set(false); showHttpError(this.notify, err); },
    });
  }

  protected severity(status: ChecklistDiffRow['status']) {
    switch (status) {
      case 'added':     return 'success';
      case 'removed':   return 'danger';
      case 'changed':   return 'warn';
      case 'unchanged': return 'secondary';
    }
  }

  private computeDiffs(earlier: CertificateDetail, later: CertificateDetail) {
    const fields: DiffField[] = [];
    const add = (label: string, e: any, l: any) => {
      const eStr = e == null || e === '' ? '—' : String(e);
      const lStr = l == null || l === '' ? '—' : String(l);
      fields.push({ label, earlier: eStr, later: lStr, changed: eStr !== lStr });
    };
    add('State', CertificateStateName[earlier.state], CertificateStateName[later.state]);
    add('Inspection type', CertificateInspectionTypeLabel[earlier.inspectionType], CertificateInspectionTypeLabel[later.inspectionType]);
    add('Result', InspectionResultLabel[earlier.result], InspectionResultLabel[later.result]);
    add('Load test', LoadTestKindLabel[earlier.loadTest], LoadTestKindLabel[later.loadTest]);
    add('Inspection date', earlier.inspectionDate, later.inspectionDate);
    add('Report issue', earlier.reportIssueDate, later.reportIssueDate);
    add('Next due', earlier.nextDueDate, later.nextDueDate);
    add('Standards', earlier.standards, later.standards);
    add('Sticker', earlier.stickerNo, later.stickerNo);
    this.fieldDiffs.set(fields);

    const eRows = parseChecklist(earlier.checklistJson);
    const lRows = parseChecklist(later.checklistJson);
    const keys = new Set<string>();
    const eByKey = new Map<string, ChecklistRow>();
    const lByKey = new Map<string, ChecklistRow>();
    eRows.forEach((r) => { const k = key(r); keys.add(k); eByKey.set(k, r); });
    lRows.forEach((r) => { const k = key(r); keys.add(k); lByKey.set(k, r); });

    const out: ChecklistDiffRow[] = [];
    let changes = 0;
    for (const k of keys) {
      const e = eByKey.get(k) ?? null;
      const l = lByKey.get(k) ?? null;
      let status: ChecklistDiffRow['status'] = 'unchanged';
      if (!e && l) status = 'added';
      else if (e && !l) status = 'removed';
      else if (e && l && (e.result !== l.result || e.remark !== l.remark || e.referenceStandard !== l.referenceStandard))
        status = 'changed';
      if (status !== 'unchanged') changes++;
      out.push({ key: k, earlier: e, later: l, status });
    }

    out.sort((a, b) => {
      const order = { changed: 0, added: 1, removed: 2, unchanged: 3 } as const;
      const o = order[a.status] - order[b.status];
      if (o !== 0) return o;
      return a.key.localeCompare(b.key, undefined, { numeric: true });
    });

    this.checklistDiffs.set(out);
    this.checklistChangeCount.set(changes);
  }
}

function parseChecklist(json: string | null): ChecklistRow[] {
  if (!json) return [];
  try {
    const doc = JSON.parse(json);
    const items = Array.isArray(doc?.items) ? doc.items : [];
    return items.map((i: any) => ({
      itemNo: String(i.itemNo ?? ''),
      acceptanceCriteria: String(i.acceptanceCriteria ?? ''),
      referenceStandard: String(i.referenceStandard ?? ''),
      result: String(i.result ?? 'NotSet'),
      remark: String(i.remark ?? ''),
    }));
  } catch {
    return [];
  }
}

function key(r: ChecklistRow): string {
  return r.itemNo || r.acceptanceCriteria || JSON.stringify(r);
}
