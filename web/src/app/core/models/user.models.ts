export interface UserListItem {
  id: string;
  userName: string;
  email: string | null;
  fullName: string | null;
  sapNo: string | null;
  certNo: string | null;
  isActive: boolean;
  isLockedOut: boolean;
  roles: string[];
  assignedClientIds: string[];
  createdAtUtc: string;
  hasSignature: boolean;
}

export interface CreateUserRequest {
  email: string;
  fullName: string;
  sapNo?: string | null;
  certNo?: string | null;
  password: string;
  roles: string[];
  assignedClientIds?: string[] | null;
  signaturePng?: string | null;
}

export interface UpdateUserRequest {
  fullName: string;
  sapNo?: string | null;
  certNo?: string | null;
  isActive: boolean;
  roles: string[];
  assignedClientIds: string[];
  signaturePng?: string | null;
}

export interface Profile {
  id: string;
  email: string | null;
  fullName: string | null;
  sapNo: string | null;
  roles: string[];
  signaturePng: string | null;
}
