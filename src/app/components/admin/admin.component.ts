import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ApiService } from '../../services/api.service';
import { NotificationService } from '../../services/notification.service';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

@Component({
    selector: 'app-admin',
    standalone: true,
    imports: [CommonModule, RouterModule, FormsModule],
    templateUrl: './admin.component.html',
    styleUrls: ['./admin.component.css']
})
export class AdminComponent implements OnInit {
    public authService = inject(AuthService);
    public router = inject(Router);
    private apiService = inject(ApiService);
    private notify = inject(NotificationService);
    private sanitizer = inject(DomSanitizer);

    public currentUser: string | null = null;
    public currentCompanyName = signal<string>('');
    public userRole = signal<string | null>(null);
    public logo = signal<string>('');
    public useDefaultLogo = signal<boolean>(false);
    public projectAdminEmail = signal<string | null>(null);

    // Sanitized logo for the header (bypasses Angular's SVG stripping)
    sanitizedLogo = computed<SafeHtml | null>(() => {
        const logoVal = this.logo();
        return logoVal ? this.sanitizer.bypassSecurityTrustHtml(logoVal) : null;
    });

    ngOnInit() {
        this.currentUser = this.authService.getCurrentUser();
        this.userRole.set(this.authService.getUserRole());
        this.loadProfile();
        this.loadLogo();
        this.loadEvents();

        // Reactive logo refresh
        this.authService.logoChanged$.subscribe(() => {
            this.loadLogo();
        });
    }

    loadProfile() {
        this.authService.getMe().subscribe({
            next: (res) => {
                this.currentCompanyName.set(res.companyName);
                this.projectAdminEmail.set(res.projectAdminEmail);
                if (res.currentPassword) {
                    this.currentPassword.set(res.currentPassword);
                }
            },
            error: (err) => console.error('Failed to load profile', err)
        });
    }

    loadLogo() {
        const companyId = this.authService.getCompanyId();
        if (!companyId) return;
        
        this.apiService.getSetting('WelcomeLogo', companyId).subscribe({
            next: (res: any) => {
                if (res && res.value) {
                    this.logo.set(res.value);
                    this.useDefaultLogo.set(false);
                } else {
                    this.logo.set(''); 
                    this.useDefaultLogo.set(true);
                }
            },
            error: (err) => {
                console.error('Failed to load company logo', err);
                this.logo.set('');
                this.useDefaultLogo.set(true);
            }
        });
    }

    onLogoError() {
        this.useDefaultLogo.set(true);
    }

    logout() {
        this.authService.logout();
    }

    switchAccount() {
        this.router.navigate(['/select-event']);
    }

    goToSecurity() {
        this.openSecurityModal();
    }

    // Modal State
    showConfirmModal = signal(false);
    showSecurityModal = signal(false);
    confirmTitle = signal('');
    confirmMessage = signal('');
    private confirmCallback: (() => void) | null = null;

    // Security Modal State
    currentPassword = signal('');
    showCurrentPassword = signal(false);
    newPassword = signal('');
    confirmPassword = signal('');

    openSecurityModal() {
        this.showSecurityModal.set(true);
    }

    closeSecurityModal() {
        this.showSecurityModal.set(false);
        this.newPassword.set('');
        this.confirmPassword.set('');
        this.showCurrentPassword.set(false);
        // Re-load the current password from profile after closing
        this.loadProfile();
    }

    updatePassword() {
        const current = this.currentPassword();
        const password = this.newPassword();
        const confirm = this.confirmPassword();

        if (!current) {
            this.notify.warning('Please enter your current password.');
            return;
        }

        if (!password || password.length < 6) {
            this.notify.warning('The new password must be at least 6 characters long.');
            return;
        }

        if (password !== confirm) {
            this.notify.warning('The new passwords do not match.');
            return;
        }

        this.notify.info('Updating password...');
        this.authService.updatePassword(current, password).subscribe({
            next: () => {
                this.notify.success('Password updated!');
                this.closeSecurityModal();
            },
            error: (err) => {
                console.error('Failed to update password', err);
                const errorMsg = err.error || 'Failed to update password.';
                this.notify.error(errorMsg);
            }
        });
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

    seedData() {
        this.openConfirm(
            'Create Test Data',
            'WARNING: This will clear participants and create new test data. Do you want to continue?',
            () => {
                this.notify.info('Creating test data...');
                this.apiService.seedData().subscribe({
                    next: (res: any) => {
                        this.notify.success(res.message);
                        // Refresh to show new data
                        window.location.reload();
                    },
                    error: (err) => {
                        console.error(err);
                        this.notify.error('Failed to create test data.');
                    }
                });
            }
        );
    }

    // Event Management Logic
    public companyEvents = signal<any[]>([]);
    public showEventSwitcher = signal(false);
    public showCreateEventModal = signal(false);
    public isCreatingEvent = signal(false);
    public isGeneratingRecommendation = signal(false);
    public aiRecommendation = signal<any>(null);
    public newEventData = signal({
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

    public activeEvent = computed(() => {
        const activeId = this.authService.getCompanyId();
        return this.companyEvents().find(e => e.id === activeId);
    });

    public editingActiveEvent = signal<boolean>(false);
    public editActiveEventNameInput = signal<string>('');
    public isSavingActiveEventName = signal<boolean>(false);

    startEditingActiveEvent(event: Event) {
        event.stopPropagation();
        const active = this.activeEvent();
        if (active) {
            this.editActiveEventNameInput.set(active.name);
            this.editingActiveEvent.set(true);
        } else {
            this.editActiveEventNameInput.set(this.currentCompanyName());
            this.editingActiveEvent.set(true);
        }
    }

    cancelEditingActiveEvent(event: Event) {
        event.stopPropagation();
        this.editingActiveEvent.set(false);
        this.editActiveEventNameInput.set('');
    }

    saveEditingActiveEventName(event: Event) {
        event.stopPropagation();
        const activeId = this.authService.getCompanyId();
        const newName = this.editActiveEventNameInput().trim();
        if (!activeId || !newName) {
            this.cancelEditingActiveEvent(event);
            return;
        }

        this.isSavingActiveEventName.set(true);
        this.authService.updateCompany(activeId, newName).subscribe({
            next: () => {
                this.notify.success('Event name updated successfully!');
                const active = this.activeEvent();
                if (active) active.name = newName;
                this.currentCompanyName.set(newName);
                this.editingActiveEvent.set(false);
                this.isSavingActiveEventName.set(false);
            },
            error: (err) => {
                console.error('Failed to update event name', err);
                this.notify.error('Failed to update event name');
                this.isSavingActiveEventName.set(false);
            }
        });
    }

    loadEvents() {
        const companyId = this.authService.getCompanyId();
        if (companyId) {
            this.apiService.getEvents(companyId).subscribe({
                next: (events) => {
                    this.companyEvents.set(events);
                    if (!events || events.length === 0) {
                        if (!sessionStorage.getItem('hasAutoOpenedCreateEventModal')) {
                            sessionStorage.setItem('hasAutoOpenedCreateEventModal', 'true');
                            this.openCreateEventModal();
                        }
                    }
                },
                error: (err) => console.error('Failed to load company events', err)
            });
        }
    }

    switchEvent(eventId: string) {
        if (eventId === this.authService.getCompanyId()) {
            this.showEventSwitcher.set(false);
            return;
        }

        this.authService.switchCompany(eventId).subscribe({
            next: () => {
                this.notify.success('Switched to event!');
                window.location.reload();
            },
            error: (err) => {
                console.error('Failed to switch event', err);
                this.notify.error('Failed to switch event');
            }
        });
    }

    openCreateEventModal() {
        this.newEventData.set({
            projectName: '',
            city: '',
            description: '',
            roleSelectionEnabled: false,
            roles: [],
            questions: [],
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
                const parentId = me.parentId || currentCompanyId;
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
                        if (res.token) {
                            sessionStorage.setItem('token', res.token);
                        }

                        // Refresh event list and profile
                        this.loadEvents();
                        this.loadProfile();
                        window.location.reload();
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
}


