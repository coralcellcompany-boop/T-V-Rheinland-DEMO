export const ContractStatus = {
  Active: 0,
  Suspended: 1,
  Terminated: 2,
} as const;
export type ContractStatusValue = (typeof ContractStatus)[keyof typeof ContractStatus];
export const ContractStatusLabel: Record<number, string> = {
  0: 'Active',
  1: 'Suspended',
  2: 'Terminated',
};

export const ServiceType = {
  None: 0,
  ThirdPartyInspection: 1,
  BlueSticker: 2,
  OperatorAssessment: 4,
  All: 7,
} as const;

export interface ClientListItem {
  id: string;
  name: string;
  code: string;
  contractStatus: number;
  allowedServices: number;
  contactName: string | null;
  contactEmail: string | null;
  createdAtUtc: string;
}

export interface ClientDetail {
  id: string;
  name: string;
  code: string;
  address: string | null;
  contactName: string | null;
  contactPhone: string | null;
  contactEmail: string | null;
  contractStatus: number;
  allowedServices: number;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface CreateClientRequest {
  name: string;
  code: string;
  address?: string | null;
  contactName?: string | null;
  contactPhone?: string | null;
  contactEmail?: string | null;
  contractStatus: number;
  allowedServices: number;
}

export type UpdateClientRequest = Omit<CreateClientRequest, 'code'>;
