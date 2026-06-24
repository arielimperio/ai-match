import { Injectable, signal, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Answers, Match, Step, ViewMode, QuestionContent } from '../models';
import { Observable, tap, switchMap, of, map } from 'rxjs';
import { ApiService } from './api.service';
import { AuthService } from './auth.service';
import { NotificationService } from './notification.service';
import { environment } from '../../environments/environment';

export const SYSTEM_ID = '11111111-1111-1111-1111-111111111111';

@Injectable({
  providedIn: 'root'
})
export class MatchmakingService {
  readonly SYSTEM_ID = SYSTEM_ID;
  // State Signals
  readonly step = signal<Step>('welcome');
  readonly view = signal<ViewMode>('user');
  readonly activeChat = signal<string | null>(null);
  readonly selectedRole = signal<'Student' | 'Company' | 'All'>('All');
  readonly companyId = computed(() => this.authService.getCompanyId());

  readonly answers = signal<Answers>({
    firstName: '',
    lastName: '',
    email: '',
    company: '',
    title: '',
    photo: null,
    dynamic: {},
    dynamicOther: {},
    hasAcceptedTerms: false
  });

  readonly content = signal<Record<string, QuestionContent>>({
    welcome: {
      id: 'welcome',
      title: "Welcome!",
      type: 'Welcome',
      description: `
        <p>Great that you want to join and create new contacts!</p>
        <p>Forget random mingling. Answer <strong>5 quick questions</strong> and our AI will match you with the people who provide the most value for your business. It takes less than a minute!</p>
        
        <p><strong>How it works:</strong></p>
        <ol>
          <li><strong>Answer 6 short questions</strong> now.</li>
          <li><strong>The AI does the work</strong> and analyzes all participants.</li>
          <li><strong>48 hours before the exhibition opens</strong> you will receive an email with contacts that are most valuable for you to meet!</li>
        </ol>

        <p><strong>Your security (GDPR)</strong></p>
        <ul>
          <li><strong>No contact details</strong> (email/mobile) are shared in the app – you control that yourself when you meet.</li>
          <li><strong>All information is deleted</strong> immediately after the exhibition ends.</li>
        </ul>

        <p><strong>Ready to find your next business partner?</strong></p>
      `,
      button: "Start matchmaking"
    }
  });

  // Derived: sorted list of regular question IDs, filtered by role
  readonly questionIds = computed(() => {
    const c = this.content();
    const role = this.selectedRole();
    
    return Object.keys(c)
      .filter(id => {
        if (id === 'welcome' || c[id].type === 'Profile') return false;
        
        // Role filtering logic
        const target = c[id].targetRole || 'All';
        if (target === 'All') return true;
        if (role === 'All') return true; // Show all if no role selected (e.g. admin preview)
        return target === role;
      })
      .sort((a, b) => (c[a].order ?? 0) - (c[b].order ?? 0));
  });

  // Profile Step Content (Configurable via Settings)
  readonly profileTitle = signal<string>('My details');
  readonly profileDescription = signal<string>('Check that your details are correct so others can reach you.');
  readonly profileShowRole = signal<boolean>(true);
  readonly profileShowCompany = signal<boolean>(true);
  readonly profileShowPhoto = signal<boolean>(true);
  readonly profilePrivacyTitle = signal<string>('Privacy & Deletion');
  readonly profilePrivacyText = signal<string>('I share my details with the organizers who carry out the matchmaking. The details are only used for this matchmaking platform and are deleted at the latest 1 week after the event. Please ensure to save your matches in good time.');
  readonly profileConsentText = signal<string>('I want to participate and agree that my details are processed according to the text below.');

  private sortIds(a: string, b: string): number {
    const numA = parseInt(a.replace('q', ''));
    const numB = parseInt(b.replace('q', ''));
    if (!isNaN(numA) && !isNaN(numB)) return numA - numB;
    return a.localeCompare(b);
  }

  private apiService = inject(ApiService);
  private authService = inject(AuthService);

  constructor() {
    // Note: Initial load might fail if companyId is missing (e.g. participant view without registration yet)
    // but components will re-trigger this if needed.
    const cid = this.authService.getCompanyId();
    if (cid) {
      this.loadQuestions(cid);
      this.loadProfileSettings(cid);
    }

    // Restore state from localStorage
    const savedRegId = localStorage.getItem('registrationId');
    if (savedRegId) this.currentRegistrationId.set(savedRegId);

    const savedPartId = localStorage.getItem('participantId');
    if (savedPartId) this.currentParticipantId.set(savedPartId);
  }

  loadRegistration(id: string) {
    this.apiService.getRegistration(id).subscribe({
      next: (reg) => {
        if (reg) {
          this.answers.update(a => ({
            ...a,
            firstName: reg.firstname || '',
            lastName: reg.lastname || '', // Corrected lowercase 'n'
            email: reg.email || '',
            company: reg.organization || '',
            title: reg.title || ''
          }));
          // Also set the current registration ID
          this.currentRegistrationId.set(reg.id);
          localStorage.setItem('registrationId', reg.id);

          // Now we have companyId, re-load content
          const cid = reg.CompanyId || reg.companyId;
          if (cid) {
            localStorage.setItem('companyId', cid);
            this.loadQuestions(cid);
            this.loadProfileSettings(cid);
          }
        }
      },
      error: (err) => console.error('Failed to load registration', err)
    });
  }

  readonly isLoadingQuestions = signal<boolean>(false);

  loadQuestions(companyId?: string) {
    const cid = companyId || this.authService.getCompanyId();
    if (!cid) return;

    this.isLoadingQuestions.set(true);
    this.applyBranding(cid);
    this.apiService.getQuestions(cid).subscribe({
      next: (questions) => {
        // Reset content to only welcome step before adding new ones
        const newContent: Record<string, QuestionContent> = {
          welcome: this.content()['welcome']
        };

        questions.forEach(q => {
          newContent[q.id] = {
            id: q.id,
            title: q.title,
            description: q.description,
            type: q.type,
            placeholder: q.placeholder,
            order: q.order,
            maxLength: q.maxLength,
            targetRole: q.targetRole,
            options: q.options?.map((o: any) => ({
              id: o.value || (o.id ? o.id.toString() : o.title),
              icon: o.icon || '✦',
              title: o.title,
              desc: o.description || '',
              order: o.order
            })).sort((a: any, b: any) => (a.order ?? 0) - (b.order ?? 0))
          };
        });

        this.content.set(newContent);
        this.isLoadingQuestions.set(false);
      },
      error: (err) => {
        console.error('Failed to load questions', err);
        this.isLoadingQuestions.set(false);
      }
    });
  }

  loadProfileSettings(companyId?: string) {
    const cid = companyId || this.authService.getCompanyId();
    if (!cid) return;

    this.apiService.getSetting('ProfileTitle', cid).subscribe((res: any) => {
      if (res && res.value) this.profileTitle.set(res.value);
    });
    this.apiService.getSetting('ProfileDescription', cid).subscribe((res: any) => {
      if (res && res.value) this.profileDescription.set(res.value);
    });
    this.apiService.getSetting('ProfileShowRole', cid).subscribe((res: any) => {
      this.profileShowRole.set(res?.value !== 'false');
    });
    this.apiService.getSetting('ProfileShowCompany', cid).subscribe((res: any) => {
      this.profileShowCompany.set(res?.value !== 'false');
    });
    this.apiService.getSetting('ProfileShowPhoto', cid).subscribe((res: any) => {
      this.profileShowPhoto.set(res?.value !== 'false');
    });
    this.apiService.getSetting('ProfilePrivacyTitle', cid).subscribe((res: any) => {
      if (res && res.value) this.profilePrivacyTitle.set(res.value);
    });
    this.apiService.getSetting('ProfilePrivacyText', cid).subscribe((res: any) => {
      if (res && res.value) this.profilePrivacyText.set(res.value);
    });
    this.apiService.getSetting('ProfileConsentText', cid).subscribe((res: any) => {
      if (res && res.value) this.profileConsentText.set(res.value);
    });

    this.applyBranding(cid);
  }

  applyBranding(companyId?: string) {
    const cid = companyId || this.authService.getCompanyId() || this.SYSTEM_ID;

    // Helper to get setting with fallback to SYSTEM_ID
    const getWithFallback = (key: string) => {
      return this.apiService.getSetting(key, cid).pipe(
        switchMap((res: any) => {
          if (res && res.value) return of(res.value);
          if (cid === this.SYSTEM_ID) return of(null);
          return this.apiService.getSetting(key, this.SYSTEM_ID).pipe(map((s: any) => s?.value));
        })
      );
    };

    getWithFallback('BrandingPrimaryColor').subscribe(val => {
      if (val) {
        document.documentElement.style.setProperty('--primary', val);
        document.documentElement.style.setProperty('--accent', val);
        document.documentElement.style.setProperty('--green', val);
        const glow = this.hexToRgba(val, 0.4);
        if (glow) document.documentElement.style.setProperty('--primary-glow', glow);
      }
    });

    getWithFallback('BrandingSecondaryColor').subscribe(val => {
      if (val) document.documentElement.style.setProperty('--secondary', val);
    });

    getWithFallback('BrandingBackgroundType').subscribe(type => {
      const bgType = type || 'color';
      document.documentElement.style.setProperty('--bg-type', bgType);
      
      if (bgType === 'image') {
        getWithFallback('BrandingBackgroundImage').subscribe(img => {
          if (img) document.documentElement.style.setProperty('--bg-image', `url(${img})`);
          else document.documentElement.style.setProperty('--bg-image', 'none');
        });
      } else {
        document.documentElement.style.setProperty('--bg-image', 'none');
      }
    });

    getWithFallback('BrandingBackgroundColor').subscribe(val => {
      document.documentElement.style.setProperty('--bg-color', val || '#002a37');
    });
  }

  private hexToRgba(hex: string, alpha: number): string | null {
    if (!hex) return null;
    let h = hex.replace('#', '');
    if (h.length === 3) {
      h = h.split('').map(c => c + c).join('');
    }
    if (h.length !== 6) return null;
    const r = parseInt(h.slice(0, 2), 16);
    const g = parseInt(h.slice(2, 4), 16);
    const b = parseInt(h.slice(4, 6), 16);
    if (isNaN(r) || isNaN(g) || isNaN(b)) return null;
    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
  }

  // Actions
  setStep(newStep: Step) {
    this.step.set(newStep);
  }

  toggleView() {
    this.view.update(v => v === 'user' ? 'admin' : 'user');
  }

  updateProfile(field: keyof Answers, val: any) {
    this.answers.update(a => ({ ...a, [field]: val }));
  }

  updateOther(field: string, val: string) {
    this.answers.update(a => {
      const dynamicOther = { ...a.dynamicOther, [field]: val };
      return { ...a, dynamicOther };
    });
  }

  selectOption(qId: string, optId: string) {
    const qContent = this.content()[qId];
    if (!qContent) return;

    this.answers.update(a => {
      const dynamic = { ...a.dynamic };

      if (qContent.type === 'MultipleChoice') {
        let current = dynamic[qId] ? dynamic[qId].split(',') : [];
        const idx = current.indexOf(optId);
        if (idx > -1) {
          current = current.filter((t: string) => t !== optId);
        } else {
          // Enforce limit if set
          if (qContent.maxLength && current.length >= qContent.maxLength) {
            this.notify.warning(`You can select a maximum of ${qContent.maxLength} options.`);
            return a; // Ignore selection if limit reached
          }
          current = [...current, optId];
        }
        dynamic[qId] = current.join(',');
      } else {
        // Choice or Text
        dynamic[qId] = optId;
      }

      return { ...a, dynamic };
    });
  }

  resetAnswers() {
    this.answers.set({
      firstName: '',
      lastName: '',
      email: '',
      company: '',
      title: '',
      photo: null,
      dynamic: {},
      dynamicOther: {},
      hasAcceptedTerms: false
    });
  }

  isSelected(qId: string, optId: string): boolean {
    const a = this.answers();
    const val = a.dynamic[qId];
    if (!val) return false;

    const qContent = this.content()[qId];
    if (qContent?.type === 'MultipleChoice') {
      return val.split(',').includes(optId);
    }
    return val === optId;
  }

  handlePhotoUpload(file: File) {
    const reader = new FileReader();
    reader.onload = (e) => {
      const dataUrl = e.target?.result as string;
      this.compressImage(dataUrl, 400, 400, 0.7).then(compressed => {
        this.updateProfile('photo', compressed);
      });
    };
    reader.readAsDataURL(file);
  }

  private compressImage(dataUrl: string, maxWidth: number, maxHeight: number, quality: number): Promise<string> {
    return new Promise((resolve) => {
      const img = new Image();
      img.onload = () => {
        let width = img.width;
        let height = img.height;

        if (width > height) {
          if (width > maxWidth) {
            height *= maxWidth / width;
            width = maxWidth;
          }
        } else {
          if (height > maxHeight) {
            width *= maxHeight / height;
            height = maxHeight;
          }
        }

        const canvas = document.createElement('canvas');
        canvas.width = width;
        canvas.height = height;
        const ctx = canvas.getContext('2d');
        ctx?.drawImage(img, 0, 0, width, height);
        resolve(canvas.toDataURL('image/jpeg', quality));
      };
      img.src = dataUrl;
    });
  }


  // Admin Actions
  importRegistrations() {
    const count = Math.floor(Math.random() * 50) + 10;
    this.notify.info(`Importing ${count} new registrations from the booking system...`);
    setTimeout(() => this.notify.success('Import ready!'), 1000);
  }

  notify = inject(NotificationService);

  // API Interaction
  private http = inject(HttpClient);
  // Backend is running on HTTP 5109
  private apiUrl = `${environment.apiUrl}/Registrations`;

  // State for current user ids
  readonly currentRegistrationId = signal<string | null>(null);
  readonly currentParticipantId = signal<string | null>(null);

  submitRegistration(companyId?: string) {
    const answers = this.answers();
    const dynamicAnswers = { ...answers.dynamic };
    if (this.selectedRole() !== 'All') {
      dynamicAnswers['system_role'] = this.selectedRole();
    }

    const registration = {
      firstName: answers.firstName,
      lastName: answers.lastName,
      organization: answers.company,
      title: answers.title || '',
      email: answers.email,
      photo: answers.photo,
      hasAcceptedTerms: answers.hasAcceptedTerms,
      // Pass dynamic answers to updated backend
      answers: dynamicAnswers,
      otherAnswers: answers.dynamicOther
    };

    let url = this.apiUrl;
    const cid = companyId || this.authService.getCompanyId() || localStorage.getItem('companyId');
    if (cid) {
      url += `?companyId=${cid}`;
    }

    return this.http.post<any>(url, registration).pipe(
      tap(response => {
        if (response && response.id) {
          this.currentRegistrationId.set(response.id);
          localStorage.setItem('registrationId', response.id.toString());
        }
        if (response && response.participantId) {
          this.currentParticipantId.set(response.participantId);
          localStorage.setItem('participantId', response.participantId.toString());
        }
      })
    );
  }

  getMatches(participantId: string) {
    return this.http.get<Match[]>(`${this.apiUrl}/participant/${participantId}/matches`);
  }

  getParticipant(participantId: string) {
    return this.apiService.getParticipant(participantId);
  }

  setInterest(targetId: string) {
    const sourceId = this.currentParticipantId();
    if (!sourceId) return;

    return this.http.post(`${this.apiUrl.replace('Registrations', 'Matches')}/interest`, {
      sourceId,
      targetId
    });
  }

  removeInterest(targetId: string) {
    const sourceId = this.currentParticipantId();
    if (!sourceId) return;

    return this.http.post(`${this.apiUrl.replace('Registrations', 'Matches')}/interest/undo`, {
      sourceId,
      targetId
    });
  }

  setFeedback(matchId: string, rating: number, comment?: string) {
    const sourceId = this.currentParticipantId();
    if (!sourceId) return;

    return this.http.post(`${this.apiUrl.replace('Registrations', 'Matches')}/feedback`, {
      matchId,
      sourceId,
      rating,
      comment
    });
  }

  getChat(matchId: string) {
    const participantId = this.currentParticipantId();
    const params = participantId ? `?participantId=${participantId}` : '';
    return this.http.get<any[]>(`${this.apiUrl.replace('Registrations', 'Matches')}/${matchId}/chat${params}`);
  }

  sendMessage(matchId: string, content: string) {
    const senderId = this.currentParticipantId();
    if (!senderId) return;

    return this.http.post(`${this.apiUrl.replace('Registrations', 'Matches')}/${matchId}/chat?participantId=${senderId}`, {
      senderId,
      content
    });
  }

  // Schedule API Methods
  getCompanySlots(eventId: string, companyParticipantId: string) {
    return this.http.get<any[]>(`${environment.apiUrl}/Schedule/event/${eventId}/company/${companyParticipantId}/slots`);
  }

  generateSlots(eventId: string, companyParticipantId: string) {
    return this.http.post<any[]>(`${environment.apiUrl}/Schedule/generate/event/${eventId}/company/${companyParticipantId}`, {});
  }

  assignStudentToSlot(slotId: string, studentId: string) {
    return this.http.put(`${environment.apiUrl}/Schedule/slots/${slotId}/assign/${studentId}`, {});
  }

  toggleSlotAvailability(slotId: string, isAvailable: boolean) {
    return this.http.put(`${environment.apiUrl}/Schedule/slots/${slotId}/availability`, isAvailable);
  }

  markNoShow(slotId: string, isNoShow: boolean) {
    return this.http.put(`${environment.apiUrl}/Schedule/slots/${slotId}/noshow`, isNoShow);
  }

  studentDeclineSlot(slotId: string) {
    return this.http.put(`${environment.apiUrl}/Schedule/slots/${slotId}/decline`, {});
  }

  studentCheckIn(slotId: string) {
    return this.http.put(`${environment.apiUrl}/Schedule/slots/${slotId}/checkin`, {});
  }

  unassignHelpdeskStudentFromSlot(slotId: string) {
    return this.http.put(`${environment.apiUrl}/Schedule/slots/${slotId}/unassign`, {});
  }
}
