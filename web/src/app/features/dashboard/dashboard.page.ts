import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DashboardApi } from '../../core/api/certificates.api';
import { StickersApi } from '../../core/api/stickers.api';
import { StickerStockSummary } from '../../core/models/sticker.models';
import { AuthService } from '../../core/auth/auth.service';
import { Roles } from '../../core/models/auth.models';
import { DashboardKpis, InspectorAnalysisRow, RecentActivityItem } from '../../core/models/certificate.models';
import { KpiCard } from '../../shared/components/kpi-card.component';
import { PageHeader } from '../../shared/components/page-header.component';
import { NotifyService } from '../../shared/services/notify.service';
import { showHttpError } from '../../shared/services/api-error.handler';

@Component({
  selector: 'tuv-dashboard',
  standalone: true,
  imports: [CommonModule, DatePipe, RouterLink, ButtonModule, KpiCard, PageHeader],
  template: `
    <section class="hero">
      <div class="msg">
        <h1>Welcome back, {{ firstName() }}.</h1>
        <p>{{ greeting() }}</p>
      </div>
      <div class="hero-actions">
        <p-button icon="pi pi-file-check" label="Browse certificates"
          severity="secondary" (onClick)="go('/certificates')" />
        <p-button icon="pi pi-thumbs-up" label="Open approvals"
          (onClick)="go('/approvals')" />
      </div>
    </section>

    @if (showLowStock()) {
      <div class="alert alert-warn">
        <i class="pi pi-exclamation-triangle"></i>
        <div class="alert-body">
          <strong>Sticker stock is low.</strong>
          Only {{ stickerSummary()?.unallocated }} unallocated sticker<ng-container *ngIf="stickerSummary()?.unallocated !== 1">s</ng-container>
          remaining (threshold {{ stickerSummary()?.lowStockThreshold }}).
          Procure a fresh batch before approvals start failing.
        </div>
        <a routerLink="/stickers" class="alert-action">Open sticker register →</a>
      </div>
    }

    <h2 class="section-title">Snapshot</h2>
    <div class="kpis">
      <tuv-kpi-card label="Total certificates" icon="pi-file-check" tone="primary"
        [value]="kpis()?.totalCertificates" [loading]="loading()"
        [hint]="kpis()?.certificatesThisMonth + ' added this month'"
        link="/certificates" />
      <tuv-kpi-card label="Pending approval" icon="pi-clock" tone="warn"
        [value]="kpis()?.pending" [loading]="loading()"
        link="/approvals" />
      <tuv-kpi-card label="Rejected" icon="pi-times" tone="danger"
        [value]="kpis()?.rejected" [loading]="loading()"
        link="/approvals" />
      <tuv-kpi-card label="Due in 30 days" icon="pi-calendar" tone="warn"
        [value]="kpis()?.dueSoon" [loading]="loading()" />
      <tuv-kpi-card label="Expired" icon="pi-hourglass" tone="danger"
        [value]="kpis()?.expired" [loading]="loading()" />
      <tuv-kpi-card label="Active equipment" icon="pi-wrench" tone="positive"
        [value]="kpis()?.activeEquipment" [loading]="loading()"
        link="/equipment" />
      <tuv-kpi-card label="Clients" icon="pi-building" tone="neutral"
        [value]="kpis()?.clients" [loading]="loading()"
        link="/clients" />
    </div>

    @if (showAnalysis()) {
      <h2 class="section-title">Inspector analysis (last 90 days)</h2>
      <div class="analysis-grid">
        <!-- Chart: equipment reviewed per inspector -->
        <div class="card chart-card">
          <h3 class="card-title">Equipment reviewed per inspector</h3>
          @if (analysisLoading()) {
            <div class="loader">Loading…</div>
          } @else if (analysis().length === 0) {
            <p class="muted">No inspection activity in this window yet.</p>
          } @else {
            <div class="bars">
              @for (r of analysis(); track r.inspectorId) {
                <div class="bar-row">
                  <span class="bar-label" [title]="r.inspectorName">{{ r.inspectorName }}</span>
                  <div class="bar-track">
                    <div class="bar-fill" [style.width.%]="barPct(r.equipmentCount)"></div>
                  </div>
                  <span class="bar-value">{{ r.equipmentCount }}</span>
                </div>
              }
            </div>
          }
        </div>

        <!-- Table: per inspector & companies -->
        <div class="card">
          <h3 class="card-title">Inspector &amp; company breakdown</h3>
          @if (!analysisLoading() && analysis().length > 0) {
            <table class="analysis-table">
              <thead>
                <tr><th>Inspector</th><th>Equipment</th><th>Companies</th><th>Created</th><th>Approved</th></tr>
              </thead>
              <tbody>
                @for (r of analysis(); track r.inspectorId) {
                  <tr>
                    <td>{{ r.inspectorName }}</td>
                    <td class="num">{{ r.equipmentCount }}</td>
                    <td class="num">{{ r.companiesCount }}</td>
                    <td class="num">{{ r.certificatesCreated }}</td>
                    <td class="num">{{ r.certificatesApproved }}</td>
                  </tr>
                }
              </tbody>
            </table>
          } @else if (!analysisLoading()) {
            <p class="muted">No data.</p>
          }
        </div>
      </div>
    }

    <h2 class="section-title">Recent activity</h2>
    <div class="activity card">
      @if (activityLoading()) {
        <div class="loader">Loading activity…</div>
      } @else if (activity().length === 0) {
        <p class="muted">Nothing has happened yet. Create a client and a certificate to see your audit trail here.</p>
      } @else {
        <ul class="feed">
          @for (a of activity(); track a.entityId + a.atUtc) {
            <li>
              <i class="pi" [ngClass]="iconForAction(a.action)"></i>
              <div class="meta">
                <div>
                  <strong>{{ humanize(a.entityName) }}</strong>
                  <span class="action" [attr.data-action]="a.action">{{ a.action }}</span>
                  <span *ngIf="a.actorRole" class="role">{{ a.actorRole }}</span>
                </div>
                <div class="when">{{ a.atUtc | date: 'medium' }}</div>
              </div>
            </li>
          }
        </ul>
      }
    </div>
  `,
  styles: [
    `
      :host { display: block; }
      .hero {
        background: linear-gradient(135deg, #0a3d62, #06283d);
        color: #fff; border-radius: 18px;
        padding: 1.6rem 1.8rem;
        display: flex; align-items: center; justify-content: space-between;
        gap: 1rem; margin-bottom: 1.5rem;
        box-shadow: 0 12px 40px -18px rgba(15, 23, 42, 0.55);
        position: relative; overflow: hidden;
      }
      .hero::after {
        content: '';
        position: absolute; right: -40px; top: -60px;
        width: 240px; height: 240px;
        background: radial-gradient(circle, rgba(249, 115, 22, 0.4), transparent 70%);
        pointer-events: none;
      }
      .hero h1 { margin: 0; font-size: 1.55rem; font-weight: 700; }
      .hero p  { margin: 0.4rem 0 0; opacity: 0.85; font-size: 0.95rem; }
      .hero-actions { display: flex; gap: 0.55rem; z-index: 1; }

      .alert {
        display: flex; align-items: center; gap: 0.8rem; padding: 0.85rem 1.1rem;
        margin-bottom: 1rem; border-radius: 12px; font-size: 0.9rem;
      }
      .alert .pi { font-size: 1.3rem; }
      .alert-warn { background: #fef3c7; color: #78350f; border: 1px solid #fcd34d; }
      .alert-warn .pi { color: #b45309; }
      .alert-body { flex: 1; }
      .alert-action {
        color: #b45309; font-weight: 600; text-decoration: none; white-space: nowrap;
      }
      .alert-action:hover { text-decoration: underline; }

      .section-title {
        font-size: 0.85rem; font-weight: 600; text-transform: uppercase;
        letter-spacing: 0.08em; color: #475569; margin: 1.3rem 0 0.7rem 0;
      }
      .kpis {
        display: grid; gap: 1rem;
        grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      }
      .card { background: #fff; border: 1px solid #e5e9f2; border-radius: 14px; padding: 1rem 1.2rem; }
      .loader { padding: 1rem; color: #64748b; text-align: center; }
      .muted { color: #94a3b8; }

      .feed { list-style: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: 0.6rem; }
      .feed li {
        display: grid; grid-template-columns: 32px 1fr; gap: 0.75rem;
        padding: 0.55rem 0.7rem; border-radius: 10px;
      }
      .feed li:hover { background: #f8fafc; }
      .feed .pi {
        width: 32px; height: 32px; display: flex; align-items: center; justify-content: center;
        background: #eef2ff; color: #4338ca; border-radius: 50%;
      }
      .feed .meta { display: flex; align-items: center; justify-content: space-between; gap: 0.5rem; }
      .feed strong { color: #0f172a; font-weight: 600; }
      .feed .action {
        margin-left: 0.5rem; font-size: 0.7rem; padding: 0.05rem 0.45rem; border-radius: 999px;
        background: #eef2ff; color: #4338ca; font-weight: 500;
      }
      .feed .action[data-action='Create']  { background: #dcfce7; color: #047857; }
      .feed .action[data-action='Update']  { background: #e0f2fe; color: #075985; }
      .feed .action[data-action='Delete']  { background: #fee2e2; color: #b91c1c; }
      .feed .role {
        margin-left: 0.45rem; font-size: 0.7rem; color: #64748b; font-style: italic;
      }
      .feed .when { color: #94a3b8; font-size: 0.78rem; white-space: nowrap; }

      .analysis-grid {
        display: grid; gap: 1rem;
        grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
      }
      .card-title { margin: 0 0 0.9rem; font-size: 0.95rem; color: #334155; }
      .bars { display: flex; flex-direction: column; gap: 0.6rem; }
      .bar-row { display: grid; grid-template-columns: 130px 1fr 36px; align-items: center; gap: 0.6rem; }
      .bar-label { font-size: 0.8rem; color: #334155; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
      .bar-track { background: #eef2f7; border-radius: 999px; height: 14px; overflow: hidden; }
      .bar-fill {
        height: 100%; border-radius: 999px;
        background: linear-gradient(90deg, #0a64a4, #38bdf8); min-width: 3px;
        transition: width 0.4s ease;
      }
      .bar-value { font-size: 0.8rem; font-weight: 600; color: #0f172a; text-align: right; }
      .analysis-table { width: 100%; border-collapse: collapse; font-size: 0.84rem; }
      .analysis-table th, .analysis-table td { padding: 0.45rem 0.55rem; text-align: left; border-bottom: 1px solid #f1f5f9; }
      .analysis-table th { color: #64748b; font-weight: 600; font-size: 0.72rem; text-transform: uppercase; letter-spacing: 0.03em; }
      .analysis-table td.num, .analysis-table th:not(:first-child) { text-align: right; }
    `,
  ],
})
export class DashboardPage {
  private api = inject(DashboardApi);
  private stickersApi = inject(StickersApi);
  protected auth = inject(AuthService);
  private notify = inject(NotifyService);
  private router = inject(Router);

  protected loading = signal(true);
  protected activityLoading = signal(true);
  protected kpis = signal<DashboardKpis | null>(null);
  protected activity = signal<RecentActivityItem[]>([]);
  protected stickerSummary = signal<StickerStockSummary | null>(null);

  protected analysisLoading = signal(true);
  protected analysis = signal<InspectorAnalysisRow[]>([]);
  private maxEquip = computed(() =>
    Math.max(1, ...this.analysis().map(r => r.equipmentCount)));
  protected barPct = (v: number) => Math.round((v / this.maxEquip()) * 100);
  protected showAnalysis = () =>
    this.auth.hasRole(Roles.Manager) || this.auth.hasRole(Roles.Coordinator);

  protected showLowStock = () => {
    const s = this.stickerSummary();
    if (!s || !s.isLowStock) return false;
    return this.auth.hasRole(Roles.Manager) || this.auth.hasRole(Roles.Coordinator);
  };

  protected firstName = () => {
    const fn = this.auth.user()?.fullName ?? this.auth.user()?.userName ?? '';
    return fn.split(/[\s@.]+/)[0] || 'there';
  };
  protected greeting = () => {
    const h = new Date().getHours();
    if (h < 12) return 'Here is your morning briefing.';
    if (h < 17) return 'Here is the afternoon snapshot.';
    return 'Here is what happened today.';
  };

  protected go(path: string) { this.router.navigate([path]); }

  constructor() {
    this.api.kpis().subscribe({
      next: (k) => { this.kpis.set(k); this.loading.set(false); },
      error: (err) => { this.loading.set(false); showHttpError(this.notify, err); },
    });
    this.api.activity(15).subscribe({
      next: (a) => { this.activity.set(a); this.activityLoading.set(false); },
      error: (err) => { this.activityLoading.set(false); showHttpError(this.notify, err); },
    });
    if (this.auth.hasRole(Roles.Manager) || this.auth.hasRole(Roles.Coordinator)) {
      this.stickersApi.stockSummary().subscribe({
        next: (s) => this.stickerSummary.set(s),
        error: () => { /* keep dashboard usable even if low-stock probe fails */ },
      });
      this.api.inspectorAnalysis(90).subscribe({
        next: (rows) => { this.analysis.set(rows); this.analysisLoading.set(false); },
        error: () => { this.analysisLoading.set(false); },
      });
    } else {
      this.analysisLoading.set(false);
    }
  }

  protected humanize(name: string): string {
    return name.replace(/([A-Z])/g, ' $1').trim();
  }

  protected iconForAction(action: string): string {
    return action === 'Create' ? 'pi-plus'
      : action === 'Update' ? 'pi-refresh'
      : action === 'Delete' ? 'pi-trash'
      : 'pi-bolt';
  }
}
