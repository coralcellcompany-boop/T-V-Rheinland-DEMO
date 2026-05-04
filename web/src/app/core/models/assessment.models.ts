export const CompetencyCategory = {
  None: 0,
  MobileCrane: 1,
  Forklift: 2,
  Manlift: 3,
  WheelLoader: 4,
  MewpTelehandler: 5,
} as const;

export const CompetencyCategoryLabel: Record<number, string> = {
  1: 'Mobile Crane Operator',
  2: 'Forklift Operator',
  3: 'Manlift Operator',
  4: 'Wheel Loader Operator',
  5: 'MEWP / Telehandler',
};

export const AssessmentState = {
  Draft: 0,
  Submitted: 1,
  Approved: 2,
  Rejected: 3,
  Expired: 4,
} as const;

export const AssessmentStateName: Record<number, string> = {
  0: 'Draft',
  1: 'Submitted',
  2: 'Approved',
  3: 'Rejected',
  4: 'Expired',
};

export const AssessmentResult = { NotSet: 0, Pass: 1, Fail: 2 } as const;
export const AssessmentResultLabel: Record<number, string> = {
  0: 'Not set',
  1: 'Pass',
  2: 'Fail',
};

export type AssessmentTrigger = 'Submit' | 'Approve' | 'Reject' | 'Resubmit';

export interface CandidateListItem {
  id: string;
  clientId: string;
  clientName: string;
  fullName: string;
  identificationNumber: string;
  phone: string | null;
  email: string | null;
  nationality: string | null;
  isActive: boolean;
  createdAtUtc: string;
}

export interface CandidateDetail extends CandidateListItem {
  employeeNo: string | null;
  dateOfBirth: string | null;
  photoKey: string | null;
  updatedAtUtc: string | null;
}

export interface CreateCandidateRequest {
  clientId: string;
  fullName: string;
  identificationNumber: string;
  phone?: string | null;
  email?: string | null;
  employeeNo?: string | null;
  nationality?: string | null;
  dateOfBirth?: string | null;
}

export interface UpdateCandidateRequest extends Omit<CreateCandidateRequest, 'clientId'> {
  isActive: boolean;
}

export interface AssessmentTransition {
  id: string;
  fromState: number;
  toState: number;
  actorUserId: string;
  actorRole: string;
  comments: string | null;
  atUtc: string;
}

export interface AssessmentListItem {
  id: string;
  assessmentNo: string;
  candidateId: string;
  candidateName: string;
  clientId: string;
  clientName: string;
  category: number;
  assessmentDate: string;
  nextAssessmentDate: string | null;
  result: number;
  state: number;
  issuedCardNo: string | null;
  createdAtUtc: string;
}

export interface AssessmentDetail {
  id: string;
  assessmentNo: string;
  candidateId: string;
  candidateName: string;
  candidateIdNumber: string;
  clientId: string;
  clientName: string;
  category: number;
  assessmentDate: string;
  nextAssessmentDate: string | null;
  location: string | null;
  theoreticalScore: number | null;
  practicalScore: number | null;
  result: number;
  comments: string | null;
  state: number;
  issuedCardId: string | null;
  issuedCardNo: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  transitions: AssessmentTransition[];
}

export interface CreateAssessmentRequest {
  candidateId: string;
  category: number;
  assessmentDate: string;
  location?: string | null;
}

export interface UpdateAssessmentRequest {
  assessmentDate: string;
  nextAssessmentDate?: string | null;
  location?: string | null;
  theoreticalScore?: number | null;
  practicalScore?: number | null;
  result: number;
  comments?: string | null;
}

export interface CompetencyCardListItem {
  id: string;
  cardNo: string;
  assessmentId: string;
  assessmentNo: string;
  candidateId: string;
  candidateName: string;
  clientId: string;
  clientName: string;
  category: number;
  issuedOn: string;
  validUntil: string | null;
  state: number;
}

export const CompetencyCardStateName: Record<number, string> = {
  0: 'Issued',
  1: 'Lost',
  2: 'Suspended',
  3: 'Expired',
  4: 'Revoked',
};

export interface CompetencyCardPublicView {
  cardNo: string;
  category: number;
  candidateNameMasked: string;
  candidateIdMasked: string;
  clientName: string | null;
  issuedOn: string;
  validUntil: string | null;
  isValidNow: boolean;
  state: number;
}
