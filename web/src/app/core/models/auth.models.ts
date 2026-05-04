// Mirrors TuvInspection.Contracts.Auth.* — will be replaced by NSwag-generated types in Phase D.

export interface UserProfile {
  id: string;
  userName: string;
  email: string | null;
  fullName: string | null;
  sapNo: string | null;
  certNo: string | null;
  roles: string[];
  assignedClientIds: string[];
}

export interface LoginRequest {
  userName: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAtUtc: string;
  user: UserProfile;
}

export const Roles = {
  Manager: 'Manager',
  Coordinator: 'Coordinator',
  Inspector: 'Inspector',
  TechReviewer: 'TechReviewer',
  ClientUser: 'ClientUser',
} as const;
export type Role = (typeof Roles)[keyof typeof Roles];
