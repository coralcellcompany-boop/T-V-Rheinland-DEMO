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
        path: 'admin',
        canActivate: [roleGuard(Roles.Manager)],
        loadComponent: () =>
          import('./features/admin/pages/users-admin.page').then((m) => m.UsersAdminPage),
      },
    ],
  },
  { path: '**', redirectTo: 'dashboard' },
];
