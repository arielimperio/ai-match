import { Component, inject, computed, signal } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { MatchmakingService } from '../../services/matchmaking.service';
import { ApiService } from '../../services/api.service';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { catchError, of } from 'rxjs';

@Component({
  selector: 'app-welcome',
  standalone: true,
  templateUrl: './welcome.component.html',
  styleUrls: ['./welcome.component.css']
})
export class WelcomeComponent {
  private router = inject(Router);
  public service = inject(MatchmakingService);
  public isLoadingQuestions = this.service.isLoadingQuestions;
  private apiService = inject(ApiService);
  private route = inject(ActivatedRoute);
  private sanitizer = inject(DomSanitizer);

  // Fallback defaults from service
  defaultContent = computed(() => this.service.content()['welcome']);

  // Dynamic overrides from admin settings
  dynamicLogo = signal<string | null>(null);
  dynamicTitle = signal<string | null>(null);
  dynamicTagline = signal<string | null>(null);
  dynamicDescription = signal<string | null>(null);
  dynamicButton = signal<string | null>(null);

  // Participant first name from ?id= query param
  participantFirstName = signal<string | null>(null);

  // Sanitized logo for safe innerHTML rendering (bypasses Angular's SVG stripping)
  sanitizedLogo = computed<SafeHtml | null>(() => {
    const logo = this.dynamicLogo();
    return logo ? this.sanitizer.bypassSecurityTrustHtml(logo) : null;
  });

  // Personalized title: substitutes {name} in dynamic title, or builds default greeting
  welcomeTitle = computed(() => {
    const firstName = this.participantFirstName();
    const base = this.dynamicTitle() || '';

    if (firstName) {
      if (base && base.includes('{name}')) {
        return base.replace('{name}', firstName);
      }
      return `Welcome, ${firstName}!`;
    }
    return base;
  });

  isSurveyOpen = signal<boolean>(true);
  isRoleSelectionEnabled = signal<boolean>(false);
  showClosedAlert = signal<boolean>(false);

  constructor() {
    this.route.queryParams.subscribe(params => {
      const companyId = params['companyId'];
      const regId = params['id'];

      if (companyId) {
        localStorage.setItem('companyId', companyId);
      }

      if (!companyId && !regId) {
        this.router.navigate(['/login']);
        return;
      }

      if (regId) {
        this.service.loadRegistration(regId);
        this.apiService.getRegistration(regId).pipe(catchError(() => of(null))).subscribe((reg: any) => {
          if (reg) {
            const name = reg.Firstname || reg.firstname || reg.firstName || '';
            if (name.trim()) this.participantFirstName.set(name.trim());
            
            const cid = reg.CompanyId || reg.companyId;
            if (cid) {
              this.loadDynamicSettings(cid);
              this.service.loadQuestions(cid);
            }
          }
        });
      } else if (companyId) {
        this.loadDynamicSettings(companyId);
        this.service.loadQuestions(companyId);
      } else {
        // Fallback for dev or generic
        this.loadDynamicSettings();
        this.service.loadQuestions();
      }
    });
  }

  loadDynamicSettings(companyId?: string) {
    this.apiService.getSetting('WelcomeLogo', companyId).pipe(catchError(() => of(null))).subscribe(s => {
      if (s && s.value) this.dynamicLogo.set(s.value);
    });
    this.apiService.getSetting('WelcomeTitle', companyId).pipe(catchError(() => of(null))).subscribe(s => {
      if (s && s.value) this.dynamicTitle.set(s.value);
    });
    this.apiService.getSetting('WelcomeTagline', companyId).pipe(catchError(() => of(null))).subscribe(s => {
      if (s && s.value) this.dynamicTagline.set(s.value);
    });
    this.apiService.getSetting('WelcomeDescription', companyId).pipe(catchError(() => of(null))).subscribe(s => {
      if (s && s.value) this.dynamicDescription.set(s.value);
    });
    this.apiService.getSetting('WelcomeButton', companyId).pipe(catchError(() => of(null))).subscribe(s => {
      if (s && s.value) this.dynamicButton.set(s.value);
    });
    this.apiService.getSetting('SurveyOpen', companyId).pipe(catchError(() => of(null))).subscribe({
      next: (s: any) => {
        if (s && s.value) this.isSurveyOpen.set(s.value.toLowerCase() === 'true');
      }
    });
    this.apiService.getSetting('RoleSelectionEnabled', companyId).pipe(catchError(() => of(null))).subscribe({
      next: (s: any) => {
        if (s && s.value) this.isRoleSelectionEnabled.set(s.value.toLowerCase() === 'true');
      }
    });
  }

  start() {
    if (!this.isSurveyOpen()) {
      this.showClosedAlert.set(true);
      return;
    }
    const id = this.route.snapshot.queryParams['id'];
    const companyId = this.route.snapshot.queryParams['companyId'];
    const queryParams: any = {};
    if (id) queryParams.id = id;
    if (companyId) queryParams.companyId = companyId;

    const firstQ = this.service.questionIds()[0];
    if (this.isRoleSelectionEnabled()) {
      this.router.navigate(['/role-selection'], { queryParams });
    } else if (firstQ) {
      this.router.navigate(['/question', firstQ], { queryParams });
    } else {
      // Fallback if no regular questions exist (e.g. only Profile questions)
      this.router.navigate(['/profile'], { queryParams });
    }
  }

  closeAlert() {
    this.showClosedAlert.set(false);
  }
}
