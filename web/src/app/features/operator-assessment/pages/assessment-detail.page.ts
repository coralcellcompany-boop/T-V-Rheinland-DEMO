import { CommonModule, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DialogModule } from 'primeng/dialog';
import { TextareaModule } from 'primeng/textarea';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { InputNumberModule } from 'primeng/inputnumber';
import { TooltipModule } from 'primeng/tooltip';

import { PageHeader } from '../../../shared/components/page-header.component';
import { StatusPill } from '../../../shared/components/status-pill.component';
import { AssessmentsApi, PublicCardApi } from '../../../core/api/assessments.api';
import {
  AssessmentDetail, AssessmentResultLabel, AssessmentStateName,
  AssessmentTrigger, CompetencyCategoryLabel,
} from '../../../core/models/assessment.models';
import { AuthService } from '../../../core/auth/auth.service';
import { Roles } from '../../../core/models/auth.models';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';
import { AssessmentTimeline } from '../components/assessment-timeline.component';

interface AvailableTrigger {
  trigger: AssessmentTrigger;
  label: string; icon: string;
  severity: 'primary' | 'secondary' | 'success' | 'warn' | 'danger' | 'info';
  requireComments?: boolean;
}

@Component({
  selector: 'tuv-assessment-detail',
  standalone: true,
  imports: [
    CommonModule, FormsModule, DatePipe, RouterLink,
    ButtonModule, CardModule, DialogModule, TextareaModule,
    InputTextModule, SelectModule, InputNumberModule, TooltipModule,
    PageHeader, StatusPill, AssessmentTimeline,
  ],
  template: `
    @if (loading()) { <div class="loader">Loading assessment…</div> }
    @else if (a(); as ax) {
      <tuv-page-header [title]="ax.assessmentNo" icon="pi-verified"
        [subtitle]="ax.candidateName + ' · ' + categoryLabel(ax.category) + ' · ' + ax.clientName">
        <a class="back" routerLink="/assessments"><i class="pi pi-arrow-left"></i> Back to list</a>
      </tuv-page-header>

      <div class="grid">
        <section class="summary card">
          <header>
            <tuv-status-pill [value]="stateName(ax.state)" />
            <tuv-status-pill *ngIf="ax.result" [value]="resultLabel(ax.result)" />
          </header>
          <dl>
            <div><dt>Assessment date</dt><dd>{{ ax.assessmentDate | date: 'dd MMM yyyy' }}</dd></div>
            <div><dt>Next assessment</dt><dd>{{ ax.nextAssessmentDate ? (ax.nextAssessmentDate | date: 'dd MMM yyyy') : '—' }}</dd></div>
            <div><dt>Theoretical score</dt><dd>{{ ax.theoreticalScore != null ? ax.theoreticalScore + ' / 100' : '—' }}</dd></div>
            <div><dt>Practical score</dt><dd>{{ ax.practicalScore != null ? ax.practicalScore + ' / 100' : '—' }}</dd></div>
            <div class="span2"><dt>Location</dt><dd>{{ ax.location ?? '—' }}</dd></div>
            <div class="span2"><dt>Comments</dt><dd>{{ ax.comments ?? '—' }}</dd></div>
          </dl>

          <div *ngIf="ax.issuedCardNo" class="card-block">
            <div class="qr-frame">
              <img [src]="qrUrl(ax.issuedCardNo)" alt="Card QR" />
            </div>
            <div class="qr-meta">
              <strong>{{ ax.issuedCardNo }}</strong>
              <span>Competency card</span>
              <span class="links">
                <a [href]="pdfUrl(ax.issuedCardNo)" target="_blank" rel="noopener">
                  <i class="pi pi-file-pdf"></i> Open card PDF
                </a>
                ·
                <a [href]="verifyUrl(ax.issuedCardNo)" target="_blank" rel="noopener">verification page</a>
              </span>
            </div>
          </div>
        </section>

        <section class="actions card">
          <h3>Actions</h3>
          <div class="action-buttons" *ngIf="transitions().length; else noActions">
            @for (t of transitions(); track t.trigger) {
              <p-button [icon]="'pi ' + t.icon" [label]="t.label"
                [severity]="t.severity" (onClick)="prepareTrigger(t)" />
            }
          </div>
          <ng-template #noActions>
            <p class="muted">No actions are available to you in the current state.</p>
          </ng-template>
        </section>

        <!-- Score editor (only when mutable: Draft/Rejected) -->
        <section class="scores card">
          <header class="block-header">
            <h3>Scores & result</h3>
            <span class="muted" *ngIf="!isMutable()">
              <i class="pi pi-lock"></i> Read-only — assessment is in {{ stateName(ax.state) }} state.
            </span>
          </header>

          <div class="scores-grid">
            <div>
              <label>Theoretical (0–100)</label>
              <p-inputNumber [(ngModel)]="theoretical" [min]="0" [max]="100"
                [disabled]="!isMutable()" [showButtons]="true" />
            </div>
            <div>
              <label>Practical (0–100)</label>
              <p-inputNumber [(ngModel)]="practical" [min]="0" [max]="100"
                [disabled]="!isMutable()" [showButtons]="true" />
            </div>
            <div>
              <label>Result</label>
              <p-select [options]="resultOptions" optionLabel="label" optionValue="value"
                [(ngModel)]="result" [disabled]="!isMutable()" appendTo="body" />
            </div>
            <div>
              <label>Next assessment date</label>
              <input pInputText type="date" [(ngModel)]="nextDate" [disabled]="!isMutable()" />
            </div>
            <div class="span2">
              <label>Comments</label>
              <textarea pTextarea rows="2" [(ngModel)]="comments" [disabled]="!isMutable()"
                placeholder="Notes from the assessor"></textarea>
            </div>
          </div>

          <div class="save-row" *ngIf="isMutable()">
            <p-button label="Save" icon="pi pi-save" [loading]="saving()" (onClick)="saveScores()" />
          </div>
        </section>

        <section class="timeline card">
          <h3>Lifecycle timeline</h3>
          <tuv-assessment-timeline [transitions]="ax.transitions" />
        </section>
      </div>

      <p-dialog [(visible)]="triggerDialog"
        [header]="pendingTrigger()?.label ?? ''"
        [modal]="true" [style]="{ width: '460px' }">
        <p>Confirm: {{ pendingTrigger()?.label }} this assessment?</p>
        @if (pendingTrigger()?.requireComments) {
          <label>Comments<span class="req">*</span></label>
          <textarea pTextarea rows="3" [(ngModel)]="triggerComments" placeholder="Required for this transition"></textarea>
        } @else {
          <label>Comments (optional)</label>
          <textarea pTextarea rows="3" [(ngModel)]="triggerComments"></textarea>
        }
        <ng-template pTemplate="footer">
          <p-button severity="secondary" label="Cancel" (onClick)="closeDialog()" [disabled]="firing()" />
          <p-button [label]="pendingTrigger()?.label ?? 'Confirm'"
            [severity]="pendingTrigger()?.severity"
            [loading]="firing()" [disabled]="!canFire()"
            (onClick)="fire()" />
        </ng-template>
      </p-dialog>
    }
  `,
  styles: [
    `
      :host { display: block; }
      .loader { padding: 3rem; text-align: center; color: #64748b; }
      .back { color: #1d4ed8; text-decoration: none; font-size: 0.85rem; }
      .back:hover { text-decoration: underline; }
      .grid {
        display: grid; grid-template-columns: 2fr 1fr; gap: 1rem;
        grid-template-areas: 'summary actions' 'scores scores' 'timeline timeline';
      }
      .summary { grid-area: summary; }
      .actions { grid-area: actions; }
      .scores  { grid-area: scores; }
      .timeline { grid-area: timeline; }
      .card { background: #fff; border: 1px solid #e5e9f2; border-radius: 14px; padding: 1.2rem 1.4rem; }
      .summary header { display: flex; gap: 0.5rem; margin-bottom: 1rem; }
      dl { display: grid; grid-template-columns: 1fr 1fr; gap: 0.85rem 1.5rem; margin: 0; }
      dl > div { display: flex; flex-direction: column; }
      dl .span2 { grid-column: 1 / -1; }
      dt { font-size: 0.72rem; color: #94a3b8; text-transform: uppercase; letter-spacing: 0.04em; }
      dd { margin: 0; font-weight: 500; color: #0f172a; font-size: 0.95rem; }

      .card-block {
        display: flex; align-items: center; gap: 1rem;
        margin-top: 1.2rem; padding: 0.85rem;
        background: linear-gradient(135deg, #ecfeff, #ffffff);
        border: 1px solid #a5f3fc; border-radius: 12px;
      }
      .qr-frame {
        width: 96px; height: 96px;
        background: #fff; border: 1px solid #cbd5e1; border-radius: 8px;
        display: flex; align-items: center; justify-content: center; padding: 6px;
      }
      .qr-frame img { width: 100%; height: 100%; image-rendering: pixelated; }
      .qr-meta { display: flex; flex-direction: column; gap: 0.2rem; }
      .qr-meta strong { font-family: ui-monospace, Menlo, monospace; color: #0f172a; }
      .qr-meta span, .qr-meta a { color: #0e7490; font-size: 0.85rem; }

      .actions h3, .scores h3, .timeline h3 { margin: 0 0 0.85rem 0; font-size: 0.95rem; color: #334155; }
      .action-buttons { display: flex; flex-direction: column; gap: 0.55rem; }
      .action-buttons :host ::ng-deep .p-button { width: 100%; justify-content: flex-start; }
      .muted { color: #94a3b8; font-size: 0.78rem; display: inline-flex; align-items: center; gap: 0.35rem; }

      .block-header { display: flex; justify-content: space-between; align-items: baseline; margin-bottom: 0.85rem; gap: 0.6rem; flex-wrap: wrap; }
      .scores-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
      .scores-grid .span2 { grid-column: 1 / -1; }
      .scores-grid label { font-size: 0.85rem; font-weight: 500; color: #334155; display: block; margin-bottom: 0.3rem; }
      .scores-grid input, .scores-grid textarea { width: 100%; }
      :host ::ng-deep .scores-grid .p-inputnumber, :host ::ng-deep .scores-grid .p-select { width: 100%; }
      .save-row { margin-top: 1rem; display: flex; justify-content: flex-end; }

      .req { color: #dc2626; margin-left: 0.15rem; }
      label { display: block; font-size: 0.85rem; font-weight: 500; color: #334155; margin: 0.6rem 0 0.3rem; }
      textarea { width: 100%; }

      @media (max-width: 1080px) {
        .grid { grid-template-columns: 1fr; grid-template-areas: 'summary' 'actions' 'scores' 'timeline'; }
      }
    `,
  ],
})
export class AssessmentDetailPage implements OnInit {
  private api = inject(AssessmentsApi);
  private cardsApi = inject(PublicCardApi);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private notify = inject(NotifyService);
  protected auth = inject(AuthService);

  protected loading = signal(true);
  protected a = signal<AssessmentDetail | null>(null);
  protected saving = signal(false);

  // Score editor state (kept in sync with cert detail pattern: live-edited via ngModel)
  protected theoretical: number | null = null;
  protected practical: number | null = null;
  protected result = 0;
  protected nextDate: string | null = null;
  protected comments: string | null = null;

  protected qrUrl = (no: string) => this.cardsApi.qrUrl(no);
  protected verifyUrl = (no: string) => `/verify-card/${encodeURIComponent(no)}`;
  protected pdfUrl = (no: string) => this.cardsApi.publicPdfUrl(no);

  protected stateName = (s: number) => AssessmentStateName[s] ?? 'Unknown';
  protected resultLabel = (r: number) => AssessmentResultLabel[r];
  protected categoryLabel = (c: number) => CompetencyCategoryLabel[c] ?? 'Unknown';

  protected resultOptions = Object.entries(AssessmentResultLabel).map(([v, l]) => ({
    value: Number(v), label: l,
  }));

  protected isMutable = computed(() => {
    const s = this.a()?.state;
    return s === 0 /* Draft */ || s === 3 /* Rejected */;
  });

  protected transitions = computed<AvailableTrigger[]>(() => {
    const ax = this.a();
    if (!ax) return [];
    const isAssessor = this.auth.hasAnyRole([Roles.Manager, Roles.Inspector, Roles.TechReviewer]);
    const isManager = this.auth.hasRole(Roles.Manager);

    switch (ax.state) {
      case 0: // Draft
        return isAssessor ? [{ trigger: 'Submit', label: 'Submit for approval', icon: 'pi-send', severity: 'primary' }] : [];
      case 1: // Submitted
        return isManager ? [
          { trigger: 'Approve', label: 'Approve', icon: 'pi-check', severity: 'success' },
          { trigger: 'Reject', label: 'Reject', icon: 'pi-times', severity: 'danger', requireComments: true },
        ] : [];
      case 3: // Rejected
        return isAssessor ? [{ trigger: 'Resubmit', label: 'Resubmit', icon: 'pi-send', severity: 'primary' }] : [];
      default:
        return [];
    }
  });

  protected triggerDialog = false;
  protected pendingTrigger = signal<AvailableTrigger | null>(null);
  protected triggerComments = '';
  protected firing = signal(false);

  protected canFire = () => {
    const t = this.pendingTrigger();
    if (!t) return false;
    if (t.requireComments && !(this.triggerComments?.trim())) return false;
    return !this.firing();
  };

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) { this.router.navigate(['/assessments']); return; }
    this.load(id);
  }

  private load(id: string) {
    this.loading.set(true);
    this.api.get(id).subscribe({
      next: (a) => {
        this.a.set(a);
        this.theoretical = a.theoreticalScore;
        this.practical = a.practicalScore;
        this.result = a.result;
        this.nextDate = a.nextAssessmentDate;
        this.comments = a.comments;
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        showHttpError(this.notify, err);
        this.router.navigate(['/assessments']);
      },
    });
  }

  saveScores() {
    const ax = this.a();
    if (!ax) return;
    this.saving.set(true);
    this.api.update(ax.id, {
      assessmentDate: ax.assessmentDate,
      nextAssessmentDate: this.nextDate,
      location: ax.location,
      theoreticalScore: this.theoretical,
      practicalScore: this.practical,
      result: this.result,
      comments: this.comments,
    }).subscribe({
      next: (updated) => {
        this.saving.set(false);
        this.a.set(updated);
        this.notify.success('Saved.');
      },
      error: (err) => {
        this.saving.set(false);
        showHttpError(this.notify, err);
      },
    });
  }

  prepareTrigger(t: AvailableTrigger) {
    this.pendingTrigger.set(t);
    this.triggerComments = '';
    this.triggerDialog = true;
  }
  closeDialog() {
    this.triggerDialog = false;
    this.pendingTrigger.set(null);
    this.triggerComments = '';
  }
  fire() {
    const ax = this.a(); const t = this.pendingTrigger();
    if (!ax || !t) return;
    this.firing.set(true);
    this.api.transition(ax.id, t.trigger, this.triggerComments?.trim() || undefined).subscribe({
      next: (updated) => {
        this.firing.set(false);
        this.a.set(updated);
        this.notify.success(`${t.label} succeeded — now ${this.stateName(updated.state)}.`);
        this.closeDialog();
      },
      error: (err) => { this.firing.set(false); showHttpError(this.notify, err); },
    });
  }
}
