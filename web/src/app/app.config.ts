import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection,
} from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { MessageService, ConfirmationService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import Aura from '@primeuix/themes/aura';
import { provideTranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';

import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { LanguageService } from './core/i18n/language.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideAnimationsAsync(),
    provideTranslateService({ fallbackLang: 'en' }),
    provideTranslateHttpLoader({ prefix: './assets/i18n/', suffix: '.json' }),
    provideAppInitializer(() => inject(LanguageService).init()),
    providePrimeNG({
      theme: {
        preset: Aura,
        options: {
          darkModeSelector: '.dark',
          cssLayer: { name: 'primeng', order: 'tailwind-base, primeng, tailwind-utilities' },
        },
      },
      ripple: true,
    }),
    MessageService,
    ConfirmationService,
  ],
};
