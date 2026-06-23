import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SignaturePad } from './signature-pad.component';

export interface SignatureEntry {
  role: string;
  name: string;
  dataUrl: string;
  atUtc: string;
}

const DEFAULT_ROLES = ['Inspector', 'Reviewed by (TÜV)', 'Receiver (Client)'];

@Component({
  selector: 'tuv-signatures-panel',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonModule, DialogModule, InputTextModule, SignaturePad],
  template: `
    <div class="bar">
      <div class="count">{{ signatures().length }} signature{{ signatures().length === 1 ? '' : 's' }} captured</div>
    </div>

    <div class="grid">
      @for (role of visibleRoles(); track role) {
        <div class="slot" [class.signed]="byRole(role)">
          <div class="head">
            <span class="role">{{ role }}</span>
            @if (byRole(role); as sig) {
              <span class="meta">{{ sig.name || 'Signed' }} · {{ sig.atUtc | date: 'dd MMM HH:mm' }}</span>
            } @else {
              <span class="meta">Pending</span>
            }
          </div>
          <div class="preview">
            @if (byRole(role); as sig) {
              <img [src]="sig.dataUrl" [alt]="role" />
            } @else {
              <div class="placeholder">
                <i class="pi pi-pencil"></i>
                <span>Tap to capture</span>
              </div>
            }
          </div>
          @if (!readonly) {
            <div class="actions">
              <p-button [label]="byRole(role) ? 'Replace' : 'Capture'"
                icon="pi pi-pencil" size="small"
                [outlined]="!!byRole(role)"
                (onClick)="openFor(role)" />
              @if (byRole(role)) {
                <p-button label="Remove" icon="pi pi-trash" severity="danger" [text]="true" size="small"
                  (onClick)="remove(role)" />
              }
            </div>
          }
        </div>
      }
    </div>

    <p-dialog [(visible)]="dialogOpen" header="Capture signature" [modal]="true"
      [style]="{ width: '660px' }" [draggable]="false" [resizable]="false">
      @if (capturingRole()) {
        <div class="capture">
          <div class="role-label">Signing as <strong>{{ capturingRole() }}</strong></div>
          <label>Signatory name (optional)
            <input pInputText [(ngModel)]="capturingName" placeholder="e.g. Ahmed Al-Saud" />
          </label>
          <tuv-signature-pad (commitSignature)="onCommit($event)" />
        </div>
      }
    </p-dialog>
  `,
  styles: [
    `
      :host { display: block; }
      .bar { display: flex; justify-content: flex-end; margin-bottom: 0.6rem; }
      .count { font-size: 0.78rem; color: #64748b; font-weight: 500; }

      .grid {
        display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 0.8rem;
      }
      .slot {
        display: flex; flex-direction: column; gap: 0.5rem;
        border: 1px solid #e5e9f2; border-radius: 12px;
        padding: 0.7rem 0.8rem; background: #fff;
      }
      .slot.signed { border-color: #6ee7b7; box-shadow: 0 0 0 3px rgba(110, 231, 183, 0.18); }
      .head { display: flex; flex-direction: column; gap: 0.1rem; }
      .role { font-weight: 600; color: #0f172a; font-size: 0.85rem; }
      .meta { font-size: 0.72rem; color: #64748b; }
      .preview {
        height: 90px; border-radius: 8px; background: #f8fafc;
        display: flex; align-items: center; justify-content: center;
        overflow: hidden; border: 1px dashed #e5e9f2;
      }
      .preview img { max-width: 100%; max-height: 100%; }
      .placeholder { color: #94a3b8; font-size: 0.78rem; display: flex; flex-direction: column; align-items: center; gap: 0.25rem; }
      .placeholder .pi { font-size: 1.2rem; }
      .actions { display: flex; justify-content: space-between; gap: 0.4rem; }

      .capture { display: flex; flex-direction: column; gap: 0.7rem; }
      .role-label { font-size: 0.85rem; color: #475569; }
      label { display: flex; flex-direction: column; gap: 0.25rem; font-size: 0.78rem; color: #475569; }
    `,
  ],
})
export class SignaturesPanel implements OnChanges {
  @Input() value: string | null = null;
  @Input() readonly = false;
  @Input() roles: string[] = DEFAULT_ROLES;
  /** Roles to hide from the panel — e.g. the inspector slot until approval (comment #8). */
  @Input() set hiddenRoles(v: string[]) { this._hidden.set(v ?? []); }
  @Output() valueChange = new EventEmitter<string>();

  private _hidden = signal<string[]>([]);
  protected visibleRoles = computed(() => this.roles.filter(r => !this._hidden().includes(r)));

  protected signatures = signal<SignatureEntry[]>([]);
  protected dialogOpen = false;
  protected capturingRole = signal<string | null>(null);
  protected capturingName = '';

  ngOnChanges(c: SimpleChanges) { if (c['value']) this.parse(this.value); }

  byRole(role: string): SignatureEntry | undefined {
    return this.signatures().find((s) => s.role === role);
  }

  openFor(role: string) {
    this.capturingRole.set(role);
    this.capturingName = this.byRole(role)?.name ?? '';
    this.dialogOpen = true;
  }

  onCommit(dataUrl: string) {
    const role = this.capturingRole();
    if (!role) return;
    const entry: SignatureEntry = {
      role,
      name: this.capturingName.trim(),
      dataUrl,
      atUtc: new Date().toISOString(),
    };
    const next = [...this.signatures().filter((s) => s.role !== role), entry];
    this.signatures.set(next);
    this.dialogOpen = false;
    this.capturingRole.set(null);
    this.emit();
  }

  remove(role: string) {
    this.signatures.set(this.signatures().filter((s) => s.role !== role));
    this.emit();
  }

  private parse(json: string | null) {
    if (!json) { this.signatures.set([]); return; }
    try {
      const arr = JSON.parse(json);
      this.signatures.set(Array.isArray(arr) ? arr : []);
    } catch { this.signatures.set([]); }
  }

  private emit() { this.valueChange.emit(JSON.stringify(this.signatures())); }
}
