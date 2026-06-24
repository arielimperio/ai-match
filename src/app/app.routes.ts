import { Routes } from '@angular/router';
import { WelcomeComponent } from './components/welcome/welcome.component';
import { QuestionComponent } from './components/question/question.component';
import { TextQuestionComponent } from './components/text-question/text-question.component';
import { ProfileComponent } from './components/profile/profile.component';
import { SuccessComponent } from './components/success/success.component';
import { AdminComponent } from './components/admin/admin.component';
import { LoginComponent } from './components/login/login.component';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: '', component: WelcomeComponent },
  { path: 'setup', loadComponent: () => import('./components/system-setup/system-setup.component').then(m => m.SystemSetupComponent) },
  { path: 'login', component: LoginComponent },
  { path: 'forgot-password', loadComponent: () => import('./components/forgot-password/forgot-password.component').then(m => m.ForgotPasswordComponent) },
  { path: 'reset-password', loadComponent: () => import('./components/reset-password/reset-password.component').then(m => m.ResetPasswordComponent) },
  { path: 'role-selection', loadComponent: () => import('./components/role-selection/role-selection.component').then(m => m.RoleSelectionComponent) },
  { path: 'question/:id', component: QuestionComponent },
  { path: 'profile', component: ProfileComponent },
  { path: 'success', component: SuccessComponent },
  { path: 'matches', loadComponent: () => import('./components/match-results/match-results.component').then(m => m.MatchResultsComponent) },
  { path: 'select-event', loadComponent: () => import('./components/admin/company-selector/company-selector.component').then(m => m.CompanySelectorComponent), canActivate: [authGuard] },
  {
    path: 'admin',
    component: AdminComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', loadComponent: () => import('./components/admin/admin-dashboard/admin-dashboard.component').then(m => m.AdminDashboardComponent) },
      { path: 'questions', loadComponent: () => import('./components/admin/admin-questions/admin-questions.component').then(m => m.AdminQuestionsComponent) },
      { path: 'settings', loadComponent: () => import('./components/admin/admin-settings/admin-settings.component').then(m => m.AdminSettingsComponent) }
    ]
  },
  { path: '**', redirectTo: '' }
];
