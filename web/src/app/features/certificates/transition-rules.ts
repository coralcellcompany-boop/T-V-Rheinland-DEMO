import { CertificateState } from '../../core/models/certificate.models';
import { Roles } from '../../core/models/auth.models';

export interface AvailableTransition {
  trigger: 'Submit' | 'BeginReview' | 'AdvanceForApproval' | 'FinalApprove' | 'Reject'
        | 'Void' | 'SendToClient' | 'ClientAccept' | 'ClientReject' | 'Archive';
  label: string;
  icon: string;
  severity?: 'primary' | 'secondary' | 'success' | 'warn' | 'danger' | 'info';
  requireComments?: boolean;
  description?: string;
}

const T = {
  Submit:             { trigger: 'Submit',             label: 'Submit',                icon: 'pi-send',          severity: 'primary' },
  BeginReview:        { trigger: 'BeginReview',        label: 'Begin review',          icon: 'pi-eye',           severity: 'info' },
  AdvanceForApproval: { trigger: 'AdvanceForApproval', label: 'Advance for approval',  icon: 'pi-arrow-right',   severity: 'info' },
  FinalApprove:       { trigger: 'FinalApprove',       label: 'Approve',               icon: 'pi-check',         severity: 'success' },
  Reject:             { trigger: 'Reject',             label: 'Reject',                icon: 'pi-times',         severity: 'danger', requireComments: true },
  Void:               { trigger: 'Void',               label: 'Void',                  icon: 'pi-ban',           severity: 'danger', requireComments: true },
  SendToClient:       { trigger: 'SendToClient',       label: 'Send to client',        icon: 'pi-share-alt',     severity: 'primary' },
  ClientAccept:       { trigger: 'ClientAccept',       label: 'Client accept',         icon: 'pi-check-circle',  severity: 'success' },
  ClientReject:       { trigger: 'ClientReject',       label: 'Client reject',         icon: 'pi-flag',          severity: 'danger', requireComments: true },
  Archive:            { trigger: 'Archive',            label: 'Archive',               icon: 'pi-inbox',         severity: 'secondary' },
} as const satisfies Record<string, AvailableTransition>;

/**
 * Mirror of the backend Stateless config. Used by the UI to decide which transition buttons
 * to show. The backend remains the source of truth — invalid attempts return 409.
 */
export function availableTransitions(
  state: number,
  roles: readonly string[]
): AvailableTransition[] {
  const has = (r: string) => roles.includes(r);
  const techOrManager = has(Roles.Manager) || has(Roles.TechReviewer);
  const inspector = has(Roles.Inspector);
  const manager = has(Roles.Manager);
  const managerOrCoord = has(Roles.Manager) || has(Roles.Coordinator);
  const clientUser = has(Roles.ClientUser);

  switch (state) {
    case CertificateState.Draft:
      return inspector ? [T.Submit] : [];
    case CertificateState.Submitted:
      return techOrManager ? [T.BeginReview, T.Reject] : [];
    case CertificateState.UnderReview:
      return techOrManager ? [T.AdvanceForApproval, T.Reject] : [];
    case CertificateState.AwaitingApproval:
      return manager ? [T.FinalApprove, T.Reject] : [];
    case CertificateState.Approved:
      const acts: AvailableTransition[] = [];
      if (managerOrCoord) acts.push(T.SendToClient);
      if (manager) acts.push(T.Void);
      return acts;
    case CertificateState.ClientSent:
      return clientUser ? [T.ClientAccept, T.ClientReject] : [];
    case CertificateState.ClientRejected:
      return techOrManager ? [T.BeginReview] : [];
    case CertificateState.ClientAccepted:
      return managerOrCoord ? [T.Archive] : [];
    case CertificateState.Rejected:
      return inspector ? [T.Submit] : [];
    default:
      return [];
  }
}
