import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/auth/auth.guards';
import { Roles } from './core/models/auth.models';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login.page').then((m) => m.LoginPage),
  },
  // Public, no-auth verification pages reachable from the QR codes.
  {
    path: 'verify/:stickerNo',
    loadComponent: () => import('./features/public/verify.page').then((m) => m.VerifyPage),
  },
  {
    path: 'verify-card/:cardNo',
    loadComponent: () => import('./features/public/verify-card.page').then((m) => m.VerifyCardPage),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./core/layout/shell.component').then((m) => m.ShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/dashboard/dashboard.page').then((m) => m.DashboardPage),
      },
      {
        path: 'my-certificates',
        loadComponent: () =>
          import('./features/client-portal/my-certificates.page').then((m) => m.MyCertificatesPage),
      },
      {
        path: 'clients',
        canActivate: [roleGuard(Roles.Manager, Roles.Coordinator)],
        loadComponent: () =>
          import('./features/clients/pages/clients-list.page').then((m) => m.ClientsListPage),
      },
      {
        path: 'equipment',
        loadComponent: () =>
          import('./features/equipment/pages/equipment-list.page').then((m) => m.EquipmentListPage),
      },
      {
        path: 'certificates',
        loadComponent: () =>
          import('./features/certificates/pages/certificates-list.page').then((m) => m.CertificatesListPage),
      },
      {
        path: 'certificates/:id',
        loadComponent: () =>
          import('./features/certificates/pages/certificate-detail.page').then((m) => m.CertificateDetailPage),
      },
      {
        path: 'approvals',
        canActivate: [roleGuard(Roles.Manager, Roles.Coordinator, Roles.TechReviewer)],
        loadComponent: () =>
          import('./features/approvals/pages/approvals.page').then((m) => m.ApprovalsPage),
      },
      {
        path: 'stickers',
        canActivate: [roleGuard(Roles.Manager, Roles.Coordinator)],
        loadComponent: () =>
          import('./features/stickers/pages/stickers-list.page').then((m) => m.StickersListPage),
      },
      {
        path: 'sticker-requests',
        canActivate: [roleGuard(Roles.Manager, Roles.Coordinator, Roles.Inspector)],
        loadComponent: () =>
          import('./features/stickers/pages/sticker-requests.page').then((m) => m.StickerRequestsPage),
      },
      {
        path: 'my-stickers',
        canActivate: [roleGuard(Roles.Inspector, Roles.Manager, Roles.Coordinator)],
        loadComponent: () =>
          import('./features/stickers/pages/my-stickers.page').then((m) => m.MyStickersPage),
      },
      {
        path: 'candidates',
        canActivate: [roleGuard(Roles.Manager, Roles.Coordinator, Roles.Inspector, Roles.TechReviewer)],
        loadComponent: () =>
          import('./features/operator-assessment/pages/candidates-list.page').then((m) => m.CandidatesListPage),
      },
      {
        path: 'assessments',
        loadComponent: () =>
          import('./features/operator-assessment/pages/assessments-list.page').then((m) => m.AssessmentsListPage),
      },
      {
        path: 'assessments/:id',
        loadComponent: () =>
          import('./features/operator-assessment/pages/assessment-detail.page').then((m) => m.AssessmentDetailPage),
      },
      {
        path: 'job-requests',
        canActivate: [roleGuard(Roles.Manager, Roles.Coordinator)],
        loadComponent: () =>
          import('./features/job-management/pages/job-requests.page').then((m) => m.JobRequestsPage),
      },
      {
        path: 'job-orders',
        loadComponent: () =>
          import('./features/job-management/pages/job-orders.page').then((m) => m.JobOrdersPage),
      },
      {
        path: 'timesheets',
        loadComponent: () =>
          import('./features/job-management/pages/timesheets.page').then((m) => m.TimesheetsPage),
      },
      {
        path: 'surveys',
        loadComponent: () =>
          import('./features/job-management/pages/surveys.page').then((m) => m.SurveysPage),
      },
      {
        path: 'reports',
        canActivate: [roleGuard(Roles.Manager, Roles.Coordinator)],
        loadComponent: () =>
          import('./features/reports/reports.page').then((m) => m.ReportsPage),
      },
      {
        path: 'admin',
        canActivate: [roleGuard(Roles.Manager)],
        loadComponent: () =>
          import('./features/admin/pages/users-admin.page').then((m) => m.UsersAdminPage),
      },
      {
        path: 'audit',
        canActivate: [roleGuard(Roles.Manager)],
        loadComponent: () =>
          import('./features/audit/audit-log.page').then((m) => m.AuditLogPage),
      },
      {
        path: 'equipment/:id/history',
        loadComponent: () =>
          import('./features/equipment/pages/equipment-history.page').then((m) => m.EquipmentHistoryPage),
      },
      {
        path: 'certificates/:id/diff',
        loadComponent: () =>
          import('./features/certificates/pages/certificate-diff.page').then((m) => m.CertificateDiffPage),
      },
    ],
  },
  { path: '**', redirectTo: 'dashboard' },
];
