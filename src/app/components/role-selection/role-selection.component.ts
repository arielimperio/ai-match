import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute } from '@angular/router';
import { MatchmakingService } from '../../services/matchmaking.service';
import { ApiService } from '../../services/api.service';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { catchError, of } from 'rxjs';

@Component({
  selector: 'app-role-selection',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './role-selection.component.html',
  styleUrls: ['./role-selection.component.css']
})
export class RoleSelectionComponent {
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private service = inject(MatchmakingService);
  private apiService = inject(ApiService);
  private sanitizer = inject(DomSanitizer);
  dynamicLogo = signal<string | null>(null);
  sanitizedLogo = computed<SafeHtml | null>(() => {
    const logo = this.dynamicLogo();
    return logo ? this.sanitizer.bypassSecurityTrustHtml(logo) : null;
  });

  selectionTitle = signal<string>('Who are you?');
  selectionDescription = signal<string>('Select your role to get the most relevant matches during the event.');
  studentName = signal<string>('Student');
  studentDescription = signal<string>('Looking for internship, thesis project, or job');
  companyName = signal<string>('Company / Exhibitor');
  companyDescription = signal<string>('Looking for new talent and collaborations');

  constructor() {
    const companyId = this.route.snapshot.queryParams['companyId'];
    if (companyId) {
      this.loadSettings(companyId);
    }
  }

  loadSettings(companyId: string) {
    const keys = [
      'WelcomeLogo',
      'RoleSelectionTitle',
      'RoleSelectionDescription',
      'RoleStudentName',
      'RoleStudentDescription',
      'RoleCompanyName',
      'RoleCompanyDescription'
    ];

    keys.forEach(key => {
      this.apiService.getSetting(key, companyId).pipe(catchError(() => of(null))).subscribe(res => {
        if (res && res.value) {
          if (key === 'WelcomeLogo') this.dynamicLogo.set(res.value);
          else if (key === 'RoleSelectionTitle') this.selectionTitle.set(res.value);
          else if (key === 'RoleSelectionDescription') this.selectionDescription.set(res.value);
          else if (key === 'RoleStudentName') this.studentName.set(res.value);
          else if (key === 'RoleStudentDescription') this.studentDescription.set(res.value);
          else if (key === 'RoleCompanyName') this.companyName.set(res.value);
          else if (key === 'RoleCompanyDescription') this.companyDescription.set(res.value);
        }
      });
    });
  }

  selectRole(role: 'Student' | 'Company') {
    this.service.selectedRole.set(role);
    this.startSurvey();
  }

  private startSurvey() {
    const queryParams = this.route.snapshot.queryParams;
    const firstQ = this.service.questionIds()[0];
    
    if (firstQ) {
      this.router.navigate(['/question', firstQ], { queryParams });
    } else {
      this.router.navigate(['/profile'], { queryParams });
    }
  }
}
