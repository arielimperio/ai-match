import { Component, inject, signal, computed, OnDestroy, OnInit } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../services/api.service';
import { MatchmakingService } from '../../../services/matchmaking.service';
import { AuthService } from '../../../services/auth.service';
import { NotificationService } from '../../../services/notification.service';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { catchError, of } from 'rxjs';
import { RichTextEditorComponent } from '../../shared/rich-text-editor/rich-text-editor.component';

@Component({
  selector: 'app-admin-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, RichTextEditorComponent],
  templateUrl: './admin-settings.component.html',
  styleUrl: './admin-settings.component.css'
})
export class AdminSettingsComponent implements OnInit, OnDestroy {
  private apiService = inject(ApiService);
  private matchmakingService = inject(MatchmakingService);
  private authService = inject(AuthService);
  private notify = inject(NotificationService);
  private sanitizer = inject(DomSanitizer);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  public userRole = signal<string | null>(this.authService.getUserRole());

  settings = signal({
    WelcomeLogo: '',
    LogoType: 'svg',
    WelcomeTitle: '',
    WelcomeTagline: '',
    WelcomeDescription: '',
    WelcomeButton: '',
    SurveyOpen: 'true',
    ProfileTitle: '',
    ProfileDescription: '',
    InvitationEmailSubject: 'Maximize your networking at Nacka Företagarträffen - try our new AI matchmaking!',
    InvitationEmailTemplate: `
      <div style='font-family: sans-serif; max-width: 600px; line-height: 1.6;'>
          <p>Hi {{ParticipantName}}!</p>
          <p>Nacka Företagarträff has always been about creating meetings that lead to real business value. This year, we are taking the next step to make your participation even more effective.</p>
          <p>Together with <strong>ITmaskinen</strong>, who have been helping companies stay ahead for 25 years, we are now introducing <strong>Träffpunkten</strong> – an entirely new AI-based matchmaking service.</p>
          
          <p><strong>Why try AI matchmaking?</strong> Instead of hoping to bump into the right person by the coffee machine, we use intelligent technology to pair you with the attendees, exhibitors, or partners that best match your needs and interests.</p>
          <p><strong>Your oasis on the exhibition floor: Träffpunkten's Lounge</strong><br>
          When you agree to participate in matchmaking, you get exclusive access to a dedicated area right in the middle of the event. In the lounge, you can:</p>
          <ul style='margin-bottom: 20px;'>
              <li><strong>Hold your meetings:</strong> A quieter place dedicated for your AI matched contacts.</li>
              <li><strong>Charge your mobile:</strong> We know the battery drains fast during an intensive exhibition day – we have charging stations ready.</li>
              <li><strong>Take a break:</strong> We treat you to coffee and give you space to land between visits.</li>
          </ul>
          <p><strong>Here is how you do it:</strong> It takes less than a minute to get started. Click the link below and briefly state what you offer and what you are looking for at this year's event.</p>
          
          <p style='margin: 30px 0;'>
              👉 <a href='{{MatchmakingLink}}' style='color: #6366f1; font-weight: bold; text-decoration: underline;'>Yes, I want to find the right contacts via AI!</a>
          </p>
          <p>See you at Nacka Företagarträff – where traditional networking meets tomorrow's technology!</p>
          <p>Warm regards,<br>
          <strong>Management Nacka Företagarträff</strong> <em>in collaboration with ITmaskinen</em></p>
      </div>`,
    ResultEmailSubject: 'Your AI matches for Nacka Företagarträffen are here!',
    ResultEmailTemplate: `
      <div style='font-family: sans-serif; max-width: 600px; line-height: 1.6;'>
          <p>Hi {{ParticipantName}}!</p>
          <p>Our AI has now finished working and analyzed the profiles to find the most relevant connections for you and your business. The result is now available!</p>
          
          <p><strong>See who you should meet:</strong> 👉 <a href='{{ResultsTable}}' style='color: #6366f1; font-weight: bold; text-decoration: underline;'>See my AI matches here</a></p>
          <p><strong>How it works:</strong></p>
          <p>To ensure the meeting is valuable for both parties, we apply a "double opt-in" principle:</p>
          
          <ol>
              <li><strong>Review:</strong> Look through the list of your suggested matches.</li>
              <li><strong>Show interest:</strong> Click "Show interest" for those you want to meet.</li>
          </ol>
          <p>3. <strong>Both say yes:</strong> Only when both sides have approved each other do we unlock the possibility to book a meeting time in our <strong>Innovation Lounge</strong>.</p>
          <p><strong>Important about your privacy:</strong> For your security, no contact details like email or phone number are shared via the platform. The purpose of AI matchmaking is to create the personal meeting – exchanging business cards and contact details happens on site when you meet over a coffee!</p>
          <p><strong>We need your feedback!</strong><br>
          When you have completed your meeting, we would love to know how it went. Was the matchmaking spot on? Your feedback helps us and ITmaskinen make networking even smarter in the future.</p>
          <p><strong>See you in the lounge?</strong> In ITmaskinen Innovation Lounge, you who participate in the matchmaking always have access to coffee and charging stations for your mobile.</p>
          <p>See you on the exhibition floor!<br>
          Warm regards,<br>
          <strong>Management Nacka Företagarträff</strong> <em>in collaboration with ITmaskinen</em></p>
      </div>`,
    SuccessMessage: '',
    QuestionBackButton: 'Back',
    QuestionNextButton: 'Next',
    QuestionSelectedText: 'selected',
    QuestionOtherLabel: 'Tell us more',
    QuestionOtherPlaceholder: 'Write your own answer here...',
    BrandingPrimaryColor: '#31a2ae',
    BrandingSecondaryColor: '#a60053',
    BrandingBackgroundType: 'color',
    BrandingBackgroundImage: '',
    BrandingBackgroundColor: '#002a37',
    RoleSelectionEnabled: 'false',
    RoleSelectionTitle: 'Who are you?',
    RoleSelectionDescription: 'Select your role to get the most relevant matches during the event.',
    RoleStudentName: 'Student',
    RoleStudentDescription: 'Looking for internship, thesis project, or job',
    RoleCompanyName: 'Company / Exhibitor',
    RoleCompanyDescription: 'Looking for new talent and collaborations',
    FeedbackEmailSubject: 'How was your matchmaking experience at Nacka Företagarträff?',
    FeedbackEmailTemplate: `
      <div style='font-family: sans-serif; line-height: 1.6;'>
          <h2>Hi {{ParticipantName}}!</h2>
          <p>We hope you had productive meetings and interesting conversations!</p>
          <p>We would love to hear your feedback on your matches and your overall matchmaking experience. This helps us make future events even better.</p>
          <p>Please click the link below to see your results and leave feedback on your matches:</p>
          <p style='margin: 20px 0;'>
              <a href='{{ResultsLink}}' style='color: #6366f1; text-decoration: underline; font-weight: bold;'>See results & leave feedback</a>
          </p>
          <p>Thank you for participating!</p>
          <p>Best regards,<br>The Nacka Företagarträff Team</p>
      </div>`,
    ProfileShowRole: 'true',
    ProfileShowCompany: 'true',
    ProfileShowPhoto: 'true',
    ProfilePrivacyTitle: 'Privacy & Deletion',
    ProfilePrivacyText: 'I share my details with the organizers who carry out the matchmaking. The details are only used for this matchmaking platform and are deleted at the latest 1 week after the event. Please ensure to save your matches in good time.',
    ProfileConsentText: 'I want to participate and agree that my details are processed according to the text below.'
  });

  isLoading = signal(true);
  logoType = signal<'svg' | 'image'>('svg');
  activeTab = signal<'design' | 'text' | 'roles' | 'data' | 'email' | 'admins' | 'schedule'>('design');
  isSaving = signal(false);

  scheduleSettings = signal({
    eventId: '',
    isActive: true,
    eventStartTime: '13:00',
    eventEndTime: '17:00',
    slotDurationMinutes: 30,
    breakStartTime: '',
    breakEndTime: ''
  });

  // Logic for Data Management
  isProcessing = signal(false);
  processingMessage = signal('');
  processingProgress = signal(0);
  private progressInterval: any;
  private csvSubscription: any;
  totalCount = signal(0);

  // Modal State
  showConfirmModal = signal(false);
  confirmTitle = signal('');
  confirmMessage = signal('');
  private confirmCallback: (() => void) | null = null;
  currentUserRole = signal<string | null>(null);

  // Admin Management State
  admins = signal<any[]>([]);
  showAdminModal = signal(false);
  isAdminSaving = signal(false);
  adminForm = signal({
    id: '',
    firstName: '',
    lastName: '',
    email: '',
    role: 'Admin',
    password: ''
  });
  isEditMode = signal(false);


  // Sanitized logo for the preview panel (bypasses Angular's SVG stripping)
  sanitizedPreviewLogo = computed<SafeHtml | null>(() => {
    const logo = this.settings().WelcomeLogo;
    return logo ? this.sanitizer.bypassSecurityTrustHtml(logo) : null;
  });

  constructor() {
    this.loadSettings();
    this.loadTotalCount();
    this.currentUserRole.set(this.authService.getUserRole());
    this.loadAdmins();
  }

  ngOnInit() {
    // Restore active tab from URL query param on load
    const tabParam = this.route.snapshot.queryParamMap.get('tab') as
      'design' | 'text' | 'roles' | 'data' | 'email' | 'admins' | 'schedule' | null;
    const validTabs: Array<'design' | 'text' | 'roles' | 'data' | 'email' | 'admins' | 'schedule'> =
      ['design', 'text', 'roles', 'data', 'email', 'admins', 'schedule'];
    if (tabParam && validTabs.includes(tabParam)) {
      this.activeTab.set(tabParam);
    }
  }

  switchTab(tab: 'design' | 'text' | 'roles' | 'data' | 'email' | 'admins' | 'schedule') {
    this.activeTab.set(tab);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab },
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
  }

  ngOnDestroy() {
    this.stopProgressPolling();
  }

  loadTotalCount() {
    const companyId = this.authService.getCompanyId();
    if (!companyId) return;

    this.apiService.getParticipantSummaries(1, 1).subscribe({
      next: (data: any) => this.totalCount.set(data.totalCount),
      error: (err) => console.error('Failed to load total count', err)
    });
  }


  loadSettings() {
    this.isLoading.set(true);
    const companyId = this.authService.getCompanyId();
    if (!companyId) {
      this.isLoading.set(false);
      return;
    }

    const keys = ['WelcomeLogo', 'LogoType', 'WelcomeTitle', 'WelcomeTagline', 'WelcomeDescription', 'WelcomeButton', 'SurveyOpen', 'ProfileTitle', 'ProfileDescription', 'InvitationEmailSubject', 'InvitationEmailTemplate', 'ResultEmailSubject', 'ResultEmailTemplate', 'FeedbackEmailSubject', 'FeedbackEmailTemplate', 'SuccessMessage', 'QuestionBackButton', 'QuestionNextButton', 'QuestionSelectedText', 'QuestionOtherLabel', 'QuestionOtherPlaceholder', 'BrandingPrimaryColor', 'BrandingSecondaryColor', 'BrandingBackgroundType', 'BrandingBackgroundImage', 'BrandingBackgroundColor', 'RoleSelectionEnabled', 'RoleSelectionTitle', 'RoleSelectionDescription', 'RoleStudentName', 'RoleStudentDescription', 'RoleCompanyName', 'RoleCompanyDescription', 'ProfileShowRole', 'ProfileShowCompany', 'ProfileShowPhoto', 'ProfilePrivacyTitle', 'ProfilePrivacyText', 'ProfileConsentText'];
    let loadedCount = 0;

    keys.forEach(key => {
      this.apiService.getSetting(key, companyId).pipe(catchError(() => of(null))).subscribe({
        next: (res: any) => {
          if (res && res.value) {
            this.settings.update(s => ({ ...s, [key]: res.value }));

            // Sync logoType signal from the saved setting
            if (key === 'LogoType') {
              this.logoType.set(res.value as 'svg' | 'image');
            }
          }
          loadedCount++;
          if (loadedCount === keys.length) {
            this.loadScheduleSettings();
          }
        },
        error: () => {
          loadedCount++;
          if (loadedCount === keys.length) {
            this.loadScheduleSettings();
          }
        }
      });
    });
  }

  loadScheduleSettings() {
    const companyId = this.authService.getCompanyId();
    if (!companyId) {
      this.isLoading.set(false);
      return;
    }
    this.apiService.getEventScheduleSettings(companyId).pipe(catchError(() => of(null))).subscribe({
      next: (res: any) => {
        if (res) {
          this.scheduleSettings.set({
            eventId: res.id,
            isActive: res.isActive !== undefined ? res.isActive : true,
            eventStartTime: res.eventStartTime || '13:00:00',
            eventEndTime: res.eventEndTime || '17:00:00',
            slotDurationMinutes: res.slotDurationMinutes || 30,
            breakStartTime: res.breakStartTime || '',
            breakEndTime: res.breakEndTime || ''
          });
        } else {
          // Defaults if not found
          this.scheduleSettings.update(s => ({ ...s, eventId: companyId }));
        }
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  saveScheduleSettings() {
    const companyId = this.authService.getCompanyId();
    if (!companyId) return;
    
    const s = this.scheduleSettings();
    const payload = {
      Id: companyId,
      CompanyId: companyId,
      IsActive: s.isActive,
      EventStartTime: s.eventStartTime,
      EventEndTime: s.eventEndTime,
      SlotDurationMinutes: s.slotDurationMinutes,
      BreakStartTime: s.breakStartTime || null,
      BreakEndTime: s.breakEndTime || null
    };

    this.isSaving.set(true);
    this.apiService.updateEventScheduleSettings(companyId, payload).subscribe({
      next: () => {
        this.notify.success('Schedule settings saved!');
        this.isSaving.set(false);
      },
      error: (err) => {
        console.error('Failed to save schedule settings', err);
        this.notify.error('Failed to save schedule settings.');
        this.isSaving.set(false);
      }
    });
  }

  toggleScheduleActive(checked: boolean) {
    this.scheduleSettings.update(s => ({ ...s, isActive: checked }));
  }

  setLogoType(type: 'svg' | 'image') {
    this.logoType.set(type);
    this.settings.update(s => ({ ...s, LogoType: type }));
  }

  onFileSelected(event: any) {
    const file = event.target.files[0];
    if (!file) return;

    const reader = new FileReader();

    // If it's an SVG, read as text.
    if (this.logoType() === 'svg') {
      if (file.type !== 'image/svg+xml') {
        this.notify.error('Please upload an SVG file.');
        return;
      }
      reader.onload = (e) => {
        const content = e.target?.result as string;
        this.settings.update(s => ({ ...s, WelcomeLogo: content }));
      };
      reader.readAsText(file);
    } else {
      // Image mode
      if (!file.type.match('image.*')) {
        this.notify.error('Please upload an image file (PNG, JPG).');
        return;
      }

      reader.onload = (e) => {
        const base64 = e.target?.result as string;
        // Wrap image in img tag
        const imgTag = `<img src="${base64}" alt="Logo">`;
        this.settings.update(s => ({ ...s, WelcomeLogo: imgTag }));
      };
      reader.readAsDataURL(file);
    }
  }

  onBackgroundImageSelected(event: any) {
    const file = event.target.files[0];
    if (!file) return;

    if (!file.type.match('image.*')) {
      this.notify.error('Please upload an image file (PNG, JPG).');
      return;
    }

    const reader = new FileReader();
    reader.onload = (e) => {
      const base64 = e.target?.result as string;
      this.settings.update(s => ({ ...s, BrandingBackgroundImage: base64 }));
    };
    reader.readAsDataURL(file);
  }

  onSurveyToggle(checked: boolean) {
    this.settings.update(s => ({ ...s, SurveyOpen: checked ? 'true' : 'false' }));
  }

  save() {
    const data = this.settings();
    let savedCount = 0;
    const keys = Object.keys(data) as Array<keyof typeof data>;

    this.notify.info('Saving settings...');

    const companyId = this.authService.getCompanyId();
    keys.forEach(key => {
      this.apiService.updateSetting(key, data[key], companyId || undefined).subscribe({
        next: () => {
          savedCount++;
          if (savedCount === keys.length) {
            this.notify.success('All settings saved!');
            this.authService.triggerLogoRefresh();
            if (companyId) this.matchmakingService.applyBranding(companyId);
          }
        },
        error: (err) => {
          console.error(`Failed to save ${key}`, err);
          this.notify.error(`Failed to save ${key}`);
        }
      });
    });
  }

  getPreviewHtml(template: string, type: 'invitation' | 'result' | 'feedback'): string {
    const dummyData = {
      ParticipantName: 'Anna Andersson',
      CompanyName: 'Tech Innovators AB',
      MatchmakingLink: 'https://matchmaking.itmaskinen.se?id=dummy123',
      ResultsTable: 'https://matchmaking.itmaskinen.se/matches?id=dummy123',
      ResultsLink: 'https://matchmaking.itmaskinen.se/matches?id=dummy123'
    };

    let html = template || '';
    if (type === 'invitation') {
      html = html.replace(/{{ParticipantName}}/g, dummyData.ParticipantName)
        .replace(/{{CompanyName}}/g, dummyData.CompanyName)
        .replace(/{{MatchmakingLink}}/g, dummyData.MatchmakingLink);
    } else if (type === 'result') {
      html = html.replace(/{{ParticipantName}}/g, dummyData.ParticipantName)
        .replace(/{{CompanyName}}/g, dummyData.CompanyName)
        .replace(/{{ResultsTable}}/g, dummyData.ResultsTable);
    } else if (type === 'feedback') {
      html = html.replace(/{{ParticipantName}}/g, dummyData.ParticipantName)
        .replace(/{{CompanyName}}/g, dummyData.CompanyName)
        .replace(/{{ResultsLink}}/g, dummyData.ResultsLink);
    }
    return html;
  }

  sendTestEmail(type: 'invitation' | 'result' | 'feedback') {
    const email = prompt('Enter email address to send the test to:');
    if (!email) return;

    this.isSaving.set(true);
    let template = '';
    let templateName = '';

    if (type === 'invitation') {
      template = this.settings().InvitationEmailTemplate;
      templateName = 'InvitationEmailTemplate';
    } else if (type === 'result') {
      template = this.settings().ResultEmailTemplate;
      templateName = 'ResultEmailTemplate';
    } else if (type === 'feedback') {
      template = this.settings().FeedbackEmailTemplate;
      templateName = 'FeedbackEmailTemplate';
    }

    const companyId = this.authService.getCompanyId();
    this.apiService.sendTestEmail({
      type: templateName,
      email: email,
      template: template
    }, companyId || undefined).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.notify.success('Test email sent!');
      },
      error: (err) => {
        this.isSaving.set(false);
        console.error('Failed to send test email', err);
        this.notify.error('Could not send test email.');
      }
    });
  }

  // Data Management Methods
  onCsvSelected(event: any) {
    const file: File = event.target.files[0];
    if (file) {
      if (!file.name.toLowerCase().endsWith('.csv')) {
        this.notify.warning('Only .csv files are allowed.');
        return;
      }

      this.isProcessing.set(true);
      this.processingMessage.set('Running AI matchmaking on CSV...');
      this.notify.info('Uploading and matching... this may take a while.');
      const companyId = this.authService.getCompanyId();
      this.startProgressPolling();

      this.csvSubscription = this.apiService.matchFromCsv(file, companyId || undefined).subscribe({
        next: (blob: Blob) => {
          this.notify.success('Matching clear! Downloading results...');
          const url = window.URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          link.download = `csv_matches_${new Date().getTime()}.xlsx`;
          link.click();
          window.URL.revokeObjectURL(url);
          this.isProcessing.set(false);
          this.processingMessage.set('');
          this.stopProgressPolling();
        },
        error: (err) => {
          console.error(err);
          this.notify.error('Failed to match from CSV. Check the file format.');
          this.isProcessing.set(false);
          this.processingMessage.set('');
          this.stopProgressPolling();
        }
      });
      event.target.value = '';
    }
  }

  onMatchesFileSelected(event: any) {
    const file: File = event.target.files[0];
    if (file) {
      const isCsv = file.name.toLowerCase().endsWith('.csv');
      const isXlsx = file.name.toLowerCase().endsWith('.xlsx');

      if (!isCsv && !isXlsx) {
        this.notify.warning('Only .csv and .xlsx files are allowed.');
        return;
      }

      this.isProcessing.set(true);
      this.processingMessage.set('Importing matches...');
      this.notify.info('Uploading matches...');

      const companyId = this.authService.getCompanyId();
      this.apiService.importMatches(file, companyId || undefined).subscribe({
        next: (res: any) => {
          this.notify.success(res.message);
          if (res.errors && res.errors.length > 0) {
            console.warn('Import completed with some errors:', res.errors);
            this.notify.warning(`Imported with ${res.errors.length} errors. See console.`);
          }
          this.isProcessing.set(false);
          this.processingMessage.set('');
        },
        error: (err) => {
          console.error(err);
          const errorMsg = err.error?.message || 'Failed to import matches. Check the file format.';
          this.notify.error(errorMsg);
          this.isProcessing.set(false);
          this.processingMessage.set('');
        }
      });
      event.target.value = '';
    }
  }

  onMapFileSelected(event: any) {
    const file: File = event.target.files[0];
    if (file) {
      if (!file.name.toLowerCase().endsWith('.csv')) {
        this.notify.warning('Only .csv files are allowed.');
        return;
      }

      this.isProcessing.set(true);
      this.processingMessage.set('Converting names to IDs...');
      this.notify.info('Processing CSV and matching participants...');

      const companyId = this.authService.getCompanyId();
      this.apiService.mapMatches(file, companyId || undefined).subscribe({
        next: (blob: Blob) => {
          const url = window.URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          link.download = `matches_mapped_${new Date().toISOString().slice(0, 10)}.csv`;
          link.click();
          window.URL.revokeObjectURL(url);
          this.notify.success('Conversion done! CSV downloaded with IDs and scores.');
          this.isProcessing.set(false);
          this.processingMessage.set('');
        },
        error: (err) => {
          console.error(err);
          const errorMsg = err.error?.message || 'Failed to convert CSV. Check the file format.';
          this.notify.error(errorMsg);
          this.isProcessing.set(false);
          this.processingMessage.set('');
        }
      });
      event.target.value = '';
    }
  }

  seedData() {
    this.openConfirm(
      'Create Test Data',
      'WARNING: This will clear participants and create new test data. Do you want to continue?',
      () => {
        this.notify.info('Creating test data...');
        this.isProcessing.set(true);
        this.processingMessage.set('Creating test data...');
        const companyId = this.authService.getCompanyId(); // Although seed might be global, let's pass it if available
        this.apiService.seedData().subscribe({
          next: (res: any) => {
            this.notify.success(res.message);
            this.loadTotalCount();
            this.isProcessing.set(false);
          },
          error: (err) => {
            console.error(err);
            this.notify.error('Failed to create test data.');
            this.isProcessing.set(false);
          }
        });
      }
    );
  }

  testMatching() {
    this.openConfirm(
      'Test Matching',
      'This generates matches in the database BUT does NOT send out any results. Do you want to continue?',
      () => {
        this.isProcessing.set(true);
        this.processingMessage.set('Generating test matches...');
        this.notify.info('Running matchmaking algorithm...');

        const companyId = this.authService.getCompanyId();
        this.apiService.generateMatches(companyId || undefined).subscribe({
          next: (res: any) => {
            this.startProgressPolling();
            // We don't have the same callback structure here as dashboard, 
            // but the polling will update the progress bar.
            this.notify.info('Matchmaking started. See progress below.');
          },
          error: (err) => {
            console.error(err);
            this.stopProgressPolling();
            this.notify.error('Failed to generate matches.');
            this.isProcessing.set(false);
          }
        });
      }
    );
  }

  resetMatches() {
    this.openConfirm(
      'Clear Matches',
      'WARNING: This will permanently remove all matches and chat messages. Do you want to continue?',
      () => {
        this.notify.info('Clearing matches...');
        this.isProcessing.set(true);
        this.processingMessage.set('Clearing matches...');
        const companyId = this.authService.getCompanyId();
        this.apiService.resetMatches(companyId || undefined).subscribe({
          next: (res: any) => {
            this.notify.success(res.message);
            this.isProcessing.set(false);
          },
          error: (err) => {
            console.error(err);
            this.notify.error('Failed to clear matches.');
            this.isProcessing.set(false);
          }
        });
      }
    );
  }

  exportRegistrations() {
    const companyId = this.authService.getCompanyId();
    if (!companyId) return;

    this.notify.info('Preparing export...');

    // Fetch questions to build dynamic headers and mapping for this project
    this.apiService.getQuestions(companyId).subscribe({
      next: (questions) => {
        const getQ = (id: string) => questions.find(q => q.id === id);
        const q1 = getQ('q1');
        const q2 = getQ('q2');
        const q3 = getQ('q3');
        const q4 = getQ('q4');
        const q6 = getQ('q6');

        const headers = [
          'ID', 'Name', 'Email', 'Company', 'Title',
          (q1?.title || 'q1 Superpower'),
          (q2?.title || 'q2 Challenge'),
          (q3?.title || 'q3 Topics'),
          (q4?.title || 'q4 About you'),
          (q6?.title || 'q6 About company'),
          'Status', 'Registered', 'Answered'
        ];

        // Dynamic mapper from question options
        const createOptionMap = (q?: any) => {
          const map: Record<string, string> = { 'other': 'Other...' };
          q?.options?.forEach((o: any) => {
            if (o.value && o.title) map[o.value] = o.title;
          });
          return map;
        };

        const mapper = {
          q1: createOptionMap(q1),
          q2: createOptionMap(q2),
          q3: createOptionMap(q3)
        };

        this.apiService.exportRegistrations(companyId || undefined).subscribe({
          next: (data: any[]) => {
            if (!data || data.length === 0) {
              this.notify.warning('No data to export.');
              return;
            }

            const mapValuesToTitles = (questionId: 'q1' | 'q2' | 'q3', values: string) => {
              if (!values) return '';
              const questionMapper = mapper[questionId];
              return values.split(',').map(v => questionMapper[v.trim()] || v.trim()).join(', ');
            };

            const csvContent = [
              headers.join(','),
              ...data.map(r => {
                const escape = (val: any) => {
                  if (val === null || val === undefined) return '';
                  const str = String(val);
                  if (str.includes(',') || str.includes('"') || str.includes('\n')) {
                    return `"${str.replace(/"/g, '""')}"`;
                  }
                  return str;
                };
                return [
                  escape(r.id), escape(r.name), escape(r.email), escape(r.company),
                  escape(r.title),
                  escape(mapValuesToTitles('q1', r.superpower)),
                  escape(mapValuesToTitles('q2', r.challenge)),
                  escape(mapValuesToTitles('q3', r.interests)),
                  escape(r.bio), escape(r.companyDescription),
                  escape(r.status), escape(r.registered), escape(r.answered)
                ].join(',');
              })
            ].join('\n');

            const blob = new Blob(['\uFEFF' + csvContent], { type: 'text/csv;charset=utf-8;' });
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.setAttribute('href', url);
            link.setAttribute('download', `participants_export_${new Date().toISOString().slice(0, 10)}.csv`);
            link.style.visibility = 'hidden';
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            this.notify.success(`Exported ${data.length} rows.`);
          },
          error: (err) => {
            console.error('Export failed', err);
            this.notify.error('Failed to export data.');
          }
        });
      },
      error: (err) => {
        console.error('Failed to load questions for export', err);
        this.notify.error('Failed to fetch company questions for export.');
      }
    });
  }

  exportMatchesExcel() {
    this.notify.info('Preparing match export...');
    const companyId = this.authService.getCompanyId();
    this.apiService.exportMatches(companyId || undefined).subscribe({
      next: (blob: Blob) => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.setAttribute('href', url);
        link.setAttribute('download', `matches_export_${new Date().toISOString().slice(0, 10)}.xlsx`);
        link.style.visibility = 'hidden';
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
        this.notify.success('Excel file downloaded!');
      },
      error: (err) => {
        console.error('Export failed', err);
        this.notify.error('Failed to export matches.');
      }
    });
  }

  startProgressPolling() {
    this.processingProgress.set(0);
    this.stopProgressPolling();
    const companyId = this.authService.getCompanyId();
    this.progressInterval = setInterval(() => {
      this.apiService.getMatchingProgress(companyId || undefined).subscribe({
        next: (data: any) => {
          const progress = Math.round(data.progress || 0);
          this.processingProgress.set(progress);

          if (!data.isActive && progress >= 100) {
            this.stopProgressPolling();
          }
        },
        error: (err: any) => console.error('Progress polling error', err)
      });
    }, 1000);
  }

  cancelProcessing() {
    if (this.csvSubscription) {
      this.csvSubscription.unsubscribe();
      this.csvSubscription = null;
    }
    const companyId = this.authService.getCompanyId();
    this.apiService.cancelMatching(companyId || undefined).subscribe({
      next: () => {
        this.notify.info('Matching cancelled.');
        this.stopProcessing();
      },
      error: (err) => {
        console.error('Failed to cancel matching', err);
        this.stopProcessing(); // Force stop UI anyway
      }
    });
  }

  private stopProcessing() {
    this.isProcessing.set(false);
    this.processingMessage.set('');
    this.processingProgress.set(0);
    this.stopProgressPolling();
  }

  private stopProgressPolling() {
    if (this.progressInterval) {
      clearInterval(this.progressInterval);
      this.progressInterval = null;
    }
  }

  openConfirm(title: string, message: string, onConfirm: () => void) {
    this.confirmTitle.set(title);
    this.confirmMessage.set(message);
    this.confirmCallback = onConfirm;
    this.showConfirmModal.set(true);
  }

  closeConfirm() {
    this.showConfirmModal.set(false);
    this.confirmCallback = null;
  }

  onConfirm() {
    if (this.confirmCallback) {
      this.confirmCallback();
    }
    this.closeConfirm();
  }

  previewSurvey() {
    const companyId = this.authService.getCompanyId();
    if (companyId) {
      window.open(`/?companyId=${companyId}`, '_blank');
    } else {
      window.open(`/`, '_blank');
    }
  }

  // Admin Management Logic
  loadAdmins() {
    const companyId = this.authService.getCompanyId();
    // Passing companyId explicitly to ensure multi-company users get the correct context
    this.apiService.getAdmins(companyId || undefined).subscribe({
      next: (data) => this.admins.set(data),
      error: (err) => {
        console.error('Failed to load admins', err);
        // If it's a 400 No company context found, we might want to show a warning
        if (err.status === 400) {
           this.notify.warning('Please select a company context to manage administrators.');
        }
      }
    });
  }

  openAddAdmin() {
    this.isEditMode.set(false);
    this.adminForm.set({
      id: '',
      firstName: '',
      lastName: '',
      email: '',
      role: 'Admin',
      password: ''
    });
    this.showAdminModal.set(true);
  }

  openEditAdmin(admin: any) {
    this.isEditMode.set(true);
    this.adminForm.set({
      id: admin.id,
      firstName: admin.firstName,
      lastName: admin.lastName,
      email: admin.email,
      role: admin.role,
      password: ''
    });
    this.showAdminModal.set(true);
  }

  closeAdminModal() {
    this.showAdminModal.set(false);
  }

  saveAdmin() {
    const form = this.adminForm();
    if (!form.firstName || !form.lastName || (!this.isEditMode() && !form.email)) {
      this.notify.warning('Please fill in all required fields.');
      return;
    }

    this.isAdminSaving.set(true);
    const companyId = this.authService.getCompanyId();

    if (this.isEditMode()) {
      this.apiService.updateAdmin(form.id, {
        firstName: form.firstName,
        lastName: form.lastName,
        role: form.role,
        password: form.password,
        companyId: companyId
      }).subscribe({
        next: () => {
          this.notify.success('Admin updated successfully!');
          this.loadAdmins();
          this.closeAdminModal();
          this.isAdminSaving.set(false);
        },
        error: (err) => {
          this.isAdminSaving.set(false);
          this.notify.error(err.error?.message || 'Failed to update admin.');
        }
      });
    } else {
      this.apiService.addAdmin({
        firstName: form.firstName,
        lastName: form.lastName,
        email: form.email,
        role: form.role,
        password: form.password,
        companyId: companyId
      }).subscribe({
        next: () => {
          this.notify.success('Admin added successfully!');
          this.loadAdmins();
          this.closeAdminModal();
          this.isAdminSaving.set(false);
        },
        error: (err) => {
          this.isAdminSaving.set(false);
          this.notify.error(err.error?.message || 'Failed to add admin.');
        }
      });
    }
  }

  removeAdmin(adminId: string) {
    this.openConfirm(
      'Remove Admin',
      'Are you sure you want to remove this admin from the project? They will lose all access to this company.',
      () => {
        const companyId = this.authService.getCompanyId();
        this.apiService.deleteAdmin(adminId, companyId || undefined).subscribe({
          next: () => {
            this.notify.success('Admin removed successfully.');
            this.loadAdmins();
          },
          error: (err) => {
            console.error(err);
            this.notify.error(err.error?.message || 'Failed to remove admin.');
          }
        });
      }
    );
  }

  switchAccount() {
    this.router.navigate(['/select-company']);
  }
}
