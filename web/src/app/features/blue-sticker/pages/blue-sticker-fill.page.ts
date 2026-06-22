import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { BlueStickerApi } from '../../../core/api/blue-sticker.api';
import { ProfileApi } from '../../../core/api/profile.api';
import {
  BlueStickerReportDetail, BlueStickerResult, BlueStickerState, BlueStickerStateName,
} from '../../../core/models/blue-sticker.models';
import { NotifyService } from '../../../shared/services/notify.service';
import { showHttpError } from '../../../shared/services/api-error.handler';
import { AuthService } from '../../../core/auth/auth.service';
import { Roles } from '../../../core/models/auth.models';

@Component({
  standalone: true,
  imports: [CommonModule, DatePipe, FormsModule, RouterLink, ButtonModule, InputTextModule,
    TextareaModule, SelectModule, TagModule],
  template: `
    @if (r(); as rep) {
      <!-- ── SIGNATURE-MISSING BANNER (Inspector / Tech Reviewer) ─ -->
      @if (needsSignature(rep) && hasSignature() === false) {
        <div class="sig-warn">
          <i class="pi pi-exclamation-triangle"></i>
          <div>
            <strong>Your signature is missing.</strong>
            <p>You can save drafts, but you can't <strong>Submit</strong> or
              <strong>Approve</strong> a Blue Sticker report until you capture your signature
              once on your profile. The system applies it automatically from then on.</p>
            <a routerLink="/profile" class="sig-link">
              <i class="pi pi-pen-to-square"></i> Set my signature now
            </a>
          </div>
        </div>
      }

      <!-- ── HEADER ───────────────────────────────────────────── -->
      <section class="hdr">
        <div>
          <h2>{{ rep.reportNo }}</h2>
          <p-tag [value]="stateName(rep.state)" [severity]="stateTone(rep.state)" />
        </div>
        <dl class="kv">
          <div><dt>TUV Job Order No.</dt><dd>{{ rep.tuvJobOrderNo }}</dd></div>
          <div><dt>Aramco Category</dt><dd>{{ rep.aramcoCategoryNo || '—' }}</dd></div>
          <div><dt>Equipment ID No.</dt><dd>{{ rep.equipmentIdNo }}</dd></div>
          <div><dt>Inspection Checklist</dt><dd>{{ rep.inspectionChecklistNumber || '—' }}</dd></div>
          <div><dt>Created</dt><dd>{{ rep.createdAtUtc | date: 'dd MMM yyyy HH:mm' }}</dd></div>
        </dl>
      </section>

      <!-- ── ADMIN FIELDS (Coordinator/Manager; editable in Draft) ─ -->
      <section class="card">
        <h3>Administrative info</h3>
        @if (canEditAdmin()) {
          <div class="grid">
            <label>Org. Code
              <input pInputText [(ngModel)]="admin.orgCode" /></label>
            <label>RPO No.
              <input pInputText [(ngModel)]="admin.rpoNo" /></label>
            <label>CRM No.
              <input pInputText [(ngModel)]="admin.crmNo" /></label>
            <label>Department / Contractor
              <input pInputText [(ngModel)]="admin.departmentContractor" /></label>
            <label>Aramco Category No.
              <input pInputText [(ngModel)]="admin.aramcoCategoryNo" /></label>
            <label>Previous Sticker No.
              <input pInputText [(ngModel)]="admin.previousStickerNo" /></label>
            <label class="col-span-2">Previous Sticker Issued By
              <input pInputText [(ngModel)]="admin.previousStickerIssuedBy" /></label>
          </div>
          <div class="actions">
            <p-button label="Save admin info" icon="pi pi-save"
              (onClick)="saveAdmin()" [loading]="busy()" />
          </div>
        } @else {
          <dl class="kv-grid">
            <div><dt>Org. Code</dt><dd>{{ rep.orgCode || '—' }}</dd></div>
            <div><dt>RPO No.</dt><dd>{{ rep.rpoNo || '—' }}</dd></div>
            <div><dt>CRM No.</dt><dd>{{ rep.crmNo || '—' }}</dd></div>
            <div><dt>Department / Contractor</dt><dd>{{ rep.departmentContractor || '—' }}</dd></div>
            <div><dt>Previous Sticker No.</dt><dd>{{ rep.previousStickerNo || '—' }}</dd></div>
            <div><dt>Previous Sticker Issued By</dt><dd>{{ rep.previousStickerIssuedBy || '—' }}</dd></div>
          </dl>
        }
      </section>

      <!-- ── EQUIPMENT (editable by Inspector in InProgress) ──── -->
      <section class="card">
        <header class="card-hdr">
          <h3>Equipment</h3>
          @if (rep.state === S.InProgress) {
            <span class="hint">Confirm or correct what you see on site</span>
          }
        </header>
        @if (rep.state === S.InProgress) {
          <div class="grid">
            <label>Aramco Category
              <input pInputText [(ngModel)]="form.aramcoCategoryNo"
                placeholder="e.g. CR10" /></label>
            <label>Equipment Type
              <input pInputText [(ngModel)]="form.equipmentType"
                placeholder="e.g. Manbasket" /></label>
            <label>Manufacturer
              <input pInputText [(ngModel)]="form.manufacturer" /></label>
            <label>Model
              <input pInputText [(ngModel)]="form.model" /></label>
            <label>Serial No.
              <input pInputText [(ngModel)]="form.equipmentSerialNo" /></label>
            <label>Capacity / SWL
              <input pInputText [(ngModel)]="form.capacity"
                placeholder="e.g. 5 t" /></label>
          </div>
        } @else {
          <dl class="kv-grid">
            <div><dt>Aramco Category</dt><dd>{{ rep.aramcoCategoryNo || '—' }}</dd></div>
            <div><dt>Equipment Type</dt><dd>{{ rep.equipmentType || '—' }}</dd></div>
            <div><dt>Manufacturer</dt><dd>{{ rep.manufacturer || '—' }}</dd></div>
            <div><dt>Model</dt><dd>{{ rep.model || '—' }}</dd></div>
            <div><dt>Serial No.</dt><dd>{{ rep.equipmentSerialNo || '—' }}</dd></div>
            <div><dt>Capacity</dt><dd>{{ rep.capacity || '—' }}</dd></div>
            <div><dt>Location</dt><dd>{{ rep.equipmentLocation || '—' }}</dd></div>
          </dl>
        }
      </section>

      <!-- ── DRAFT: start inspection ──────────────────────────── -->
      @if (rep.state === S.Draft) {
        <section class="card">
          <p>Press <strong>Start inspection</strong> to lock in the inspection date/time and proceed to data entry.</p>
          <p-button label="Start inspection" icon="pi pi-play"
            (onClick)="fire('StartInspection')" [loading]="busy()" />
        </section>
      }

      <!-- ── IN PROGRESS: inspector data entry ────────────────── -->
      @if (rep.state === S.InProgress) {
        <section class="card">
          <h3>Inspection</h3>
          <dl class="kv-grid muted">
            <div><dt>Inspection Date</dt><dd>{{ rep.inspectionDate || '—' }}</dd></div>
            <div><dt>Inspection Time</dt><dd>{{ rep.inspectionTime || '—' }}</dd></div>
          </dl>
          <div class="grid">
            <label class="col-span-2">Area of Inspection<span class="req">*</span>
              <input pInputText [(ngModel)]="form.areaOfInspection" /></label>
            <label>Inspection Result<span class="req">*</span>
              <p-select [options]="resultOptions" optionLabel="label" optionValue="value"
                [(ngModel)]="form.result" appendTo="body" /></label>
            <label>Equipment Location (override)
              <input pInputText [(ngModel)]="form.equipmentLocation" /></label>
            <label class="col-span-2">Deficiencies / Observations
              <textarea pTextarea [(ngModel)]="form.deficiencies" rows="3"
                placeholder="One defect per line"></textarea></label>
            <label class="col-span-2">Corrective Action Taken
              <textarea pTextarea [(ngModel)]="form.correctiveActionsTaken" rows="3"
                placeholder="Repair done for each defect"></textarea></label>
          </div>

          <h4>Receiver (site representative)</h4>
          <div class="grid">
            <label>Name<span class="req">*</span>
              <input pInputText [(ngModel)]="form.receiverName" /></label>
            <label>Badge No.<span class="req">*</span>
              <input pInputText [(ngModel)]="form.receiverBadgeNo" /></label>
            <label>Telephone
              <input pInputText [(ngModel)]="form.receiverTelephone" /></label>
          </div>

          <h4>Inspector</h4>
          <div class="grid">
            <label>Telephone
              <input pInputText [(ngModel)]="form.inspectorTelephone" /></label>
          </div>

          @if (missing().length > 0) {
            <div class="missing">
              <i class="pi pi-exclamation-triangle"></i>
              <div>
                <strong>Cannot submit yet — fill in:</strong>
                <ul>
                  @for (m of missing(); track m) { <li>{{ m }}</li> }
                </ul>
              </div>
            </div>
          }

          <div class="actions">
            <p-button label="Save draft" icon="pi pi-save" [outlined]="true"
              (onClick)="save()" [loading]="busy()" />
            <p-button label="Submit to technical reviewer" icon="pi pi-send"
              [loading]="busy()"
              [disabled]="missing().length > 0 || hasSignature() === false"
              (onClick)="submit()" />
          </div>
          @if (hasSignature() === false) {
            <p class="hint warn">Signature missing — set it on
              <a routerLink="/profile">your profile</a> to enable Submit.</p>
          } @else {
            <p class="hint">Your stored signature will be applied automatically.</p>
          }
        </section>
      }

      <!-- ── INSPECTION RECORD (visible from UnderReview onwards) ─ -->
      @if (rep.state >= S.UnderReview && rep.state !== S.Rejected) {
        <section class="card">
          <h3>Inspection record</h3>
          <dl class="kv-grid">
            <div><dt>Inspection Date</dt><dd>{{ rep.inspectionDate || '—' }}</dd></div>
            <div><dt>Inspection Time</dt><dd>{{ rep.inspectionTime || '—' }}</dd></div>
            <div><dt>Area of Inspection</dt><dd>{{ rep.areaOfInspection || '—' }}</dd></div>
            <div><dt>Result</dt><dd>{{ resultName(rep.result) }}</dd></div>
            <div class="col-span-2"><dt>Deficiencies / Observations</dt>
              <dd><pre>{{ rep.deficiencies || '—' }}</pre></dd></div>
            <div class="col-span-2"><dt>Corrective Action Taken</dt>
              <dd><pre>{{ rep.correctiveActionsTaken || '—' }}</pre></dd></div>
          </dl>

          <h4>Receiver</h4>
          <dl class="kv-grid">
            <div><dt>Name</dt><dd>{{ rep.receiverName || '—' }}</dd></div>
            <div><dt>Badge No.</dt><dd>{{ rep.receiverBadgeNo || '—' }}</dd></div>
            <div><dt>Telephone</dt><dd>{{ rep.receiverTelephone || '—' }}</dd></div>
          </dl>

          <h4>Inspector</h4>
          <dl class="kv-grid">
            <div><dt>Name</dt><dd>{{ rep.inspectorName || '—' }}</dd></div>
            <div><dt>SAP No.</dt><dd>{{ rep.inspectorSapNo || '—' }}</dd></div>
            <div><dt>Telephone</dt><dd>{{ rep.inspectorTelephone || '—' }}</dd></div>
          </dl>

          @if (rep.state === S.UnderReview) {
            @if (auth.hasAnyRole([Roles.TechReviewer, Roles.Manager])) {
              <div class="actions">
                <p-button label="Approve" icon="pi pi-check" severity="success"
                  [loading]="busy()"
                  [disabled]="hasSignature() === false"
                  (onClick)="approve()" />
                <p-button label="Reject" icon="pi pi-times" severity="danger" [text]="true"
                  [loading]="busy()" (onClick)="fire('Reject')" />
              </div>
              @if (hasSignature() === false) {
                <p class="hint warn">Signature missing — set it on
                  <a routerLink="/profile">your profile</a> to enable Approve.</p>
              } @else {
                <p class="hint">Your stored signature will be applied automatically.</p>
              }
            } @else {
              <p class="hint">Submitted — awaiting technical reviewer.</p>
            }
          }
        </section>
      }

      <!-- ── APPROVED+ ────────────────────────────────────────── -->
      @if (rep.state >= S.Approved && rep.state !== S.Rejected) {
        <section class="card">
          <header class="card-hdr">
            <h3>Approval</h3>
            @if (rep.state === S.Approved || rep.state === S.AwaitingClientSignature) {
              <p-tag value="Pending Receiver signature" severity="warn"
                icon="pi pi-exclamation-triangle" />
            } @else if (rep.state === S.ClientSigned) {
              <p-tag value="All signatures captured" severity="success"
                icon="pi pi-check-circle" />
            }
          </header>
          <dl class="kv-grid">
            <div><dt>New Sticker No.</dt><dd>{{ rep.newStickerNo || '—' }}</dd></div>
            <div><dt>Sticker Expiration</dt><dd>{{ rep.stickerExpirationDate || '—' }}</dd></div>
            <div><dt>Technical Reviewer</dt><dd>{{ rep.technicalReviewerName || '—' }}</dd></div>
            <div><dt>Reviewed Date</dt><dd>{{ rep.reviewedDate || '—' }}</dd></div>
            <div><dt>Received Date</dt><dd>{{ rep.receivedDate || '—' }}</dd></div>
          </dl>

          @if (rep.state === S.Approved || rep.state === S.AwaitingClientSignature) {
            <div class="receiver-needed">
              <i class="pi pi-info-circle"></i>
              <div>
                <strong>The Receiver must sign before this certificate can close.</strong>
                <p>Hand the tablet to the site representative — request the OTP that was emailed
                  to the client, then capture their signature. The final Annex 1 PDF only
                  becomes available after the Receiver signs.</p>
              </div>
            </div>
          }

          <div class="actions">
            @if (rep.state === S.Approved || rep.state === S.AwaitingClientSignature) {
              <p-button label="Go to client signing" icon="pi pi-pencil"
                severity="warn" (onClick)="goFinalize()" />
              <p-button label="Preview draft PDF" icon="pi pi-file-pdf"
                [outlined]="true" (onClick)="download(true)" />
            }
            @if (rep.state === S.ClientSigned) {
              <p-button label="Download Annex 1 PDF" icon="pi pi-file-pdf"
                (onClick)="download(false)" />
            }
          </div>
        </section>
      }

      <!-- ── SIGNATURES (any captured) ────────────────────────── -->
      @if (rep.receiverSignaturePng || rep.inspectorSignaturePng
           || rep.technicalReviewerSignaturePng) {
        <section class="card">
          <h3>Signatures</h3>
          <div class="sigs">
            <div class="sig" [class.sig-required]="!rep.receiverSignaturePng">
              <div class="sig-label">
                Receiver
                @if (!rep.receiverSignaturePng) { <span class="req-badge">REQUIRED</span> }
              </div>
              <div class="sig-box">
                @if (rep.receiverSignaturePng) {
                  <img [src]="rep.receiverSignaturePng" alt="Receiver signature" />
                } @else {
                  <span class="sig-pending">
                    <i class="pi pi-exclamation-triangle"></i>
                    Receiver must sign before this certificate can close
                  </span>
                }
              </div>
              <dl class="sig-meta">
                <div><dt>Name</dt><dd>{{ rep.receiverName || '—' }}</dd></div>
                <div><dt>Badge No.</dt><dd>{{ rep.receiverBadgeNo || '—' }}</dd></div>
                <div><dt>Telephone</dt><dd>{{ rep.receiverTelephone || '—' }}</dd></div>
                <div><dt>Received date</dt><dd>{{ rep.receivedDate || '—' }}</dd></div>
              </dl>
              @if (!rep.receiverSignaturePng &&
                   (rep.state === S.Approved || rep.state === S.AwaitingClientSignature)) {
                <p-button label="Capture receiver signature" icon="pi pi-pencil" size="small"
                  severity="warn" (onClick)="goFinalize()" />
              }
            </div>
            <div class="sig">
              <div class="sig-label">Inspector</div>
              <div class="sig-box">
                @if (rep.inspectorSignaturePng) {
                  <img [src]="rep.inspectorSignaturePng" alt="Inspector signature" />
                } @else { <span class="sig-pending">Not signed yet</span> }
              </div>
              <dl class="sig-meta">
                <div><dt>Name</dt><dd>{{ rep.inspectorName || '—' }}</dd></div>
                <div><dt>SAP No.</dt><dd>{{ rep.inspectorSapNo || '—' }}</dd></div>
                <div><dt>Telephone</dt><dd>{{ rep.inspectorTelephone || '—' }}</dd></div>
              </dl>
            </div>
            <div class="sig">
              <div class="sig-label">Technical Reviewer</div>
              <div class="sig-box">
                @if (rep.technicalReviewerSignaturePng) {
                  <img [src]="rep.technicalReviewerSignaturePng" alt="Tech reviewer signature" />
                } @else { <span class="sig-pending">Not signed yet</span> }
              </div>
              <dl class="sig-meta">
                <div><dt>Name</dt><dd>{{ rep.technicalReviewerName || '—' }}</dd></div>
                <div><dt>Reviewed date</dt><dd>{{ rep.reviewedDate || '—' }}</dd></div>
              </dl>
            </div>
          </div>
        </section>
      }
    } @else { <p class="loader">Loading…</p> }
  `,
  styles: [`
    :host{display:block;max-width:1000px}
    .hdr{display:flex;justify-content:space-between;align-items:flex-start;
      gap:1rem;padding:1rem;background:#fff;border:1px solid #e5e9f2;border-radius:14px;margin-bottom:1rem}
    .hdr h2{margin:0 0 .25rem 0;font-size:1.5rem}
    .card{background:#fff;border:1px solid #e5e9f2;border-radius:14px;
      padding:1rem 1.25rem;margin-bottom:1rem}
    .card h3{margin:0 0 .8rem 0;font-size:1.05rem;color:#0f172a}
    .card h4{margin:1rem 0 .5rem 0;font-size:.95rem;color:#334155}
    .grid{display:grid;grid-template-columns:1fr 1fr;gap:.6rem 1rem}
    .col-span-2{grid-column:1/-1}
    .grid label{display:flex;flex-direction:column;gap:.25rem;font-size:.85rem;color:#475569}
    .grid input,.grid textarea{font-size:.95rem}
    .kv-grid{display:grid;grid-template-columns:1fr 1fr;gap:.5rem 1.5rem;margin:0}
    .kv-grid.muted{background:#f8fafc;padding:.5rem .75rem;border-radius:8px;margin-bottom:.6rem}
    .kv-grid > div{display:flex;flex-direction:column}
    .kv-grid dt{font-size:.75rem;color:#64748b;text-transform:uppercase;letter-spacing:.04em}
    .kv-grid dd{margin:0;font-size:.95rem;color:#0f172a}
    .kv-grid pre{margin:0;font-family:inherit;white-space:pre-wrap;font-size:.95rem}
    .kv{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:.5rem 1rem;flex:1;max-width:680px}
    .kv > div{display:flex;flex-direction:column}
    .kv dt{font-size:.7rem;color:#64748b;text-transform:uppercase;letter-spacing:.04em}
    .kv dd{margin:0;font-size:.9rem;color:#0f172a}
    .actions{display:flex;gap:.5rem;margin-top:.8rem}
    .hint{color:#64748b;font-size:.85rem;margin:.25rem 0 .5rem}
    .loader{padding:2rem;text-align:center;color:#64748b}
    .req{color:#dc2626;margin-left:.15rem}
    .missing{margin-top:1rem;padding:.75rem 1rem;border-radius:10px;
      background:#fef3c7;border:1px solid #fcd34d;
      display:flex;gap:.6rem;align-items:flex-start;color:#92400e;font-size:.9rem}
    .missing .pi{font-size:1.1rem;margin-top:.1rem}
    .missing ul{margin:.3rem 0 0 1.2rem;padding:0}
    .missing li{margin:.1rem 0}
    .sig-warn{margin-bottom:1rem;padding:1rem 1.2rem;border-radius:12px;
      background:#fef2f2;border:2px solid #fca5a5;color:#7f1d1d;
      display:flex;gap:.8rem;align-items:flex-start}
    .sig-warn > .pi{font-size:1.4rem;color:#dc2626;margin-top:.1rem}
    .sig-warn strong{font-size:1rem}
    .sig-warn p{margin:.35rem 0 .6rem;font-size:.9rem;line-height:1.4}
    .sig-link{display:inline-flex;align-items:center;gap:.4rem;
      padding:.4rem .8rem;border-radius:8px;background:#dc2626;color:#fff;
      text-decoration:none;font-weight:600;font-size:.88rem}
    .sig-link:hover{background:#b91c1c}
    .hint.warn{color:#b45309}
    .sigs{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:1rem}
    .sig{display:flex;flex-direction:column;gap:.4rem}
    .sig-label{font-size:.78rem;color:#64748b;text-transform:uppercase;letter-spacing:.05em;
      font-weight:600;display:flex;align-items:center;gap:.4rem}
    .req-badge{font-size:.6rem;padding:.05rem .35rem;border-radius:999px;
      background:#dc2626;color:#fff;letter-spacing:.05em}
    .sig-box{height:120px;border:1px solid #cbd5e1;border-radius:8px;
      display:flex;align-items:center;justify-content:center;background:#f8fafc;overflow:hidden}
    .sig-box img{max-height:100%;max-width:100%;object-fit:contain}
    .sig-pending{color:#94a3b8;font-style:italic;font-size:.85rem;text-align:center;padding:0 .5rem;
      display:flex;flex-direction:column;align-items:center;gap:.3rem}
    .sig-pending .pi{font-size:1.4rem;color:#f59e0b;font-style:normal}
    .sig-required .sig-box{border-color:#fcd34d;background:#fffbeb}
    .sig-required .sig-pending{color:#92400e}
    .sig-meta{display:grid;grid-template-columns:1fr 1fr;gap:.2rem .8rem;margin:0;font-size:.8rem}
    .sig-meta dt{color:#64748b;font-size:.7rem}
    .sig-meta dd{margin:0;color:#0f172a}
    .receiver-needed{margin:1rem 0;padding:.75rem 1rem;border-radius:10px;
      background:#fffbeb;border:1px solid #fcd34d;color:#92400e;font-size:.9rem;
      display:flex;gap:.6rem;align-items:flex-start}
    .receiver-needed > .pi{font-size:1.1rem;margin-top:.1rem;color:#d97706}
    .receiver-needed p{margin:.3rem 0 0;line-height:1.4}
    .card-hdr{display:flex;justify-content:space-between;align-items:center;
      gap:.8rem;margin-bottom:.6rem}
    .card-hdr h3{margin:0}
    @media (max-width:760px){.sigs{grid-template-columns:1fr}}
  `],
})
export class BlueStickerFillPage {
  private api = inject(BlueStickerApi);
  private profileApi = inject(ProfileApi);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private notify = inject(NotifyService);
  protected auth = inject(AuthService);
  protected hasSignature = signal<boolean | null>(null);   // null = loading

  protected S = BlueStickerState;
  protected Roles = Roles;
  protected r = signal<BlueStickerReportDetail | null>(null);
  protected busy = signal(false);
  private id = this.route.snapshot.paramMap.get('id')!;

  protected resultOptions = [
    { label: 'Pass', value: BlueStickerResult.Pass },
    { label: 'Fail', value: BlueStickerResult.Fail },
  ];
  protected stateName = (s: number) => BlueStickerStateName[s];
  protected resultName = (r: number) =>
    r === BlueStickerResult.Pass ? 'Pass' : r === BlueStickerResult.Fail ? 'Fail' : '—';
  protected stateTone = (s: number) =>
    s === BlueStickerState.Approved || s === BlueStickerState.ClientSigned ? 'success'
    : s === BlueStickerState.Rejected || s === BlueStickerState.Voided ? 'danger'
    : s === BlueStickerState.UnderReview || s === BlueStickerState.AwaitingClientSignature ? 'warn'
    : 'info';

  protected canEditAdmin = computed(() =>
    this.r()?.state === BlueStickerState.Draft &&
    this.auth.hasAnyRole([Roles.Coordinator, Roles.Manager]));

  protected admin: any = {};
  protected form: any = { result: BlueStickerResult.Pass };
  /** Live list of required-but-empty fields. Mirrors the backend submit gate exactly so the
   *  Submit button is only enabled when the server would accept it. Plain method so it re-runs
   *  with each change-detection cycle — [(ngModel)] mutations don't trip Signals. */
  protected missing = (): string[] => {
    const f = this.form;
    const m: string[] = [];
    if (!f.areaOfInspection?.trim()) m.push('Area of Inspection');
    if (f.result == null || f.result === BlueStickerResult.NotSet) m.push('Inspection Result');
    if (!f.receiverName?.trim()) m.push('Receiver Name');
    if (!f.receiverBadgeNo?.trim()) m.push('Receiver Badge No.');
    return m;
  };

  /** Does the user need to have a signature on file for this report's current state? Yes when
   *  Inspector is in InProgress (about to submit) or Tech Reviewer is in UnderReview. */
  protected needsSignature(rep: BlueStickerReportDetail): boolean {
    if (rep.state === BlueStickerState.InProgress) return true;
    if (rep.state === BlueStickerState.UnderReview &&
        this.auth.hasAnyRole([Roles.TechReviewer, Roles.Manager])) return true;
    return false;
  }

  constructor() {
    this.load();
    // Inspector / Tech Reviewer need a signature on file before they can submit / approve.
    // Fetch the current profile once on load so we can warn upfront rather than 409 at submit.
    this.profileApi.me().subscribe({
      next: (p) => this.hasSignature.set(!!p.signaturePng),
      error: () => this.hasSignature.set(false),
    });
  }

  private load() {
    this.api.get(this.id).subscribe({
      next: (rep) => {
        this.r.set(rep);
        this.admin = {
          orgCode: rep.orgCode ?? '',
          rpoNo: rep.rpoNo ?? '',
          crmNo: rep.crmNo ?? '',
          departmentContractor: rep.departmentContractor ?? '',
          aramcoCategoryNo: rep.aramcoCategoryNo ?? '',
          previousStickerNo: rep.previousStickerNo ?? '',
          previousStickerIssuedBy: rep.previousStickerIssuedBy ?? '',
        };
        this.form = {
          areaOfInspection: rep.areaOfInspection ?? '',
          result: rep.result || BlueStickerResult.Pass,
          deficiencies: rep.deficiencies ?? '',
          correctiveActionsTaken: rep.correctiveActionsTaken ?? '',
          equipmentLocation: rep.equipmentLocation ?? '',
          receiverName: rep.receiverName ?? '',
          receiverBadgeNo: rep.receiverBadgeNo ?? '',
          receiverTelephone: rep.receiverTelephone ?? '',
          inspectorTelephone: rep.inspectorTelephone ?? '',
          aramcoCategoryNo: rep.aramcoCategoryNo ?? '',
          manufacturer: rep.manufacturer ?? '',
          model: rep.model ?? '',
          equipmentType: rep.equipmentType ?? '',
          equipmentSerialNo: rep.equipmentSerialNo ?? '',
          capacity: rep.capacity ?? '',
        };
      },
      error: (e) => showHttpError(this.notify, e),
    });
  }

  saveAdmin() {
    this.busy.set(true);
    this.api.updateAdmin(this.id, this.admin).subscribe({
      next: (rep) => { this.r.set(rep); this.busy.set(false);
        this.notify.success('Administrative info saved'); },
      error: (e) => { this.busy.set(false); showHttpError(this.notify, e); },
    });
  }

  fire(trigger: any) {
    this.busy.set(true);
    this.api.transition(this.id, trigger).subscribe({
      next: (rep) => { this.r.set(rep); this.busy.set(false); },
      error: (e) => { this.busy.set(false); showHttpError(this.notify, e); },
    });
  }

  save() {
    this.busy.set(true);
    this.api.updateInspection(this.id, this.form).subscribe({
      next: (rep) => { this.r.set(rep); this.busy.set(false);
        this.notify.success('Saved'); },
      error: (e) => { this.busy.set(false); showHttpError(this.notify, e); },
    });
  }

  submit() {
    this.busy.set(true);
    this.api.updateInspection(this.id, this.form).subscribe({
      next: () => this.api.transition(this.id, 'SubmitForReview').subscribe({
        next: (rep) => { this.r.set(rep); this.busy.set(false);
          this.notify.success('Submitted'); },
        error: (e) => { this.busy.set(false); showHttpError(this.notify, e); },
      }),
      error: (e) => { this.busy.set(false); showHttpError(this.notify, e); },
    });
  }

  goFinalize() { this.router.navigate(['/blue-sticker', this.id, 'finalize']); }

  download(draft = false) {
    this.api.pdf(this.id, draft).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        window.open(url, '_blank');
        setTimeout(() => window.URL.revokeObjectURL(url), 60_000);
      },
      error: (e) => showHttpError(this.notify, e),
    });
  }

  approve() {
    this.busy.set(true);
    this.api.transition(this.id, 'Approve').subscribe({
      next: (rep) => { this.r.set(rep); this.busy.set(false);
        this.notify.success('Approved — sticker issued'); },
      error: (e) => { this.busy.set(false); showHttpError(this.notify, e); },
    });
  }
}
