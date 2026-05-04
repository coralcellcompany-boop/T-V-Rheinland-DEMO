import { Injectable, inject, signal } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

const STORAGE_KEY = 'tuv.lang';
export type Lang = 'en' | 'ar';
const SUPPORTED: Lang[] = ['en', 'ar'];

/**
 * Centralizes language switching, persists the choice, and toggles document direction
 * (LTR/RTL) so the whole shell reflows for Arabic without per-component overrides.
 */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private translate = inject(TranslateService);
  readonly current = signal<Lang>('en');

  init(): void {
    this.translate.addLangs(SUPPORTED);
    this.translate.setFallbackLang('en');
    const stored = (localStorage.getItem(STORAGE_KEY) as Lang | null) ?? 'en';
    this.use(SUPPORTED.includes(stored) ? stored : 'en');
  }

  use(lang: Lang): void {
    this.current.set(lang);
    this.translate.use(lang);
    localStorage.setItem(STORAGE_KEY, lang);
    const dir = lang === 'ar' ? 'rtl' : 'ltr';
    document.documentElement.setAttribute('lang', lang);
    document.documentElement.setAttribute('dir', dir);
    document.body.setAttribute('dir', dir);
  }

  toggle(): void { this.use(this.current() === 'en' ? 'ar' : 'en'); }
}
