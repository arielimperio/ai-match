import { Component, inject, signal } from '@angular/core';
import { Router, RouterOutlet, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs/operators';
import { AuthService } from './services/auth.service';
import { MatchmakingService, SYSTEM_ID } from './services/matchmaking.service';
import { NotificationComponent } from './components/shared/notification/notification.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, NotificationComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent {
  private router = inject(Router);
  public authService = inject(AuthService);
  private matchmakingService = inject(MatchmakingService);
  
  isAdminView = signal(false);
  isSelectCompanyView = signal(false);
  showToggle = signal(true);

  constructor() {
    this.router.events.pipe(
      filter(e => e instanceof NavigationEnd)
    ).subscribe((val: any) => {
      const url = val.url;
      const isAdmin = url.includes('admin');
      const isMatches = url.includes('matches');
      const isSelect = url.includes('select-event');
      const isAuthPage = url.includes('login') || url.includes('setup') || url.includes('forgot-password') || url.includes('reset-password');
      
      const isSettings = url.includes('admin/settings');
      const isQuestions = url.includes('admin/questions');
      
      this.isAdminView.set(isAdmin || isSelect);
      this.isSelectCompanyView.set(isSelect);
      this.showToggle.set(!isSettings && !isQuestions);

      // Toggle Admin Mode
      if (isAdmin || isSelect) {
        document.body.classList.add('admin-mode');
      } else {
        document.body.classList.remove('admin-mode');
      }

      // Toggle Survey Mode (Background Image)
      // Restricted to public or survey pages only (not admin, matches, select-project, or auth pages)
      if (!isAdmin && !isMatches && !isSelect && !isAuthPage) {
        document.body.classList.add('survey-mode');
      } else {
        document.body.classList.remove('survey-mode');
      }
      // Apply Branding
      if (isSelect) {
        // Always force system branding on the project selection dashboard
        this.matchmakingService.applyBranding(SYSTEM_ID);
      } else {
        const cid = this.authService.getCompanyId();
        if (cid) {
          this.matchmakingService.applyBranding(cid);
        } else {
          const qParams = this.router.parseUrl(val.url).queryParams;
          const qid = qParams['companyId'] || qParams['id'];
          if (qid) {
            this.matchmakingService.applyBranding(qid as string);
          } else {
            this.matchmakingService.applyBranding(SYSTEM_ID); // Global fallback
          }
        }
      }
    });
  }

  toggleView() {
    if (this.isAdminView()) {
      const companyId = this.authService.getCompanyId();
      if (companyId) {
        this.router.navigate(['/'], { queryParams: { companyId: companyId } });
      } else {
        this.router.navigate(['/']);
      }
    } else {
      this.router.navigate(['/admin']);
    }
  }

  switchAccount() {
    this.router.navigate(['/select-event']);
  }

  isAuthorizedForToggle(): boolean {
    if (!this.authService.isLoggedIn()) return false;
    const role = this.authService.getUserRole();
    return role === 'Admin' || role === 'SuperAdmin';
  }
}
