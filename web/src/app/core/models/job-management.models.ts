// Job Requests
export const JobRequestStatus = { New: 0, UnderReview: 1, Accepted: 2, Rejected: 3, Converted: 4 } as const;
export const JobRequestStatusName: Record<number, string> = {
  0: 'New', 1: 'UnderReview', 2: 'Accepted', 3: 'Rejected', 4: 'Converted',
};

export interface JobRequestListItem {
  id: string; requestNo: string; clientId: string; clientName: string;
  service: number; requestedFrom: string; requestedTo: string;
  site: string | null; contactEmail: string | null;
  status: number; convertedJobOrderId: string | null;
  createdAtUtc: string;
}
export interface JobRequestDetail extends JobRequestListItem {
  contactName: string | null; contactPhone: string | null;
  scopeNotes: string | null; poReference: string | null;
  rejectionReason: string | null; updatedAtUtc: string | null;
}
export interface CreateJobRequestRequest {
  clientId: string; service: number; requestedFrom: string; requestedTo: string;
  site?: string | null; contactName?: string | null; contactPhone?: string | null;
  contactEmail?: string | null; scopeNotes?: string | null; poReference?: string | null;
}

// Job Orders
export const JobOrderStatus = { Open: 0, InProgress: 1, Completed: 2, Cancelled: 3 } as const;
export const JobOrderStatusName: Record<number, string> = {
  0: 'Open', 1: 'InProgress', 2: 'Completed', 3: 'Cancelled',
};
export const ServiceType = { None: 0, ThirdPartyInspection: 1, BlueSticker: 2, OperatorAssessment: 4, All: 7 } as const;
export const ServiceTypeLabel: Record<number, string> = {
  0: 'None', 1: 'TPI', 2: 'Blue Sticker', 4: 'Operator Assessment', 7: 'All services',
};

export interface JobOrderListItem {
  id: string; jobOrderNo: string; clientId: string; clientName: string;
  service: number; dateFrom: string; dateTo: string;
  location: string | null; status: number; assignedInspectorCount: number;
  attachmentCount: number;
  createdAtUtc: string;
}
export interface JobOrderDetail {
  id: string; jobOrderNo: string; clientId: string; clientName: string;
  service: number; dateFrom: string; dateTo: string;
  location: string | null; status: number;
  assignedInspectorIds: string[];
  attachmentKeys: string[];
  createdAtUtc: string; updatedAtUtc: string | null;
}
export interface CreateJobOrderRequest {
  clientId: string; service: number; dateFrom: string; dateTo: string; location?: string | null;
  quantity?: number; attachmentKeys?: string[] | null;
}
export interface UpdateJobOrderRequest {
  dateFrom: string; dateTo: string; location?: string | null;
  status: number; assignedInspectorIds: string[];
  attachmentKeys?: string[] | null;
}

// DWR
export const DwrStatus = { Draft: 0, Submitted: 1, Approved: 2, Rejected: 3 } as const;
export const DwrStatusName: Record<number, string> = {
  0: 'Draft', 1: 'Submitted', 2: 'Approved', 3: 'Rejected',
};
export interface DwrListItem {
  id: string; dwrNo: string; jobOrderId: string; jobOrderNo: string;
  clientId: string; clientName: string;
  inspectorId: string; inspectorName: string | null;
  date: string; timeFrom: string; timeTo: string;
  equipmentInspected: number; operatorsAssessed: number;
  status: number; createdAtUtc: string;
}
export interface CreateDwrRequest {
  jobOrderId: string; date: string; timeFrom: string; timeTo: string;
  location?: string | null; equipmentInspected: number; operatorsAssessed: number;
  notes?: string | null;
}

// Surveys
export const SurveyStatus = { Draft: 0, Submitted: 1, ConvertedToJobOrder: 2 } as const;
export const SurveyStatusName: Record<number, string> = {
  0: 'Draft', 1: 'Submitted', 2: 'ConvertedToJobOrder',
};
export interface SurveyListItem {
  id: string; surveyNo: string; clientId: string; clientName: string;
  date: string; site: string | null; estimatedEquipmentCount: number;
  status: number; convertedJobOrderId: string | null; createdAtUtc: string;
}
export interface CreateSurveyRequest {
  clientId: string; date: string; site?: string | null;
}
