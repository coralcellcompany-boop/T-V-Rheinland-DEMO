import { CommonModule } from '@angular/common';
import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { MessageService, ConfirmationService } from 'primeng/api';
import { Sidenav } from './sidenav.component';
import { Topbar } from './topbar.component';

@Component({
  selector: 'tuv-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet, Sidenav, Topbar, ToastModule, ConfirmDialogModule],
  providers: [MessageService, ConfirmationService],
  template: `
    <div class="shell" [class.collapsed]="collapsed()">
      <tuv-sidenav [collapsed]="collapsed()" (toggle)="toggle()" />

      <div class="main">
        <tuv-topbar (menu)="toggle()" />
        <main class="content">
          <router-outlet />
        </main>
      </div>

      <p-toast position="top-right" />
      <p-confirmDialog />
    </div>
  `,
  styles: [
    `
      :host { display: block; height: 100vh; }
      .shell {
        display: grid;
        grid-template-columns: 240px 1fr;
        height: 100vh;
        transition: grid-template-columns 0.18s ease;
      }
      .shell.collapsed { grid-template-columns: 64px 1fr; }
      .main { display: flex; flex-direction: column; min-width: 0; }
      .content {
        flex: 1;
        padding: 1.5rem;
        background: #f4f6fb;
        overflow-y: auto;
      }
    `,
  ],
})
export class ShellComponent {
  protected collapsed = signal(false);
  protected toggle = () => this.collapsed.update((v) => !v);
}
