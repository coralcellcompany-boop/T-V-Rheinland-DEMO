export const StickerState = {
  Unallocated: 0,
  AllocatedToJob: 1,
  Issued: 2,
  Replaced: 3,
  Voided: 4,
  Expired: 5,
} as const;

export const StickerStateName: Record<number, string> = {
  0: 'Unallocated',
  1: 'AllocatedToJob',
  2: 'Issued',
  3: 'Replaced',
  4: 'Voided',
  5: 'Expired',
};

export interface StickerListItem {
  id: string;
  stickerNo: string;
  state: number;
  clientId: string | null;
  clientName: string | null;
  issuedToCertificateId: string | null;
  certificateNo: string | null;
  issuedToEquipmentId: string | null;
  equipmentIdNo: string | null;
  validUntil: string | null;
  createdAtUtc: string;
}

export interface StickerStockSummary {
  unallocated: number;
  issued: number;
  voided: number;
  expired: number;
  lowStockThreshold: number;
  isLowStock: boolean;
}

export interface StickerPublicView {
  stickerNo: string;
  state: number;
  aramcoCategory: string | null;
  equipmentTypeName: string | null;
  equipmentIdNo: string | null;
  clientName: string | null;
  validUntil: string | null;
  isValidNow: boolean;
  certificateNo: string | null;
  issuedAtUtc: string | null;
}
