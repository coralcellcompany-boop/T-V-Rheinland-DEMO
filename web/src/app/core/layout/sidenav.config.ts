import { Roles } from '../models/auth.models';

export interface NavItem {
  label: string;
  icon: string;
  route: string;
  roles?: readonly string[];   // empty = all authenticated
  badge?: 'pending' | 'expired';
}

export const PRIMARY_NAV: NavItem[] = [
  { label: 'Dashboard',     icon: 'pi-home',           route: '/dashboard' },
  { label: 'Certificates',  icon: 'pi-file-check',     route: '/certificates' },
  { label: 'Approvals',     icon: 'pi-thumbs-up',      route: '/approvals',
    roles: [Roles.Manager, Roles.Coordinator, Roles.TechReviewer], badge: 'pending' },
  { label: 'Equipment',     icon: 'pi-wrench',         route: '/equipment' },
  { label: 'Clients',       icon: 'pi-building',       route: '/clients',
    roles: [Roles.Manager, Roles.Coordinator] },
  { label: 'Stickers',      icon: 'pi-qrcode',         route: '/stickers',
    roles: [Roles.Manager, Roles.Coordinator] },
  { label: 'Candidates',    icon: 'pi-id-card',        route: '/candidates',
    roles: [Roles.Manager, Roles.Coordinator, Roles.Inspector, Roles.TechReviewer] },
  { label: 'Assessments',   icon: 'pi-verified',       route: '/assessments' },
];

export const SECONDARY_NAV: NavItem[] = [
  { label: 'Admin',         icon: 'pi-cog',            route: '/admin', roles: [Roles.Manager] },
];
