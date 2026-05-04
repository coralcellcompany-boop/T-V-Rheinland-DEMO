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
}

export interface CreateUserRequest {
  email: string;
  fullName: string;
  sapNo?: string | null;
  certNo?: string | null;
  password: string;
  roles: string[];
  assignedClientIds?: string[] | null;
}

export interface UpdateUserRequest {
  fullName: string;
  sapNo?: string | null;
  certNo?: string | null;
  isActive: boolean;
  roles: string[];
  assignedClientIds: string[];
}
