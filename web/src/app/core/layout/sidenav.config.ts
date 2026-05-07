import { Roles } from '../models/auth.models';

export interface NavItem {
  label: string;        // translation key under "nav.*"
  icon: string;
  route: string;
  roles?: readonly string[];
  badge?: 'pending' | 'expired';
}

export interface NavSection {
  label: string;        // translation key under "nav.section.*"
  items: NavItem[];
}

/**
 * Primary nav, organised by the three service lines TUV Rheinland Arabia operates
 * (Third Party Inspection, Blue Sticker, Operator Assessment) plus a cross-cutting
 * Operations section. Sections empty after role filtering are dropped from the menu.
 */
export const PRIMARY_SECTIONS: NavSection[] = [
  {
    label: 'nav.section.workspace',
    items: [
      { label: 'nav.dashboard', icon: 'pi-home', route: '/dashboard' },
    ],
  },
  {
    label: 'nav.section.thirdParty',
    items: [
      { label: 'nav.certificates', icon: 'pi-file-check', route: '/certificates' },
      { label: 'nav.equipment',    icon: 'pi-wrench',     route: '/equipment' },
      { label: 'nav.approvals',    icon: 'pi-thumbs-up',  route: '/approvals',
        roles: [Roles.Manager, Roles.Coordinator, Roles.TechReviewer], badge: 'pending' },
    ],
  },
  {
    label: 'nav.section.blueSticker',
    items: [
      { label: 'nav.stickers',         icon: 'pi-qrcode', route: '/stickers',
        roles: [Roles.Manager, Roles.Coordinator] },
      { label: 'nav.stickerRequests',  icon: 'pi-inbox',  route: '/sticker-requests',
        roles: [Roles.Manager, Roles.Coordinator, Roles.Inspector] },
    ],
  },
  {
    label: 'nav.section.assessment',
    items: [
      { label: 'nav.candidates',  icon: 'pi-id-card',  route: '/candidates',
        roles: [Roles.Manager, Roles.Coordinator, Roles.Inspector, Roles.TechReviewer] },
      { label: 'nav.assessments', icon: 'pi-verified', route: '/assessments' },
    ],
  },
  {
    label: 'nav.section.operations',
    items: [
      { label: 'nav.clients',     icon: 'pi-building',   route: '/clients',
        roles: [Roles.Manager, Roles.Coordinator] },
      { label: 'nav.jobRequests', icon: 'pi-inbox',      route: '/job-requests',
        roles: [Roles.Manager, Roles.Coordinator] },
      { label: 'nav.jobOrders',   icon: 'pi-briefcase',  route: '/job-orders' },
      { label: 'nav.timesheets',  icon: 'pi-clock',      route: '/timesheets' },
      { label: 'nav.surveys',     icon: 'pi-map-marker', route: '/surveys' },
    ],
  },
];

export const SECONDARY_NAV: NavItem[] = [
  { label: 'nav.reports', icon: 'pi-chart-line', route: '/reports',
    roles: [Roles.Manager, Roles.Coordinator] },
  { label: 'nav.audit',   icon: 'pi-history',    route: '/audit',
    roles: [Roles.Manager] },
  { label: 'nav.admin',   icon: 'pi-cog',        route: '/admin', roles: [Roles.Manager] },
];

/**
 * When the user is *only* a ClientUser (no staff roles), the shell switches to this
 * focused nav. They see only their own pending acceptance queue and the certificate
 * detail view.
 */
export const CLIENT_NAV: NavItem[] = [
  { label: 'nav.dashboard',    icon: 'pi-home',          route: '/my-certificates' },
  { label: 'nav.certificates', icon: 'pi-file-check',    route: '/my-certificates' },
];

export function pickPrimarySections(roles: readonly string[]): NavSection[] {
  const staffRoles: string[] = [Roles.Manager, Roles.Coordinator, Roles.Inspector, Roles.TechReviewer];
  const isStaff = roles.some(r => staffRoles.includes(r));
  if (!isStaff) {
    return [{ label: 'nav.section.workspace', items: CLIENT_NAV }];
  }
  return PRIMARY_SECTIONS;
}

export function pickSecondaryNav(roles: readonly string[]): NavItem[] {
  const staffRoles: string[] = [Roles.Manager, Roles.Coordinator, Roles.Inspector, Roles.TechReviewer];
  const isStaff = roles.some(r => staffRoles.includes(r));
  return isStaff ? SECONDARY_NAV : [];
}
