import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Output, inject } from '@angular/core';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { MenuModule } from 'primeng/menu';
import { MenuItem } from 'primeng/api';
import { AuthService } from '../auth/auth.service';

@Component({
  selector: 'tuv-topbar',
  standalone: true,
  imports: [CommonModule, ButtonModule, MenuModule],
  template: `
    <header class="topbar">
      <button class="hamburger" (click)="menu.emit()" aria-label="Toggle menu">
        <i class="pi pi-bars"></i>
      </button>

      <div class="search">
        <i class="pi pi-search"></i>
        <input type="text" placeholder="Search certificates, equipment, clients…" />
        <kbd>⌘K</kbd>
      </div>

      <div class="right">
        <button class="icon-btn" pTooltip="Notifications">
          <i class="pi pi-bell"></i>
        </button>

        <p-menu #userMenu [popup]="true" [model]="userMenuItems" />
        <button type="button" class="user" (click)="userMenu.toggle($event)">
          <span class="avatar">{{ initials() }}</span>
          <span class="meta">
            <span class="name">{{ auth.user()?.fullName ?? auth.user()?.userName }}</span>
            <span class="role">{{ auth.roles()[0] ?? '' }}</span>
          </span>
          <i class="pi pi-angle-down"></i>
        </button>
      </div>
    </header>
  `,
  styles: [
    `
      :host { display: block; }
      .topbar {
        display: flex; align-items: center; gap: 1rem;
        padding: 0.6rem 1.1rem;
        background: #ffffff;
        border-bottom: 1px solid #e5e9f2;
      }
      .hamburger {
        background: transparent; border: 1px solid #e5e9f2; border-radius: 8px;
        padding: 0.4rem 0.55rem; cursor: pointer; color: #475569;
      }
      .hamburger:hover { background: #f1f5f9; }

      .search {
        flex: 1; max-width: 460px;
        display: flex; align-items: center; gap: 0.55rem;
        background: #f5f7fb; border: 1px solid transparent;
        border-radius: 10px; padding: 0.45rem 0.7rem;
        transition: border-color 0.15s ease, background 0.15s ease;
      }
      .search:focus-within { background: #fff; border-color: #c7d2fe; box-shadow: 0 0 0 4px rgba(99, 102, 241, 0.12); }
      .search input { flex: 1; border: 0; background: transparent; outline: none; font-size: 0.92rem; color: #0f172a; }
      .search .pi { color: #94a3b8; }
      .search kbd {
        font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
        font-size: 0.7rem; color: #64748b;
        background: #fff; border: 1px solid #e5e9f2; border-radius: 6px; padding: 0.05rem 0.35rem;
      }

      .right { margin-left: auto; display: flex; align-items: center; gap: 0.4rem; }
      .icon-btn {
        background: transparent; border: 1px solid transparent; border-radius: 10px;
        padding: 0.4rem 0.6rem; cursor: pointer; color: #475569;
        position: relative;
      }
      .icon-btn:hover { background: #f1f5f9; color: #0f172a; }

      .user {
        display: flex; align-items: center; gap: 0.6rem;
        background: transparent; border: 1px solid transparent;
        padding: 0.3rem 0.55rem 0.3rem 0.3rem; border-radius: 999px;
        cursor: pointer;
      }
      .user:hover { background: #f1f5f9; border-color: #e5e9f2; }
      .avatar {
        width: 34px; height: 34px; border-radius: 50%;
        background: linear-gradient(135deg, #1d4ed8, #4f46e5);
        color: #fff; font-weight: 700; font-size: 0.85rem;
        display: inline-flex; align-items: center; justify-content: center;
      }
      .meta { display: flex; flex-direction: column; align-items: flex-start; line-height: 1.1; }
      .meta .name { font-size: 0.85rem; font-weight: 600; color: #0f172a; }
      .meta .role { font-size: 0.72rem; color: #64748b; }
    `,
  ],
})
export class Topbar {
  protected auth = inject(AuthService);
  private router = inject(Router);

  @Output() menu = new EventEmitter<void>();

  protected initials = () => {
    const name = this.auth.user()?.fullName ?? this.auth.user()?.userName ?? '';
    const parts = name.split(/[\s@.]+/).filter(Boolean);
    return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase() || 'U';
  };

  protected userMenuItems: MenuItem[] = [
    {
      label: 'Profile',
      icon: 'pi pi-user',
      command: () => this.router.navigate(['/profile']),
    },
    {
      label: 'Settings',
      icon: 'pi pi-cog',
      command: () => this.router.navigate(['/admin']),
    },
    { separator: true },
    {
      label: 'Sign out',
      icon: 'pi pi-sign-out',
      command: () => {
        this.auth.logout();
        this.router.navigate(['/login']);
      },
    },
  ];
}
