import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Output, computed, inject, input } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TooltipModule } from 'primeng/tooltip';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../auth/auth.service';
import { NavItem, pickPrimaryNav, pickSecondaryNav } from './sidenav.config';

@Component({
  selector: 'tuv-sidenav',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, TooltipModule, TranslateModule],
  template: `
    <aside class="sidenav" [class.collapsed]="collapsed()">
      <div class="brand">
        <div class="logo">T<span>R</span></div>
        <div class="name" *ngIf="!collapsed()">
          <strong>{{ 'app.brand' | translate }}</strong>
          <span>{{ 'app.subtitle' | translate }}</span>
        </div>
      </div>

      <nav class="nav">
        <span class="group" *ngIf="!collapsed()">{{ 'nav.workspace' | translate }}</span>
        <a *ngFor="let item of primary()"
           [routerLink]="item.route"
           routerLinkActive="active"
           [pTooltip]="collapsed() ? (item.label | translate) : ''"
           tooltipPosition="right">
          <i class="pi" [ngClass]="item.icon"></i>
          <span class="label">{{ item.label | translate }}</span>
        </a>

        <span class="group" *ngIf="secondary().length && !collapsed()">{{ 'nav.administration' | translate }}</span>
        <a *ngFor="let item of secondary()"
           [routerLink]="item.route"
           routerLinkActive="active"
           [pTooltip]="collapsed() ? (item.label | translate) : ''"
           tooltipPosition="right">
          <i class="pi" [ngClass]="item.icon"></i>
          <span class="label">{{ item.label | translate }}</span>
        </a>
      </nav>

      <div class="footer">
        <button type="button" class="collapse" (click)="toggle.emit()"
                [attr.aria-label]="collapsed() ? 'Expand' : 'Collapse'">
          <i class="pi" [ngClass]="collapsed() ? 'pi-angle-right' : 'pi-angle-left'"></i>
        </button>
      </div>
    </aside>
  `,
  styles: [
    `
      :host { display: block; height: 100%; }
      .sidenav {
        height: 100%;
        background: linear-gradient(180deg, #0a3d62 0%, #06283d 100%);
        color: #d6e4f0;
        width: 240px;
        display: flex; flex-direction: column;
        transition: width 0.18s ease;
      }
      .sidenav.collapsed { width: 64px; }

      .brand { display: flex; align-items: center; gap: 0.6rem; padding: 1rem; border-bottom: 1px solid rgba(255,255,255,0.07); }
      .logo {
        width: 38px; height: 38px; border-radius: 10px;
        background: linear-gradient(135deg, #f97316, #ea580c);
        color: #fff; font-weight: 800;
        display: flex; align-items: center; justify-content: center;
        font-size: 1.05rem; letter-spacing: 0.04em;
        box-shadow: 0 4px 14px -2px rgba(249, 115, 22, 0.4);
      }
      .logo span { color: #fde68a; margin-left: 1px; }
      .name { display: flex; flex-direction: column; line-height: 1.1; }
      .name strong { font-size: 0.92rem; color: #fff; }
      .name span { font-size: 0.72rem; color: #93c5fd; opacity: 0.85; }

      .nav { flex: 1; padding: 0.5rem 0.4rem; overflow-y: auto; }
      .group {
        display: block; padding: 0.85rem 0.6rem 0.4rem; font-size: 0.7rem;
        text-transform: uppercase; letter-spacing: 0.08em; color: #94a3b8;
      }
      .nav a {
        display: flex; align-items: center; gap: 0.7rem;
        padding: 0.55rem 0.65rem; border-radius: 8px;
        color: #d6e4f0; text-decoration: none; font-size: 0.9rem;
        transition: background 0.12s ease, color 0.12s ease, transform 0.12s ease;
      }
      .nav a:hover { background: rgba(255,255,255,0.06); color: #fff; transform: translateX(1px); }
      .nav a.active {
        background: rgba(249, 115, 22, 0.18);
        color: #fff;
        box-shadow: inset 3px 0 0 #f97316;
      }
      .nav a .pi { font-size: 1rem; min-width: 1rem; text-align: center; }

      .sidenav.collapsed .label { display: none; }
      .sidenav.collapsed .nav a { justify-content: center; }
      .sidenav.collapsed .group { display: none; }

      .footer { padding: 0.5rem; border-top: 1px solid rgba(255,255,255,0.07); display: flex; justify-content: flex-end; }
      .collapse {
        background: transparent; color: #93c5fd; border: 1px solid rgba(255,255,255,0.1);
        border-radius: 8px; padding: 0.3rem 0.55rem; cursor: pointer;
      }
      .collapse:hover { background: rgba(255,255,255,0.06); color: #fff; }
    `,
  ],
})
export class Sidenav {
  private auth = inject(AuthService);

  readonly collapsed = input<boolean>(false);
  @Output() toggle = new EventEmitter<void>();

  protected primary = computed(() => filterByRole(pickPrimaryNav(this.auth.roles()), this.auth.roles()));
  protected secondary = computed(() => filterByRole(pickSecondaryNav(this.auth.roles()), this.auth.roles()));
}

function filterByRole(items: NavItem[], roles: string[]): NavItem[] {
  return items.filter((i) => !i.roles || i.roles.some((r) => roles.includes(r)));
}
