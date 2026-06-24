import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { AuthService } from '../../../services/auth.service';
import { ApiService } from '../../../services/api.service';
import { MatchmakingService, SYSTEM_ID } from '../../../services/matchmaking.service';
import { Router, ActivatedRoute } from '@angular/router';
import { NotificationService } from '../../../services/notification.service';
import { catchError, of } from 'rxjs';
import { QuillModule } from 'ngx-quill';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

@Component({
  selector: 'app-company-selector',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, QuillModule],
  templateUrl: './company-selector.component.html',
  styleUrls: ['./company-selector.component.css']
})
export class CompanySelectorComponent implements OnInit {
  private authService = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private notify = inject(NotificationService);
  private fb = inject(FormBuilder);
  private apiService = inject(ApiService);
  private matchmakingService = inject(MatchmakingService);
  private sanitizer = inject(DomSanitizer);

  companies = signal<any[]>([]);
  searchQuery = signal('');
  
  filteredCompanies = computed(() => {
    const q = this.searchQuery().toLowerCase().trim();
    if (!q) return this.companies();
    return this.companies().filter(c => c.name.toLowerCase().includes(q));
  });

  totalParticipantsCount = computed(() =>
    this.companies().reduce((sum: number, c: any) => sum + (c.participantCount || 0), 0)
  );

  activeCompaniesCount = computed(() =>
    this.companies().filter((c: any) => c.isVerified !== false).length
  );

  pendingCompaniesCount = computed(() =>
    this.companies().filter((c: any) => c.isVerified === false).length
  );

  loading = true;
  currentUser: any = null;
  
  activeTab = signal<'overview' | 'ai' | 'email' | 'emailTemplates' | 'branding'>('overview');
  showSecurityModal = signal(false);
  passwordForm: FormGroup;
  isUpdatingPassword = false;

  // New Event creation signals
  showCreateEventModal = signal(false);
  isCreatingEvent = signal(false);
  isGeneratingRecommendation = signal(false);
  aiRecommendation = signal<any>(null);
  newEventData = signal({
    projectName: '',
    city: '',
    description: '',
    roleSelectionEnabled: false,
    roles: [] as any[],
    questions: [] as any[],
    emailTemplate: '',
    resultEmailTemplate: '',
    feedbackEmailTemplate: '',
    brandingPrimaryColor: '',
    brandingSecondaryColor: '',
    brandingBackgroundType: '',
    brandingBackgroundColor: '',
    welcomeTitle: '',
    welcomeTagline: '',
    welcomeDescription: '',
    welcomeButton: ''
  });

  // System Settings
  systemSettings = signal({
    AiProvider: 'OpenAI',
    AiModel: 'gpt-4o',
    AiApiKey: '',
    SendGridApiKey: '',
    SendGridFromEmail: '',
    EmailFromName: '',
    VerificationEmailSubject: '',
    VerificationEmailHtmlBody: '',
    PasswordResetEmailSubject: '',
    PasswordResetEmailHtmlBody: '',
    BrandingPrimaryColor: '#31a2ae',
    BrandingSecondaryColor: '#a60053',
    BrandingBackgroundType: 'color',
    BrandingBackgroundImage: '',
    BrandingBackgroundColor: '#002a37'
  });
  
  activeTemplateType = signal<'verification' | 'reset'>('verification');
  isLoadingSettings = signal(false);
  isSavingSettings = signal(false);
  isSendingTest = signal(false);
  readonly SYSTEM_ID = '11111111-1111-1111-1111-111111111111';

  readonly DEFAULT_VERIFICATION_TEMPLATE = `
<div style="font-family: sans-serif; color: #333; line-height: 1.6; max-width: 600px;">
    <h2 style="color: #1e293b; margin-top: 0;">Welcome to Nacka Matchmaking</h2>
    <p>Thank you for registering. Please verify your account by clicking the link below:</p>
    <p style="margin: 20px 0;">
        <a href="{{VerificationLink}}" style="color: #3b82f6; font-weight: bold; text-decoration: underline;">Verify Account</a>
    </p>
    <p>If the link above doesn't work, you can copy and paste this URL into your browser:</p>
    <p style="word-break: break-all; color: #64748b; font-size: 13px;">{{VerificationLink}}</p>
    <hr style="border: 0; border-top: 1px solid #e2e8f0; margin: 30px 0;">
    <p style="color: #64748b; font-size: 12px;">If you did not request this, please ignore this email.</p>
</div>
  `.trim();

  readonly DEFAULT_RESET_TEMPLATE = `
<div style="font-family: sans-serif; color: #333; line-height: 1.6; max-width: 600px;">
    <h2 style="color: #1e293b; margin-top: 0;">Password Reset Request</h2>
    <p>We received a request to reset your password. Click the link below to choose a new one:</p>
    <p style="margin: 20px 0;">
        <a href="{{ResetLink}}" style="color: #3b82f6; font-weight: bold; text-decoration: underline;">Reset Password</a>
    </p>
    <p>If you did not request this, you can safely ignore this email. The link will expire in 1 hour.</p>
    <p>If the link above doesn't work, copy and paste this URL:</p>
    <p style="word-break: break-all; color: #64748b; font-size: 13px;">{{ResetLink}}</p>
</div>
  `.trim();

  constructor() {
    this.passwordForm = this.fb.group({
      currentPassword: ['', [Validators.required]],
      newPassword: ['', [Validators.required, Validators.minLength(5)]],
      confirmPassword: ['', [Validators.required]]
    }, { validators: this.passwordMatchValidator });
  }

  ngOnInit() {
    // Restore active tab from URL query param on load
    const tabParam = this.route.snapshot.queryParamMap.get('tab') as
      'overview' | 'ai' | 'email' | 'emailTemplates' | 'branding' | null;
    const validTabs: Array<'overview' | 'ai' | 'email' | 'emailTemplates' | 'branding'> =
      ['overview', 'ai', 'email', 'emailTemplates', 'branding'];
    if (tabParam && validTabs.includes(tabParam)) {
      this.activeTab.set(tabParam);
    }

    this.loadCompanies();
    this.loadUserProfile();
    this.loadSystemSettings();
    this.matchmakingService.applyBranding(SYSTEM_ID);
    this.togglePortalLayout(true);
  }

  switchTab(tab: 'overview' | 'ai' | 'email' | 'emailTemplates' | 'branding') {
    this.activeTab.set(tab);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab },
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
  }

  private passwordMatchValidator(g: FormGroup) {
    return g.get('newPassword')?.value === g.get('confirmPassword')?.value
      ? null : { mismatch: true };
  }

  loadUserProfile() {
    this.authService.getMe().subscribe({
      next: (user) => {
        this.currentUser = user;
      },
      error: (err) => {
        console.error('Failed to load user profile', err);
        // Fallback to name from token if getMe fails
        const username = this.authService.getCurrentUser();
        const role = this.authService.getUserRole();
        this.currentUser = { username, role };
      }
    });
  }

  loadSystemSettings() {
    this.isLoadingSettings.set(true);
    const keys = [
      'AiProvider', 'AiModel', 'AiApiKey', 
      'SendGridApiKey', 'SendGridFromEmail', 'EmailFromName',
      'VerificationEmailSubject', 'VerificationEmailHtmlBody',
      'PasswordResetEmailSubject', 'PasswordResetEmailHtmlBody',
      'BrandingPrimaryColor', 'BrandingSecondaryColor', 'BrandingBackgroundType', 'BrandingBackgroundImage', 'BrandingBackgroundColor'
    ];
    let loadedCount = 0;

    keys.forEach(key => {
      this.apiService.getSetting(key, this.SYSTEM_ID).pipe(
        catchError(() => of(null))
      ).subscribe({
        next: (res: any) => {
          if (res && res.value !== null && res.value !== undefined && res.value !== '') {
            this.systemSettings.update(s => ({ ...s, [key]: res.value }));
          } else {
            // Apply defaults for templates
            if (key === 'VerificationEmailHtmlBody') {
              this.systemSettings.update(s => ({ ...s, [key]: this.DEFAULT_VERIFICATION_TEMPLATE }));
            } else if (key === 'VerificationEmailSubject') {
              this.systemSettings.update(s => ({ ...s, [key]: 'Verify your account' }));
            } else if (key === 'PasswordResetEmailHtmlBody') {
              this.systemSettings.update(s => ({ ...s, [key]: this.DEFAULT_RESET_TEMPLATE }));
            } else if (key === 'PasswordResetEmailSubject') {
              this.systemSettings.update(s => ({ ...s, [key]: 'Password Reset Request' }));
            }
          }
          loadedCount++;
          if (loadedCount === keys.length) this.isLoadingSettings.set(false);
        },
        error: () => {
          loadedCount++;
          if (loadedCount === keys.length) this.isLoadingSettings.set(false);
        }
      });
    });
  }

  saveSystemSettings() {
    this.isSavingSettings.set(true);
    const data = this.systemSettings();
    const keys = Object.keys(data) as Array<keyof typeof data>;
    let savedCount = 0;

    keys.forEach(key => {
      this.apiService.updateSetting(String(key), data[key], this.SYSTEM_ID).subscribe({
        next: () => {
          savedCount++;
          if (savedCount === keys.length) {
            this.notify.success('System settings saved!');
            this.isSavingSettings.set(false);
            this.matchmakingService.applyBranding(SYSTEM_ID);
          }
        },
        error: (err) => {
          console.error(`Failed to save ${key}`, err);
          savedCount++;
          if (savedCount === keys.length) {
            this.isSavingSettings.set(false);
          }
        }
      });
    });
  }

  updateSystemSetting(key: string, value: any) {
    this.systemSettings.update(s => ({ ...s, [key]: value }));
  }

  resetToDefaultTemplate() {
    if (confirm('Are you sure you want to revert to the default template? This will overwrite your current changes.')) {
      if (this.activeTemplateType() === 'verification') {
        this.systemSettings.update(s => ({ 
          ...s, 
          VerificationEmailHtmlBody: this.DEFAULT_VERIFICATION_TEMPLATE,
          VerificationEmailSubject: 'Verify your account'
        }));
      } else {
        this.systemSettings.update(s => ({ 
          ...s, 
          PasswordResetEmailHtmlBody: this.DEFAULT_RESET_TEMPLATE,
          PasswordResetEmailSubject: 'Password Reset Request'
        }));
      }
    }
  }

  sendTestEmail() {
    const email = prompt('Enter email address to send test to:', this.currentUser?.email || '');
    if (!email) return;

    this.isSendingTest.set(true);
    const isVerification = this.activeTemplateType() === 'verification';
    const template = isVerification 
      ? this.systemSettings().VerificationEmailHtmlBody 
      : this.systemSettings().PasswordResetEmailHtmlBody;
    
    this.apiService.sendTestEmail({ 
      email, 
      template, 
      type: isVerification ? 'VerificationEmail' : 'PasswordResetEmail'
    }, this.SYSTEM_ID).subscribe({
      next: () => {
        this.notify.success('Test email sent successfully!');
        this.isSendingTest.set(false);
      },
      error: (err: any) => {
        console.error('Failed to send test email', err);
        this.notify.error('Failed to send test email');
        this.isSendingTest.set(false);
      }
    });
  }

  logout() {
    this.authService.logout();
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
      this.systemSettings.update(s => ({ ...s, BrandingBackgroundImage: base64 }));
    };
    reader.readAsDataURL(file);
  }

  setAiProvider(provider: 'OpenAI' | 'Gemini') {
    this.systemSettings.update((s: any) => ({
      ...s,
      AiProvider: provider,
      AiModel: provider === 'OpenAI' ? 'gpt-4o' : 'gemini-1.5-flash'
    }));
  }

  updatePassword() {
    if (this.passwordForm.invalid) return;

    this.isUpdatingPassword = true;
    const { currentPassword, newPassword } = this.passwordForm.value;

    this.authService.updatePassword(currentPassword, newPassword).subscribe({
      next: () => {
        this.notify.success('Password updated successfully!');
        this.isUpdatingPassword = false;
        this.passwordForm.reset();
        this.showSecurityModal.set(false);
      },
      error: (err) => {
        console.error('Failed to update password', err);
        this.notify.error('Failed to update password. Please check your current password.');
        this.isUpdatingPassword = false;
      }
    });
  }

  ngOnDestroy() {
    this.togglePortalLayout(false);
  }

  private togglePortalLayout(active: boolean) {
    const appElement = document.getElementById('app');
    if (appElement) {
      if (active) {
        appElement.classList.add('portal-layout');
      } else {
        appElement.classList.remove('portal-layout');
      }
    }
  }

  loadCompanies() {
    this.authService.getCompanies().subscribe({
      next: (res) => {
        this.companies.set(res);
        this.loading = false;
        if (!res || res.length === 0) {
          this.openCreateEventModal();
        }
      },
      error: (err) => {
        console.error('Failed to load events', err);
        this.notify.error('Failed to fetch event list.');
        this.loading = false;
      }
    });
  }

  selectCompany(id: string) {
    this.authService.switchCompany(id).subscribe({
      next: () => {
        this.notify.success('Event selected!');
        this.matchmakingService.applyBranding(id);
        this.router.navigate(['/admin']);
      },
      error: (err) => {
        console.error('Failed to switch event', err);
        this.notify.error('Could not switch event.');
      }
    });
  }

  getInitials(name: string): string {
    return name.split(' ').map(n => n[0]).join('').toUpperCase().substring(0, 2);
  }

  getSanitizedLogo(logo: string) {
    return this.sanitizer.bypassSecurityTrustHtml(logo);
  }

  editingCompanyId = signal<string | null>(null);
  editNameInput = signal<string>('');
  isSavingName = signal(false);

  // Individual Company Branding Modal
  showBrandingModal = signal(false);
  selectedCompanyForBranding = signal<any>(null);
  isSavingCompanyBranding = signal(false);
  companyBrandingForm = signal({
    BrandingPrimaryColor: '#31a2ae',
    BrandingSecondaryColor: '#a60053',
    BrandingBackgroundType: 'color',
    BrandingBackgroundImage: '',
    BrandingBackgroundColor: '#002a37'
  });

  openBrandingModal(event: Event, company: any) {
    event.stopPropagation();
    this.selectedCompanyForBranding.set(company);
    this.showBrandingModal.set(true);
    
    // Load current branding for this company
    const keys = ['BrandingPrimaryColor', 'BrandingSecondaryColor', 'BrandingBackgroundType', 'BrandingBackgroundImage', 'BrandingBackgroundColor'];
    keys.forEach(key => {
      this.apiService.getSetting(key, company.id).subscribe((res: any) => {
        if (res && res.value !== null && res.value !== undefined) {
          this.companyBrandingForm.update(f => ({ ...f, [key]: res.value }));
        }
      });
    });
  }

  closeBrandingModal() {
    this.showBrandingModal.set(false);
    this.selectedCompanyForBranding.set(null);
  }

  saveCompanyBranding() {
    const cid = this.selectedCompanyForBranding()?.id;
    if (!cid) return;

    this.isSavingCompanyBranding.set(true);
    const data = this.companyBrandingForm();
    const keys = Object.keys(data) as Array<keyof typeof data>;
    let savedCount = 0;

    keys.forEach(key => {
      this.apiService.updateSetting(String(key), data[key], cid).subscribe({
        next: () => {
          savedCount++;
          if (savedCount === keys.length) {
            this.notify.success('Event branding updated!');
            this.isSavingCompanyBranding.set(false);
            this.closeBrandingModal();
            // Removed redundant applyBranding call to avoid affecting the main selector dashboard
          }
        },
        error: (err) => {
          console.error(`Failed to save ${key}`, err);
          savedCount++;
          if (savedCount === keys.length) {
            this.isSavingCompanyBranding.set(false);
          }
        }
      });
    });
  }

  onModalBackgroundImageSelected(event: any) {
    const file = event.target.files[0];
    if (!file) return;

    if (!file.type.match('image.*')) {
      this.notify.error('Please upload an image file (PNG, JPG).');
      return;
    }

    const reader = new FileReader();
    reader.onload = (e) => {
      const base64 = e.target?.result as string;
      this.updateModalBranding('BrandingBackgroundImage', base64);
    };
    reader.readAsDataURL(file);
  }

  updateModalBranding(key: string, value: any) {
    this.companyBrandingForm.update(f => ({ ...f, [key]: value }));
  }

  previewSystemUserView() {
    window.open('/', '_blank');
  }

  // Password reveal tracking for system dashboard
  revealedPasswords = signal<Set<string>>(new Set<string>());

  togglePasswordReveal(event: Event, accountId: string) {
    event.stopPropagation();
    const current = new Set(this.revealedPasswords());
    if (current.has(accountId)) {
      current.delete(accountId);
    } else {
      current.add(accountId);
    }
    this.revealedPasswords.set(current);
  }

  copyPassword(event: Event, password: string) {
    event.stopPropagation();
    navigator.clipboard.writeText(password).then(() => {
      this.notify.success('Password copied to clipboard!');
    }).catch(() => {
      this.notify.error('Failed to copy password.');
    });
  }



  startEdit(event: Event, company: any) {
    event.stopPropagation();
    this.editingCompanyId.set(company.id);
    this.editNameInput.set(company.name);
  }

  // Quick Event Switcher
  showEventSwitcherModal = signal(false);
  accountEvents = signal<any[]>([]);
  isLoadingEvents = signal(false);
  selectedAccountForEvents = signal<any>(null);

  openEventSwitcher(event: Event, account: any) {
    event.stopPropagation();
    this.selectedAccountForEvents.set(account);
    this.showEventSwitcherModal.set(true);
    this.isLoadingEvents.set(true);
    
    const cid = account.companyId || account.id;
    this.apiService.getEvents(cid).subscribe({
      next: (events) => {
        this.accountEvents.set(events);
        this.isLoadingEvents.set(false);
      },
      error: (err) => {
        console.error('Failed to load account events', err);
        this.isLoadingEvents.set(false);
        this.notify.error('Failed to load events for this account.');
      }
    });
  }

  closeEventSwitcher() {
    this.showEventSwitcherModal.set(false);
    this.selectedAccountForEvents.set(null);
    this.accountEvents.set([]);
  }

  selectAccountEvent(eventId: string) {
    this.isLoadingEvents.set(true);
    this.authService.switchCompany(eventId).subscribe({
      next: () => {
        this.notify.success('Switched to event!');
        this.router.navigate(['/admin/dashboard']);
      },
      error: (err) => {
        console.error('Failed to switch event', err);
        this.isLoadingEvents.set(false);
        this.notify.error('Failed to switch to the selected event.');
      }
    });
  }

  resendVerification(event: Event, account: any) {
    event.stopPropagation();
    this.apiService.resendVerification(account.id).subscribe({
      next: () => {
        this.notify.success('Verification email resent successfully.');
      },
      error: (err) => {
        console.error('Failed to resend verification', err);
        this.notify.error(err.error?.message || 'Failed to resend verification.');
      }
    });
  }
  cancelEdit(event: Event) {
    event.stopPropagation();
    this.editingCompanyId.set(null);
    this.editNameInput.set('');
  }

  saveEdit(event: Event, company: any) {
    event.stopPropagation();
    if (!this.editNameInput().trim() || this.editNameInput().trim() === company.name) {
      this.cancelEdit(event);
      return;
    }

    this.isSavingName.set(true);
    const newName = this.editNameInput().trim();
    this.authService.updateCompany(company.id, newName).subscribe({
      next: () => {
        company.name = newName;
        this.notify.success('Event name updated successfully!');
        this.editingCompanyId.set(null);
        this.isSavingName.set(false);
      },
      error: (err) => {
        console.error('Failed to update project name', err);
        this.notify.error('Failed to update project name');
        this.isSavingName.set(false);
      }
    });
  }

  showDeleteModal = signal(false);
  projectToDelete = signal<any>(null);
  deleteConfirmText = signal('');
  isDeleting = signal(false);

  deleteProject(event: Event, project: any) {
    event.stopPropagation();
    
    if (project.participantCount > 0) {
      this.notify.error('Cannot delete a project that still has registered participants.');
      return;
    }

    this.projectToDelete.set(project);
    this.deleteConfirmText.set('');
    this.showDeleteModal.set(true);
  }

  closeDeleteModal() {
    this.showDeleteModal.set(false);
    this.projectToDelete.set(null);
    this.deleteConfirmText.set('');
  }

  confirmDeleteProject() {
    const project = this.projectToDelete();
    if (!project) return;

    if (this.deleteConfirmText() !== project.name) {
      this.notify.warning('Event name does not match.');
      return;
    }

    this.isDeleting.set(true);
    const targetId = project.companyId || project.id;

    this.apiService.deleteCompany(targetId).subscribe({
      next: (res: any) => {
        this.notify.success('Event deleted successfully.');
        
        if (res && res.token) {
          sessionStorage.setItem('token', res.token);
        }

        this.isDeleting.set(false);
        this.closeDeleteModal();
        this.loadCompanies();
      },
      error: (err: any) => {
        console.error('Failed to delete project', err);
        this.notify.error(err.error?.message || 'Failed to delete project.');
        this.isDeleting.set(false);
      }
    });
  }

  openCreateEventModal() {
    this.newEventData.set({
      projectName: '',
      city: '',
      description: '',
      roleSelectionEnabled: false,
      roles: [] as any[],
      questions: [] as any[],
      emailTemplate: '',
      resultEmailTemplate: '',
      feedbackEmailTemplate: '',
      brandingPrimaryColor: '',
      brandingSecondaryColor: '',
      brandingBackgroundType: '',
      brandingBackgroundColor: '',
      welcomeTitle: '',
      welcomeTagline: '',
      welcomeDescription: '',
      welcomeButton: ''
    });
    this.aiRecommendation.set(null);
    this.showCreateEventModal.set(true);
  }

  closeCreateEventModal() {
    this.showCreateEventModal.set(false);
  }

  generateAiRecommendation() {
    const desc = this.newEventData().description;
    if (!desc) {
      this.notify.warning('Please provide an event description to get AI recommendations.');
      return;
    }

    this.isGeneratingRecommendation.set(true);
    this.apiService.recommendEventSetup(desc).subscribe({
      next: (res: any) => {
        this.isGeneratingRecommendation.set(false);
        this.aiRecommendation.set(res);
        this.notify.success('AI recommendations generated successfully!');

        // Set recommended properties to form
        this.newEventData.update(d => ({
          ...d,
          roleSelectionEnabled: res.roleSelectionEnabled || false,
          roles: res.roles || [],
          questions: res.questions || [],
          emailTemplate: res.emailTemplate || '',
          resultEmailTemplate: res.resultEmailTemplate || '',
          feedbackEmailTemplate: res.feedbackEmailTemplate || '',
          brandingPrimaryColor: res.brandingPrimaryColor || '',
          brandingSecondaryColor: res.brandingSecondaryColor || '',
          brandingBackgroundType: res.brandingBackgroundType || '',
          brandingBackgroundColor: res.brandingBackgroundColor || '',
          welcomeTitle: res.welcomeTitle || '',
          welcomeTagline: res.welcomeTagline || '',
          welcomeDescription: res.welcomeDescription || '',
          welcomeButton: res.welcomeButton || ''
        }));
      },
      error: (err: any) => {
        console.error('Failed to generate AI recommendations', err);
        const msg = err.error?.message || 'Failed to generate recommendations.';
        this.notify.error(msg);
        this.isGeneratingRecommendation.set(false);
      }
    });
  }

  createEvent() {
    const data = this.newEventData();
    if (!data.projectName || !data.city) {
      this.notify.warning('Please fill in event name and city.');
      return;
    }

    this.isCreatingEvent.set(true);
    const currentCompanyId = this.authService.getCompanyId();

    this.authService.getMe().subscribe({
      next: (me) => {
        const parentId = me?.parentId || currentCompanyId;
        const payload = {
          name: data.projectName,
          city: data.city,
          description: data.description,
          parentId: parentId,
          roleSelectionEnabled: data.roleSelectionEnabled,
          roles: data.roles,
          questions: data.questions,
          emailTemplate: data.emailTemplate,
          resultEmailTemplate: data.resultEmailTemplate,
          feedbackEmailTemplate: data.feedbackEmailTemplate,
          brandingPrimaryColor: data.brandingPrimaryColor,
          brandingSecondaryColor: data.brandingSecondaryColor,
          brandingBackgroundType: data.brandingBackgroundType,
          brandingBackgroundColor: data.brandingBackgroundColor,
          welcomeTitle: data.welcomeTitle,
          welcomeTagline: data.welcomeTagline,
          welcomeDescription: data.welcomeDescription,
          welcomeButton: data.welcomeButton
        };

        this.apiService.createEvent(payload).subscribe({
          next: (res: any) => {
            this.notify.success('New event created successfully!');
            this.isCreatingEvent.set(false);
            this.showCreateEventModal.set(false);

            // If it returns a token, update sessionStorage
            if (res && res.token) {
              sessionStorage.setItem('token', res.token);
            }

            // After creating the event, since this is first time login, switch to this new event and navigate to admin!
            // Wait, what's the company ID of the newly created event?
            // If the create event returns an event / company ID or the user needs to reload:
            if (res && res.id) {
              this.selectCompany(res.id);
            } else if (res && res.companyId) {
              this.selectCompany(res.companyId);
            } else {
              // Refresh companies
              this.loadCompanies();
              window.location.reload();
            }
          },
          error: (err: any) => {
            console.error('Event creation failed', err);
            const msg = typeof err.error === 'string' ? err.error : (err.error?.message || 'Failed to create event.');
            this.notify.error(msg);
            this.isCreatingEvent.set(false);
          }
        });
      },
      error: (err: any) => {
        console.error('Failed to get user profile', err);
        this.notify.error('Failed to fetch your profile information.');
        this.isCreatingEvent.set(false);
      }
    });
  }

  // --- Account Details Modal & Manual Verify ---
  showAccountDetailsModal = signal(false);
  selectedAccountDetails = signal<any>(null);
  isSavingDetails = signal(false);
  
  accountForm = this.fb.group({
    companyName: ['', Validators.required],
    adminFirstName: ['', Validators.required],
    adminLastName: ['', Validators.required],
    adminEmail: ['', [Validators.required, Validators.email]],
    newPassword: ['']
  });

  openAccountDetails(event: Event, account: any) {
    event.stopPropagation();
    this.selectedAccountDetails.set(account);

    let fName = account.adminFirstName || '';
    let lName = account.adminLastName || '';
    
    // Fallback if backend hasn't been restarted to include the new fields
    if (!fName && !lName && account.adminName) {
      const parts = account.adminName.split(' ');
      fName = parts[0] || '';
      lName = parts.slice(1).join(' ') || '';
    }

    this.accountForm.patchValue({
      companyName: account.name,
      adminFirstName: fName,
      adminLastName: lName,
      adminEmail: account.adminEmail,
      newPassword: ''
    });
    this.showAccountDetailsModal.set(true);
  }

  closeAccountDetails() {
    this.showAccountDetailsModal.set(false);
    this.selectedAccountDetails.set(null);
    this.accountForm.reset();
  }

  saveAccountDetails() {
    if (this.accountForm.invalid) {
      this.accountForm.markAllAsTouched();
      return;
    }
    
    const account = this.selectedAccountDetails();
    if (!account) return;

    this.isSavingDetails.set(true);
    const formVals = this.accountForm.value;

    // Update Company Name First
    this.authService.updateCompany(account.companyId || account.id, formVals.companyName!).subscribe({
      next: () => {
        // Then Update Admin Details
        const adminUpdate = {
          FirstName: formVals.adminFirstName,
          LastName: formVals.adminLastName,
          Email: formVals.adminEmail,
          Password: formVals.newPassword ? formVals.newPassword : null,
          Role: 'Admin'
        };

        this.apiService.updateAdmin(account.id, adminUpdate).subscribe({
          next: () => {
            this.notify.success('Account details updated successfully!');
            this.isSavingDetails.set(false);
            this.closeAccountDetails();
            this.loadCompanies(); // refresh list
          },
          error: (err) => {
            console.error('Failed to update admin details', err);
            this.notify.error(err.error?.message || 'Failed to update admin details.');
            this.isSavingDetails.set(false);
          }
        });
      },
      error: (err) => {
        console.error('Failed to update company name', err);
        this.notify.error('Failed to update event name.');
        this.isSavingDetails.set(false);
      }
    });
  }

  showVerifyModal = signal(false);
  accountToVerify = signal<any>(null);
  isVerifying = signal(false);

  manualVerify() {
    const account = this.selectedAccountDetails();
    if (!account) return;
    this.accountToVerify.set(account);
    this.showVerifyModal.set(true);
  }

  closeVerifyModal() {
    this.showVerifyModal.set(false);
    this.accountToVerify.set(null);
  }

  confirmVerifyAccount() {
    const account = this.accountToVerify();
    if (!account) return;

    this.isVerifying.set(true);
    this.apiService.manualVerifyAccount(account.id).subscribe({
      next: () => {
        this.notify.success('Account successfully verified!');
        // Update local state so it shows active immediately
        const acc = { ...account, isVerified: true, isDeactivated: false };
        
        if (this.selectedAccountDetails()?.id === account.id) {
          this.selectedAccountDetails.set(acc);
        }
        
        this.loadCompanies(); // refresh list
        this.isVerifying.set(false);
        this.closeVerifyModal();
      },
      error: (err) => {
        console.error('Failed to manually verify account', err);
        this.notify.error(err.error?.message || 'Failed to manually verify account.');
        this.isVerifying.set(false);
        this.closeVerifyModal();
      }
    });
  }

  showDeactivateModal = signal(false);
  accountToDeactivate = signal<any>(null);
  isDeactivating = signal(false);

  deactivateAccount() {
    const account = this.selectedAccountDetails();
    if (!account) return;
    this.accountToDeactivate.set(account);
    this.showDeactivateModal.set(true);
  }

  closeDeactivateModal() {
    this.showDeactivateModal.set(false);
    this.accountToDeactivate.set(null);
  }

  confirmDeactivateAccount() {
    const account = this.accountToDeactivate();
    if (!account) return;

    this.isDeactivating.set(true);
    this.apiService.deactivateAccount(account.id).subscribe({
      next: () => {
        this.notify.success('Account successfully deactivated!');
        const acc = { ...account, isVerified: false, isDeactivated: true };
        
        // Update local state if the modal is still focused
        if (this.selectedAccountDetails()?.id === account.id) {
          this.selectedAccountDetails.set(acc);
        }
        
        this.loadCompanies(); // refresh list
        this.isDeactivating.set(false);
        this.closeDeactivateModal();
      },
      error: (err) => {
        console.error('Failed to deactivate account', err);
        this.notify.error(err.error?.message || 'Failed to deactivate account.');
        this.isDeactivating.set(false);
        this.closeDeactivateModal();
      }
    });
  }
}
