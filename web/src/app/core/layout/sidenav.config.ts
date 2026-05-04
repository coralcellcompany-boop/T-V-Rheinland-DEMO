import { Roles } from '../models/auth.models';

export interface NavItem {
  label: string;        // translation key under "nav.*"
  icon: string;
  route: string;
  roles?: readonly string[];
  badge?: 'pending' | 'expired';
}

export const PRIMARY_NAV: NavItem[] = [
  { label: 'nav.dashboard',     icon: 'pi-home',         route: '/dashboard' },
  { label: 'nav.certificates',  icon: 'pi-file-check',   route: '/certificates' },
  { label: 'nav.approvals',     icon: 'pi-thumbs-up',    route: '/approvals',
    roles: [Roles.Manager, Roles.Coordinator, Roles.TechReviewer], badge: 'pending' },
  { label: 'nav.equipment',     icon: 'pi-wrench',       route: '/equipment' },
  { label: 'nav.clients',       icon: 'pi-building',     route: '/clients',
    roles: [Roles.Manager, Roles.Coordinator] },
  { label: 'nav.stickers',      icon: 'pi-qrcode',       route: '/stickers',
    roles: [Roles.Manager, Roles.Coordinator] },
  { label: 'nav.candidates',    icon: 'pi-id-card',      route: '/candidates',
    roles: [Roles.Manager, Roles.Coordinator, Roles.Inspector, Roles.TechReviewer] },
  { label: 'nav.assessments',   icon: 'pi-verified',     route: '/assessments' },
  { label: 'nav.jobRequests',   icon: 'pi-inbox',        route: '/job-requests',
    roles: [Roles.Manager, Roles.Coordinator] },
  { label: 'nav.jobOrders',     icon: 'pi-briefcase',    route: '/job-orders' },
  { label: 'nav.timesheets',    icon: 'pi-clock',        route: '/timesheets' },
  { label: 'nav.surveys',       icon: 'pi-map-marker',   route: '/surveys' },
];

export const SECONDARY_NAV: NavItem[] = [
  { label: 'nav.reports',       icon: 'pi-chart-line',   route: '/reports',
    roles: [Roles.Manager, Roles.Coordinator] },
  { label: 'nav.admin',         icon: 'pi-cog',          route: '/admin', roles: [Roles.Manager] },
];
