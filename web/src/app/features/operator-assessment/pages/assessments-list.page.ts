import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { SelectModule } from 'primeng/select';
import { DialogModule } from 'primeng/dialog';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { toSignal } from '@angular/core/rxjs-interop';

import { PageHeader } from '../../../shared/components/page-header.component';
import { StatusPill } from '../../../shared/components/status-pill.component';
import { EmptyState } from '../../../shared/components/empty-state.component';

import { AssessmentsApi, CandidatesApi } from '../../../core/api/assessments.api';
import {
  AssessmentListItem, AssessmentResultLabel, AssessmentStateName,
  CandidateListItem, CompetencyCategoryLabel,
} from '../../../core/models/assessment.models';
import { AuthService } from '../../../core/auth/auth.service';
import { Roles } from '../../../core/models/auth.models';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';

@Component({
  selector: 'tuv-assessments-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule, DatePipe,
    ButtonModule, TableModule, InputTextModule, IconFieldModule, InputIconModule,
    SelectModule, DialogModule,
    PageHeader, StatusPill, EmptyState,
  ],
  template: `
    <tuv-page-header title="Operator Assessments" icon="pi-verified"
      subtitle="Theoretical + practical operator competency evaluations. Approved assessments auto-issue a TÜV competency card.">
      <p-button *ngIf="canCreate()" icon="pi pi-plus" label="New assessment" (onClick)="newDialog = true" />
    </tuv-page-header>

    <div class="filters">
      <p-iconfield iconPosition="left" class="grow">
        <p-inputicon styleClass="pi pi-search" />
        <input pInputText placeholder="Search by assessment number"
          [(ngModel)]="searchInput" (ngModelChange)="search$.next($event)" />
      </p-iconfield>
      <p-select [options]="categoryOptions" optionLabel="label" optionValue="value"
        [(ngModel)]="filterCategory" (ngModelChange)="onFilterChange()"
        [showClear]="true" placeholder="Any category" appendTo="body" styleClass="filter wide" />
      <p-select [options]="stateOptions" optionLabel="label" optionValue="value"
        [(ngModel)]="filterState" (ngModelChange)="onFilterChange()"
        [showClear]="true" placeholder="Any state" appendTo="body" styleClass="filter" />
    </div>

    <div class="card">
      @if (firstLoad()) { <div class="loader">Loading assessments…</div> }
      @else if (rows().length === 0) {
        <tuv-empty-state icon="pi-verified" title="No assessments yet"
          message="Create the first competency assessment from a candidate.">
          <p-button *ngIf="canCreate()" icon="pi pi-plus" label="New assessment" (onClick)="newDialog = true" />
        </tuv-empty-state>
      } @else {
        <p-table [value]="rows()" [rowHover]="true" styleClass="p-datatable-sm"
          [paginator]="true" [rows]="pageSize()" [totalRecords]="total()" [lazy]="true"
          [loading]="loading()"
          (onLazyLoad)="onLazyLoad($event)" [rowsPerPageOptions]="[10, 25, 50, 100]">
          <ng-template pTemplate="header">
            <tr>
              <th style="width: 14%">Assessment</th>
              <th style="width: 22%">Candidate</th>
              <th>Category</th>
              <th style="width: 14%">Date</th>
              <th style="width: 9%">Result</th>
              <th style="width: 12%">State</th>
              <th style="width: 14%">Card</th>
              <th style="width: 50px"></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-a>
            <tr (click)="open(a.id)" style="cursor: pointer">
              <td>
                <div class="cell-mono">{{ a.assessmentNo }}</div>
                <div class="muted">{{ a.clientName }}</div>
              </td>
              <td>
                <div class="name">{{ a.candidateName }}</div>
              </td>
              <td>{{ categoryLabel(a.category) }}</td>
              <td>{{ a.assessmentDate | date: 'dd MMM yyyy' }}</td>
              <td>
                <tuv-status-pill *ngIf="a.result" [value]="resultLabel(a.result)" />
              </td>
              <td><tuv-status-pill [value]="stateName(a.state)" /></td>
              <td>
                <span *ngIf="a.issuedCardNo" class="card-no">{{ a.issuedCardNo }}</span>
                <span *ngIf="!a.issuedCardNo" class="muted">—</span>
              </td>
              <td>
                <p-button icon="pi pi-arrow-right" severity="secondary" [text]="true" rounded
                  (onClick)="open(a.id); $event.stopPropagation()" />
              </td>
            </tr>
          </ng-template>
        </p-table>
      }
    </div>

    <!-- Create dialog -->
    <p-dialog [(visible)]="newDialog" [modal]="true" [style]="{ width: '480px' }"
      header="New assessment" [closable]="!creating()">
      <div class="new-form">
        <label>Candidate<span class="req">*</span></label>
        <p-select [options]="candidateOptions()" optionLabel="label" optionValue="value"
          [(ngModel)]="newCandidateId" appendTo="body" [filter]="true" filterBy="label"
          placeholder="Select candidate" />
        <label>Category<span class="req">*</span></label>
        <p-select [options]="categoryOptions" optionLabel="label" optionValue="value"
          [(ngModel)]="newCategory" appendTo="body" />
        <label>Assessment date<span class="req">*</span></label>
        <input pInputText type="date" [(ngModel)]="newAssessmentDate" />
        <label>Location</label>
        <input pInputText [(ngModel)]="newLocation" placeholder="e.g. Khobar training centre" />
      </div>
      <ng-template pTemplate="footer">
        <p-button severity="secondary" label="Cancel" (onClick)="closeNew()" [disabled]="creating()" />
        <p-button label="Create" icon="pi pi-plus" [loading]="creating()"
          [disabled]="!canSubmitNew()" (onClick)="createAssessment()" />
      </ng-template>
    </p-dialog>
  `,
  styles: [
    `
      :host { display: block; }
      .filters { display: flex; gap: 0.6rem; align-items: center; margin-bottom: 1rem; flex-wrap: wrap; }
      .filters .grow { flex: 1; min-width: 220px; max-width: 320px; }
      :host ::ng-deep .filter { width: 180px; }
      :host ::ng-deep .filter.wide { width: 240px; }
      .card { background: #fff; border-radius: 14px; border: 1px solid #e5e9f2; padding: 1rem; }
      .loader { padding: 2rem; text-align: center; color: #64748b; }
      .cell-mono { font-family: ui-monospace, Menlo, monospace; font-weight: 600; color: #0f172a; }
      .name { font-weight: 600; color: #0f172a; }
      .muted { color: #94a3b8; font-size: 0.78rem; margin-top: 0.15rem; }
      .card-no { font-family: ui-monospace, Menlo, monospace; font-size: 0.78rem; color: #047857; font-weight: 600; }
      .new-form { display: flex; flex-direction: column; gap: 0.6rem; padding: 0.5rem 0; }
      .new-form label { font-size: 0.85rem; font-weight: 500; color: #334155; margin-top: 0.2rem; }
      .req { color: #dc2626; margin-left: 0.15rem; }
      .new-form input { width: 100%; }
      :host ::ng-deep .new-form .p-select { width: 100%; }
    `,
  ],
})
export class AssessmentsListPage {
  private api = inject(AssessmentsApi);
  private candidatesApi = inject(CandidatesApi);
  protected auth = inject(AuthService);
  private notify = inject(NotifyService);
  private router = inject(Router);

  protected loading = signal(true);
  protected firstLoad = signal(true);
  protected rows = signal<AssessmentListItem[]>([]);
  protected total = signal(0);
  protected page = signal(1);
  protected pageSize = signal(25);

  protected candidates = signal<CandidateListItem[]>([]);
  protected candidateOptions = computed(() =>
    this.candidates().map((c) => ({
      label: `${c.fullName} — ${c.identificationNumber} (${c.clientName})`,
      value: c.id,
    }))
  );

  protected searchInput = '';
  protected search$ = new Subject<string>();
  private searchSig = toSignal(this.search$.pipe(debounceTime(250), distinctUntilChanged()),
    { initialValue: '' });

  protected filterCategory: number | null = null;
  protected filterState: number | null = null;

  protected newDialog = false;
  protected creating = signal(false);
  protected newCandidateId: string | null = null;
  protected newCategory = 1;
  protected newAssessmentDate = new Date().toISOString().substring(0, 10);
  protected newLocation = '';

  protected canCreate = () => this.auth.hasAnyRole([
    Roles.Manager, Roles.Coordinator, Roles.Inspector, Roles.TechReviewer,
  ]);

  protected stateOptions = Object.entries(AssessmentStateName).map(([v, l]) => ({
    value: Number(v), label: l,
  }));
  protected categoryOptions = Object.entries(CompetencyCategoryLabel).map(([v, l]) => ({
    value: Number(v), label: l,
  }));

  protected stateName = (s: number) => AssessmentStateName[s] ?? 'Unknown';
  protected resultLabel = (r: number) => AssessmentResultLabel[r];
  protected categoryLabel = (c: number) => CompetencyCategoryLabel[c] ?? 'Unknown';

  protected canSubmitNew = () =>
    !!this.newCandidateId && !!this.newCategory && !!this.newAssessmentDate;

  constructor() {
    this.candidatesApi.list({ pageSize: 200 }).subscribe({
      next: (r) => this.candidates.set(r.items),
      error: (err) => showHttpError(this.notify, err),
    });
    this.refresh(1, this.pageSize(), '');
    let first = true;
    effect(() => {
      const s = this.searchSig();
      if (first) { first = false; return; }
      this.refresh(1, this.pageSize(), s);
    });
  }

  onFilterChange() { this.refresh(1, this.pageSize(), this.searchSig()); }
  onLazyLoad(e: any) {
    const page = Math.floor((e.first ?? 0) / (e.rows ?? this.pageSize())) + 1;
    this.refresh(page, e.rows ?? this.pageSize(), this.searchSig());
  }

  private refresh(page: number, pageSize: number, search: string) {
    this.loading.set(true);
    this.api.list({
      page, pageSize,
      search: search?.trim() || undefined,
      state: this.filterState ?? undefined,
      category: this.filterCategory ?? undefined,
    }).subscribe({
      next: (res) => {
        this.rows.set(res.items); this.total.set(res.total);
        this.page.set(res.page); this.pageSize.set(res.pageSize);
        this.loading.set(false);
        this.firstLoad.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.firstLoad.set(false);
        showHttpError(this.notify, err);
      },
    });
  }

  open(id: string) { this.router.navigate(['/assessments', id]); }

  closeNew() {
    this.newDialog = false;
    this.newCandidateId = null;
    this.newLocation = '';
  }

  createAssessment() {
    if (!this.canSubmitNew()) return;
    this.creating.set(true);
    this.api.create({
      candidateId: this.newCandidateId!,
      category: this.newCategory,
      assessmentDate: this.newAssessmentDate,
      location: this.newLocation || null,
    }).subscribe({
      next: (a) => {
        this.creating.set(false);
        this.notify.success(`Created ${a.assessmentNo}`);
        this.closeNew();
        this.router.navigate(['/assessments', a.id]);
      },
      error: (err) => { this.creating.set(false); showHttpError(this.notify, err); },
    });
  }
}
