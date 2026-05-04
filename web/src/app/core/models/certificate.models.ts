export const CertificateState = {
  Draft: 0,
  Submitted: 1,
  UnderReview: 2,
  AwaitingApproval: 3,
  Approved: 4,
  ClientSent: 5,
  ClientAccepted: 6,
  ClientRejected: 7,
  Rejected: 8,
  Voided: 9,
  Expired: 10,
  Archived: 11,
} as const;

export const CertificateStateName: Record<number, string> = {
  0: 'Draft',
  1: 'Submitted',
  2: 'UnderReview',
  3: 'AwaitingApproval',
  4: 'Approved',
  5: 'ClientSent',
  6: 'ClientAccepted',
  7: 'ClientRejected',
  8: 'Rejected',
  9: 'Voided',
  10: 'Expired',
  11: 'Archived',
};

export const CertificateInspectionType = { PeriodicInspection: 0, ReInspection: 1, InitialInspection: 2 } as const;
export const CertificateInspectionTypeLabel: Record<number, string> = {
  0: 'Periodic',
  1: 'Re-inspection',
  2: 'Initial',
};

export const LoadTestKind = { None: 0, Mechanical: 1, Witnessed: 2, Performed: 3 } as const;
export const LoadTestKindLabel: Record<number, string> = {
  0: '—',
  1: 'Mechanical',
  2: 'Witnessed',
  3: 'Performed',
};

export const InspectionResult = { NotSet: 0, Pass: 1, Fail: 2, FailWithObservations: 3 } as const;
export const InspectionResultLabel: Record<number, string> = {
  0: 'Not set',
  1: 'Pass',
  2: 'Fail',
  3: 'Fail w/ observations',
};

export type CertificateTrigger =
  | 'Submit' | 'BeginReview' | 'AdvanceForApproval' | 'FinalApprove' | 'Reject'
  | 'Void' | 'SendToClient' | 'ClientAccept' | 'ClientReject' | 'Archive' | 'Expire';

export interface CertificateTransition {
  id: string;
  fromState: number;
  toState: number;
  actorUserId: string;
  actorRole: string;
  comments: string | null;
  atUtc: string;
}

export interface CertificateListItem {
  id: string;
  certificateNo: string;
  clientId: string;
  clientName: string;
  equipmentId: string;
  equipmentIdNo: string;
  equipmentTypeName: string;
  inspectionDate: string;
  nextDueDate: string | null;
  inspectionType: number;
  loadTest: number;
  result: number;
  state: number;
  stickerNo: string | null;
  createdAtUtc: string;
}

export interface CertificateDetail {
  id: string;
  certificateNo: string;
  clientId: string;
  clientName: string;
  equipmentId: string;
  equipmentIdNo: string;
  equipmentTypeId: string;
  equipmentTypeName: string;
  jobOrderId: string | null;
  inspectionDate: string;
  reportIssueDate: string;
  nextDueDate: string | null;
  inspectionType: number;
  loadTest: number;
  result: number;
  state: number;
  standards: string | null;
  stickerNo: string | null;
  checklistJson: string | null;
  findingsJson: string | null;
  photosJson: string | null;
  signaturesJson: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  transitions: CertificateTransition[];
}

export interface CreateCertificateRequest {
  equipmentId: string;
  jobOrderId?: string | null;
  inspectionDate: string;
  reportIssueDate: string;
  inspectionType: number;
  standards?: string | null;
}

export interface UpdateCertificateRequest {
  inspectionDate: string;
  reportIssueDate: string;
  nextDueDate?: string | null;
  inspectionType: number;
  loadTest: number;
  result: number;
  standards?: string | null;
  stickerNo?: string | null;
  checklistJson?: string | null;
  findingsJson?: string | null;
  photosJson?: string | null;
  signaturesJson?: string | null;
}

export interface ApprovalQueueCounts {
  pending: number;
  rejected: number;
  mine: number;
}

export interface DashboardKpis {
  totalCertificates: number;
  certificatesThisMonth: number;
  pending: number;
  rejected: number;
  dueSoon: number;
  expired: number;
  activeEquipment: number;
  clients: number;
}

export interface RecentActivityItem {
  entityName: string;
  entityId: string;
  action: string;
  actorUserId: string | null;
  actorRole: string | null;
  atUtc: string;
}
