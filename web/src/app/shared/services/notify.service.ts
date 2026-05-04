import { inject, Injectable } from '@angular/core';
import { MessageService } from 'primeng/api';

/**
 * Thin wrapper over PrimeNG MessageService — keeps callers from importing PrimeNG primitives
 * directly and centralizes default lifetimes, severities and labels.
 */
@Injectable({ providedIn: 'root' })
export class NotifyService {
  private msg = inject(MessageService);

  success(detail: string, summary = 'Success'): void {
    this.msg.add({ severity: 'success', summary, detail, life: 3500 });
  }

  info(detail: string, summary = 'Info'): void {
    this.msg.add({ severity: 'info', summary, detail, life: 3500 });
  }

  warn(detail: string, summary = 'Heads up'): void {
    this.msg.add({ severity: 'warn', summary, detail, life: 4500 });
  }

  error(detail: string, summary = 'Something went wrong'): void {
    this.msg.add({ severity: 'error', summary, detail, life: 6000 });
  }
}
