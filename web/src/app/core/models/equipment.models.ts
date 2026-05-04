export const EquipmentStatus = { Active: 0, Decommissioned: 1, Sold: 2 } as const;
export const EquipmentStatusLabel: Record<number, string> = {
  0: 'Active',
  1: 'Decommissioned',
  2: 'Sold',
};

export const AramcoCategory = {
  None: 0,
  CR01: 1, CR02: 2, CR03: 3, CR04: 4, CR05: 5, CR06: 6, CR07: 7,
  CR08: 8, CR09: 9, CR10: 10, CR11: 11, CR12: 12, CR13: 13, CR14: 14,
} as const;

export const AramcoCategoryName: Record<number, string> = {
  1: 'CR01 · Mobile Crane',
  2: 'CR02 · Elevator/Escalator',
  3: 'CR03 · Elevation Work Platform',
  4: 'CR04 · Marine/Offshore Cranes',
  5: 'CR05 · Storage Retrieval Machine',
  6: 'CR06 · Articulating Boom Crane',
  7: 'CR07 · Lifting/Spreader Beam',
  8: 'CR08 · Powered Platform/Sky Climber',
  9: 'CR09 · Vehicle-Mounted Aerial Device',
  10: 'CR10 · Manbasket',
  11: 'CR11 · Fixed Cranes/Hoists',
  12: 'CR12 · Side Boom Tractor',
  13: 'CR13 · A-Frame/Mobile Gantry',
  14: 'CR14 · Tower Crane',
};

export interface EquipmentType {
  id: string;
  name: string;
  aramcoCategory: number | null;
  defaultStandards: string | null;
  msReference: string | null;
  annex: string | null;
  isActive: boolean;
}

export interface EquipmentListItem {
  id: string;
  clientId: string;
  clientName: string;
  equipmentTypeId: string;
  equipmentTypeName: string;
  aramcoCategory: number | null;
  idNo: string;
  serialNo: string | null;
  manufacturer: string | null;
  model: string | null;
  swl: string | null;
  location: string | null;
  status: number;
}

export interface EquipmentDetail extends EquipmentListItem {
  yearOfManufacture: number | null;
  photoKey: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface CreateEquipmentRequest {
  clientId: string;
  equipmentTypeId: string;
  aramcoCategory: number | null;
  idNo: string;
  serialNo?: string | null;
  manufacturer?: string | null;
  model?: string | null;
  yearOfManufacture?: number | null;
  swl?: string | null;
  location?: string | null;
}

export interface UpdateEquipmentRequest {
  equipmentTypeId: string;
  aramcoCategory: number | null;
  idNo: string;
  serialNo?: string | null;
  manufacturer?: string | null;
  model?: string | null;
  yearOfManufacture?: number | null;
  swl?: string | null;
  location?: string | null;
  status: number;
}

export interface EquipmentImportResult {
  imported: number;
  skipped: number;
  errors: string[];
}
