import { Component, inject, signal, computed } from '@angular/core';
import { RouterLink, Router, ActivatedRoute } from '@angular/router';
import { CommonModule } from '@angular/common';
import { MatchmakingService } from '../../../services/matchmaking.service';
import { ApiService } from '../../../services/api.service';
import { NotificationService } from '../../../services/notification.service';
import { AuthService } from '../../../services/auth.service';

import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  templateUrl: './admin-dashboard.component.html',
  styleUrls: ['./admin-dashboard.component.css']
})
export class AdminDashboardComponent {
  public service = inject(MatchmakingService);
  private apiService = inject(ApiService);
  private authService = inject(AuthService);
  private notify = inject(NotificationService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  public userRole = signal<string | null>(this.authService.getUserRole());

  page = signal(1);
  searchQuery = signal('');
  isLoading = signal(false);
  hasMore = signal(true);
  totalCount = signal(0);
  statusFilter = signal('');

  activeTab = signal<'participants' | 'results' | 'schedule'>('participants');
  mutualMatches = signal<any[]>([]);
  matchesSearchQuery = signal('');
  isLoadingMatches = signal(false);

  // Helpdesk Global Schedule State
  globalSlots = signal<any[]>([]);
  isLoadingSchedule = signal(false);
  isScheduleEnabled = signal(false); // Only true when admin has enabled the schedule feature

  // Manual Assign Modal State
  showAssignModal = signal(false);
  selectedSlot = signal<any>(null);
  selectedStudentId = signal<string>('');
  studentCandidates = signal<any[]>([]);

  registrations = signal<any[]>([]);
  companyEvents = signal<any[]>([]);
  showEventSwitcher = signal(false);

  totalParticipants = computed(() => this.feedbackStats().totalParticipants || this.totalCount());
  activeEvent = computed(() => {
    const activeId = this.authService.getCompanyId();
    return this.companyEvents().find(e => e.id === activeId);
  });

  // Note: 'newToday' still relies on loaded data for now, but others use global stats.
  newToday = computed(() => {
    const today = new Date().toISOString().split('T')[0];
    return this.registrations().filter(r => {
      if (!r.lastActive) return false;
      return r.lastActive.toString().startsWith(today);
    }).length;
  });

  completedCount = computed(() => {
    return this.feedbackStats().completedCount || this.registrations().filter(r => r.status && (r.status === 'IN PROGRESS' || r.status === 'COMPLETED' || r.status === 'Answered')).length;
  });

  responseRate = computed(() => {
    const total = this.totalParticipants();
    if (total === 0) return 0;
    return Math.round((this.completedCount() / total) * 100);
  });

  completionPercentage = computed(() => {
    const total = this.totalParticipants();
    if (total === 0) return 0;
    // Strict progress: Only 'COMPLETED' counts as complete (100%)
    const completedCount = this.registrations().filter(r => r.status === 'COMPLETED').length;
    return Math.round((completedCount / total) * 100);
  });

  totalMatches = computed(() => {
    return this.feedbackStats().totalMatches || 0;
  });

  avgMatches = computed(() => {
    const total = this.totalParticipants();
    if (total === 0) return 0;
    return (this.totalMatches() / total).toFixed(1);
  });

  totalPotentialMatches = computed(() => this.feedbackStats().totalPotentialMatches || 0);
  totalFeedback = computed(() => this.feedbackStats().totalFeedback || 0);


  bookedMeetings = computed(() => {
    return this.totalMatches();
  });

  requestedMeetingParticipants = computed(() => this.feedbackStats().requestedMeetingParticipants || 0);
  totalRequestedMeetings = computed(() => this.feedbackStats().totalRequestedMeetings || 0);
  avgRequestedPerPerson = computed(() => {
    const p = this.requestedMeetingParticipants();
    return p === 0 ? '0' : (this.totalRequestedMeetings() / p).toFixed(1);
  });

  bookedMeetingParticipants = computed(() => this.feedbackStats().bookedMeetingParticipants || 0);
  totalBookedMeetings = computed(() => this.feedbackStats().totalBookedMeetings || 0);
  avgBookedPerPerson = computed(() => {
    const p = this.bookedMeetingParticipants();
    return p === 0 ? '0' : (this.totalBookedMeetings() / p).toFixed(1);
  });

  matchCalculations = computed(() => {
    const n = this.totalParticipants();
    if (n < 2) return 0;
    return (n * (n - 1)) / 2;
  });

  feedbackStats = signal<any>({
    relevant: 0,
    notRelevant: 0,
    accuracy: 0,
    reasons: [],
    marketNeeds: []
  });

  isSurveyOpen = signal<boolean>(true);
  isRoleSelectionEnabled = signal<boolean>(false);
  roleFilter = signal<string>('');

  ngOnInit() {
    // Always start at the top of the page
    window.scrollTo(0, 0);

    // Restore active tab from URL query param on load
    const tabParam = this.route.snapshot.queryParamMap.get('tab') as 'participants' | 'results' | 'schedule' | null;
    const validTabs: Array<'participants' | 'results' | 'schedule'> = ['participants', 'results', 'schedule'];
    if (tabParam && validTabs.includes(tabParam)) {
      this.activeTab.set(tabParam);
      if (tabParam === 'results') this.loadMutualMatches();
      else if (tabParam === 'schedule') this.loadGlobalSchedule();
    }

    this.loadEvents();
    this.loadRegistrations();
    this.loadAllStudents();
    this.loadStats();
    this.loadSettings(); // loadSettings also sets isScheduleEnabled
  }

  loadAllStudents() {
    const companyId = this.authService.getCompanyId();
    if (!companyId) return;

    // Fetch up to 1000 students to ensure all are available for manual assignment
    this.apiService.getParticipantSummaries(1, 1000, '', '', companyId, 'Student').subscribe({
      next: (data: any) => {
        const items = data?.items || data?.Items || (Array.isArray(data) ? data : []);
        this.studentCandidates.set(items);
      },
      error: (err) => console.error('Failed to load all students for manual assignment', err)
    });
  }

  loadEvents() {
    const companyId = this.authService.getCompanyId();
    if (companyId) {
      this.apiService.getEvents(companyId).subscribe({
        next: (events) => {
          this.companyEvents.set(events);
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

  constructor() {
    const companyId = this.authService.getCompanyId();
    if (!companyId) {
      console.warn('No active company context. Redirecting to selector.');
      this.router.navigate(['/select-event']);
      return;
    }
  }

  loadSettings() {
    const companyId = this.authService.getCompanyId();
    if (!companyId) return;

    this.apiService.getSetting('SurveyOpen', companyId).subscribe({
      next: (setting: any) => {
        if (setting && setting.value) {
          this.isSurveyOpen.set(setting.value.toLowerCase() === 'true');
        }
      },
      error: (err) => console.error('Failed to load settings', err)
    });

    this.apiService.getSetting('RoleSelectionEnabled', companyId).subscribe({
      next: (setting: any) => {
        if (setting && setting.value) {
          this.isRoleSelectionEnabled.set(setting.value.toLowerCase() === 'true');
        }
      },
      error: (err) => console.error('Failed to load role setting', err)
    });

    // Check if schedule feature is enabled
    this.apiService.getEventScheduleSettings(companyId).subscribe({
      next: (res: any) => {
        this.isScheduleEnabled.set(!!(res && res.isActive !== false && res.eventStartTime && res.eventEndTime));
      },
      error: () => this.isScheduleEnabled.set(false)
    });
  }

  toggleSurvey() {
    const companyId = this.authService.getCompanyId();
    if (!companyId) return;

    const newState = !this.isSurveyOpen();
    this.isSurveyOpen.set(newState);
    this.apiService.updateSetting('SurveyOpen', newState.toString()).subscribe({
      next: () => this.notify.success(newState ? 'The survey is now open.' : 'The survey is now closed.'),
      error: (err) => {
        console.error('Failed to update setting', err);
        this.notify.error('Failed to change survey status.');
        this.isSurveyOpen.set(!newState); // Revert on error
      }
    });
  }

  visitSurvey() {
    const companyId = this.authService.getCompanyId();
    const url = companyId ? `/?companyId=${companyId}` : '/';
    window.open(url, '_blank');
  }

  loadStats() {
    const companyId = this.authService.getCompanyId();
    if (!companyId) return;

    this.apiService.getStats(companyId).subscribe({
      next: (data) => this.feedbackStats.set(data),
      error: (err) => console.error('Failed to load stats', err)
    });
  }

  loadMutualMatches() {
    if (this.isLoadingMatches()) return;
    const companyId = this.authService.getCompanyId();
    if (!companyId) return;

    this.isLoadingMatches.set(true);
    this.apiService.getMutualMatches(this.matchesSearchQuery(), companyId).subscribe({
      next: (data: any[]) => {
        this.mutualMatches.set(data);
        this.isLoadingMatches.set(false);
      },
      error: (err) => {
        console.error('Failed to load mutual matches', err);
        this.isLoadingMatches.set(false);
      }
    });
  }

  onMatchesSearch(value: string) {
    this.matchesSearchQuery.set(value);
    this.loadMutualMatches();
  }

  switchTab(tab: 'participants' | 'results' | 'schedule') {
    this.activeTab.set(tab);
    // Reflect the active tab in the URL as a query param
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab },
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
    if (tab === 'results') {
      this.loadMutualMatches();
    } else if (tab === 'schedule') {
      this.loadGlobalSchedule();
    }
  }

  loadGlobalSchedule() {
    const eventId = this.authService.getCompanyId();
    if (!eventId) return;

    this.isLoadingSchedule.set(true);
    this.apiService.getAllEventSlots(eventId).subscribe({
      next: (slots: any[]) => {
        this.globalSlots.set(slots || []);
        this.isLoadingSchedule.set(false);
      },
      error: (err) => {
        console.error('Failed to load global schedule', err);
        this.isLoadingSchedule.set(false);
        this.notify.error('Failed to load global schedule.');
      }
    });
  }

  // Helpdesk Manual Assign Actions
  openAssignStudentModal(slot: any) {
    this.selectedSlot.set(slot);
    this.selectedStudentId.set('');
    this.showAssignModal.set(true);
    
    // If registrations are not fully loaded, maybe load them or rely on the fact that Helpdesk can search in the Participants tab first.
  }

  closeAssignModal() {
    this.showAssignModal.set(false);
    this.selectedSlot.set(null);
    this.selectedStudentId.set('');
  }

  confirmAssignStudent() {
    const slot = this.selectedSlot();
    const studentId = this.selectedStudentId();
    if (!slot || !studentId) return;

    this.service.assignStudentToSlot(slot.id, studentId).subscribe({
      next: () => {
        this.notify.success('Student assigned successfully.');
        this.closeAssignModal();
        this.loadGlobalSchedule();
      },
      error: (err) => {
        console.error('Failed to assign student', err);
        this.notify.error('Failed to assign student. ' + (err.error || ''));
      }
    });
  }

  unassignStudent(slot: any) {
    this.openConfirm('Unassign Student', 'Are you sure you want to remove the student from this slot?', () => {
      this.service.unassignHelpdeskStudentFromSlot(slot.id).subscribe({
        next: () => {
          this.notify.success('Student unassigned successfully.');
          this.loadGlobalSchedule();
        },
        error: (err) => {
          console.error('Failed to unassign student', err);
          this.notify.error('Failed to unassign student.');
        }
      });
    });
  }

  private loadSubscription?: Subscription;
  private loadRequestId = 0;

  loadRegistrations() {
    const companyId = this.authService.getCompanyId();
    if (!companyId) return;

    // Cancel any previous in-flight request
    if (this.loadSubscription) {
      this.loadSubscription.unsubscribe();
    }

    // Track which request is the latest — stale responses will be ignored
    const requestId = ++this.loadRequestId;

    this.isLoading.set(true);
    this.loadSubscription = this.apiService.getParticipantSummaries(this.page(), 20, this.searchQuery(), this.statusFilter(), companyId, this.roleFilter()).subscribe({
      next: (data: any) => {
        // Discard stale responses from older requests
        if (requestId !== this.loadRequestId) return;
        try {
          const items = data?.items || data?.Items || (Array.isArray(data) ? data : []);
          const tCount = data?.totalCount ?? data?.TotalCount ?? items.length;

          if (this.page() === 1) {
            this.registrations.set(items);
          } else {
            this.registrations.update(current => [...current, ...items]);
          }

          this.totalCount.set(tCount);
          this.hasMore.set(items.length === 20);
        } catch (e) {
          console.error('Error parsing registrations data:', e);
        } finally {
          if (requestId === this.loadRequestId) {
            this.isLoading.set(false);
          }
        }
      },
      error: (err) => {
        if (requestId !== this.loadRequestId) return;
        console.error('Failed to load registrations', err);
        this.isLoading.set(false);
      }
    });
  }

  onSearch(query: string) {
    this.searchQuery.set(query);
    this.resetAndLoad();
  }

  onStatusChange(status: string) {
    // Map display values to backend values
    const map: Record<string, string> = {
      'Matched': 'COMPLETED',
      'Answered': 'IN PROGRESS',
      'Not started': 'NOT STARTED',
      '': ''
    };
    this.statusFilter.set(map.hasOwnProperty(status) ? map[status] : '');
    this.resetAndLoad();
  }

  onRoleChange(role: string) {
    this.roleFilter.set(role);
    this.resetAndLoad();
  }

  private resetAndLoad() {
    this.page.set(1);
    this.hasMore.set(true);
    // Important: Scroll to top of the table container to prevent "ghost scroll" triggers
    const container = document.querySelector('.table-scroll-container');
    if (container) {
      container.scrollTop = 0;
    }
    // We clear registrations to avoid confusion and ensure scroll calculation is fresh
    this.registrations.set([]);
    this.loadRegistrations();
  }

  onScroll(event: any) {
    const element = event.target;
    // Check if scrolled near bottom (within 50px)
    if (element.scrollHeight - element.scrollTop <= element.clientHeight + 50 && this.hasMore() && !this.isLoading()) {
      this.page.update(p => p + 1);
      this.loadRegistrations();
    }
  }

  triggerUpload() {
    const fileInput = document.querySelector('input[type="file"]') as HTMLElement;
    fileInput?.click();
  }

  downloadTemplate() {
    this.apiService.getImportTemplate().subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = 'registration_template.xlsx';
        link.click();
        window.URL.revokeObjectURL(url);
      },
      error: (err) => {
        console.error('Failed to download template', err);
        this.notify.error('Failed to download template.');
      }
    });
  }

  onFileSelected(event: any) {
    const file: File = event.target.files[0];
    if (file) {
      if (!file.name.toLowerCase().endsWith('.xlsx')) {
        this.notify.warning('Only .xlsx files are allowed.');
        return;
      }

      this.isProcessing.set(true);
      this.processingMessage.set('Uploading and processing file...');
      this.notify.info('Uploading file... Please wait.');

      const companyId = this.authService.getCompanyId();
      this.apiService.uploadRegistrations(file, companyId || undefined).subscribe({
        next: (res: any) => {
          this.notify.success('Import successful! ' + res.message);
          this.page.set(1);
          this.loadRegistrations(); // Refresh list after import
          this.isProcessing.set(false);
          this.processingMessage.set('');
        },
        error: (err: any) => {
          console.error(err);
          this.notify.error('An error occurred during upload. Check console for details.');
          this.isProcessing.set(false);
          this.processingMessage.set('');
        }
      });
    }
  }



  exportCSV() {
    this.notify.info('Exporting CSV...');
  }

  // Modal State
  showConfirmModal = signal(false);
  confirmTitle = signal('');
  confirmMessage = signal('');
  private confirmCallback: (() => void) | null = null;

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

  sendInvitations() {
    this.openConfirm(
      'Send Invitations',
      'Do you want to send invitations to all participants?',
      () => {
        this.isProcessing.set(true);
        this.processingMessage.set('Sending invitations...');
        this.notify.info('Sending email invitations...');

        const companyId = this.authService.getCompanyId();
        this.apiService.sendInvitations(companyId || undefined).subscribe({
          next: (res: any) => {
            this.startProgressPolling(() => {
              this.notify.success(`Completed! Total invitations sent: ${this.processedCount()}`);
              this.isProcessing.set(false);
              this.processingMessage.set('');
            });
          },
          error: (err) => {
            console.error(err);
            this.notify.error('Failed to send invitations.');
            this.isProcessing.set(false);
            this.processingMessage.set('');
          }
        });
      }
    );
  }

  sendReminders() {
    this.openConfirm(
      'Send Reminders',
      'Do you want to send reminders to all participants who have not yet started their profile?',
      () => {
        this.isProcessing.set(true);
        this.processingMessage.set('Sending reminders...');
        this.notify.info('Sending reminders...');

        const companyId = this.authService.getCompanyId();
        this.apiService.sendReminders(companyId || undefined).subscribe({
          next: (res: any) => {
            this.startProgressPolling(() => {
              this.notify.success(`Completed! Total reminders sent: ${this.processedCount()}`);
              this.isProcessing.set(false);
              this.processingMessage.set('');
            });
          },
          error: (err) => {
            console.error(err);
            this.notify.error('Failed to send reminders.');
            this.isProcessing.set(false);
            this.processingMessage.set('');
          }
        });
      }
    );
  }

  sendResults() {
    const executeSend = () => {
      this.isProcessing.set(true);
      if (!this.processingMessage()) {
        this.processingMessage.set('Sending results...');
      }

      this.notify.info('Distributing matchmaking results via email...');
      const companyId = this.authService.getCompanyId();
      this.apiService.sendResults(companyId || undefined).subscribe({
        next: (res: any) => {
          this.startProgressPolling(() => {
            this.notify.success(`Completed! Total result emails sent: ${this.processedCount()}`);
            this.loadRegistrations(); // Refresh statuses after sending
            this.isProcessing.set(false); // Done
            this.processingMessage.set('');
          });
        },
        error: (err) => {
          console.error(err);
          this.notify.error('Failed to send matchings.');
          this.isProcessing.set(false); // Done (error)
          this.processingMessage.set('');
        }
      });
    };

    if (this.isProcessing()) {
      executeSend();
    } else {
      this.openConfirm(
        'Send Results',
        'Are you sure you want to send the results to all participants?',
        () => {
          executeSend();
        }
      );
    }
  }

  sendFeedbackRequests() {
    this.openConfirm(
      'Send Feedback Requests',
      'Do you want to send feedback requests to all participants who have been matched?',
      () => {
        this.isProcessing.set(true);
        this.processingMessage.set('Sending feedback requests...');
        this.notify.info('Distributing feedback requests via email...');

        const companyId = this.authService.getCompanyId();
        this.apiService.sendFeedbackRequests(companyId || undefined).subscribe({
          next: (res: any) => {
            this.startProgressPolling(() => {
              this.notify.success(`Completed! Total feedback requests sent: ${this.processedCount()}`);
              this.isProcessing.set(false);
              this.processingMessage.set('');
            });
          },
          error: (err) => {
            console.error(err);
            this.notify.error('Failed to send feedback requests.');
            this.isProcessing.set(false);
            this.processingMessage.set('');
          }
        });
      }
    );
  }

  sendIndividualFeedback(reg: any) {
    if (reg.bookedCount === 0) {
      this.notify.warning('This participant has no mutual matches.');
      return;
    }

    this.notify.info(`Sending feedback request to ${reg.name}...`);
    this.apiService.sendFeedbackRequest(reg.id).subscribe({
      next: () => {
        this.notify.success(`Feedback request sent to ${reg.name}`);
      },
      error: (err) => {
        console.error(err);
        this.notify.error(`Failed to send feedback request to ${reg.name}`);
      }
    });
  }


  testMatching() {
    this.openConfirm(
      'Test Matching',
      'This generates matchings in the database BUT does NOT send out any results. Do you want to continue?',
      () => {
        this.isProcessing.set(true);
        this.processingMessage.set('Generating test matchings...');
        this.notify.info('Running matchmaking algorithm...');

        const companyId = this.authService.getCompanyId();
        this.apiService.generateMatches(companyId || undefined).subscribe({
          next: (res: any) => {
            this.startProgressPolling(() => {
              this.notify.success('Matching complete! ' + res.message);
              this.page.set(1);
              this.loadRegistrations(); // Refresh table to show new match counts
              this.loadStats(); // Update stats
              this.isProcessing.set(false);
              this.processingMessage.set('');
            });
          },
          error: (err) => {
            console.error(err);
            this.stopProgressPolling();
            this.notify.error('Failed to generate matchings.');
            this.isProcessing.set(false);
          }
        });
      }
    );
  }

  deleteRegistration(id: string) {
    this.openConfirm(
      'Remove participant',
      'Are you sure you want to remove this participant? All related matches and chats will also be deleted.',
      () => {
        this.apiService.deleteRegistration(id).subscribe({
          next: () => {
            this.page.set(1);
            this.loadRegistrations(); // Refresh table
          },
          error: (err) => console.error('Failed to delete registration', err)
        });
      }
    );
  }




  isProcessing = signal(false);
  processingMessage = signal('');
  processingProgress = signal(0);
  processedCount = signal(0);
  totalCountProcessed = signal(0); // Renamed to avoid confusion with total registrations count
  estimatedTimeLeft = signal<string | null>(null);
  private progressInterval: any;
  private matchingSubscription: any;

  startProgressPolling(onComplete?: () => void) {
    this.processingProgress.set(0);
    this.estimatedTimeLeft.set(null);
    this.stopProgressPolling();
    const companyId = this.authService.getCompanyId();

    this.progressInterval = setInterval(() => {
      this.apiService.getMatchingProgress(companyId || undefined).subscribe({
        next: (data: any) => {
          const progress = Math.round(data.progress);
          this.processingProgress.set(progress);
          this.processedCount.set(data.processed || 0);
          this.totalCountProcessed.set(data.total || 0);

          if (data.estimatedTimeRemainingSeconds != null) {
            this.estimatedTimeLeft.set(this.formatTimeRemaining(data.estimatedTimeRemainingSeconds));
          } else {
            this.estimatedTimeLeft.set(null);
          }

          // Stop if task is no longer active and progress reached 100
          if (!data.isActive && progress >= 100) {
            this.stopProgressPolling();
            this.estimatedTimeLeft.set(null);
            if (onComplete) onComplete();
          }
        },
        error: (err) => {
          console.error('Progress polling error', err);
          this.stopProgressPolling();
          this.estimatedTimeLeft.set(null);
        }
      });
    }, 2000);
  }

  private formatTimeRemaining(seconds: number): string {
    if (seconds < 60) {
      return `approx ${Math.round(seconds)} sec left`;
    }
    const mins = Math.ceil(seconds / 60);
    if (mins === 1) return `approx 1 minute left`;
    return `approx ${mins} minutes left`;
  }

  cancelProcessing() {
    if (this.matchingSubscription) {
      this.matchingSubscription.unsubscribe();
      this.matchingSubscription = null;
    }
    const companyId = this.authService.getCompanyId();
    this.apiService.cancelMatching(companyId || undefined).subscribe({
      next: () => {
        this.notify.info('Matching cancelled.');
        this.stopProcessing();
      },
      error: (err: any) => {
        console.error('Failed to cancel matching', err);
        this.stopProcessing();
      }
    });
  }

  private stopProcessing() {
    this.isProcessing.set(false);
    this.processingMessage.set('');
    this.processingProgress.set(0);
    this.stopProgressPolling();
  }

  stopProgressPolling() {
    if (this.progressInterval) {
      clearInterval(this.progressInterval);
      this.progressInterval = null;
    }
  }

  startMatching() {
    this.openConfirm(
      'Start Matching',
      'Start matching? This will generate matchings for all participants based on their interests and needs. You can then review the results before sending them out.',
      () => {
        this.isProcessing.set(true);
        this.processingMessage.set('Matching in progress...');
        this.notify.info('Generating matches...');

        const companyId = this.authService.getCompanyId();
        this.matchingSubscription = this.apiService.generateMatches(companyId || undefined).subscribe({
          next: (res: any) => {
            this.startProgressPolling(() => {
              this.notify.success('Matching complete! You can now review the results.');
              this.page.set(1);
              this.loadRegistrations(); // Refresh table to show new match counts
              this.loadStats(); // Update stats
              this.isProcessing.set(false);
              this.processingMessage.set('');
            });
          },
          error: (err) => {
            console.error(err);
            this.stopProgressPolling();
            this.notify.error('Failed to generate matchings.');
            this.isProcessing.set(false); // Stop on error
          }
        });
      }
    );
  }

  deleteAllCandidates() {
    this.openConfirm(
      '⚠️ SUPPRESS EVERYTHING ⚠️',
      'WARNING: Are you sure you want to remove ALL participants? This also deletes all matches and chats. Registrations (email list) remain.',
      () => {
        const companyId = this.authService.getCompanyId();
        this.apiService.deleteAllRegistrations(companyId || undefined).subscribe({
          next: () => {
            this.notify.success('All participants have been removed.');
            this.page.set(1);
            this.registrations.set([]);
            this.loadRegistrations(); // Refresh table
          },
          error: (err) => {
            console.error('Failed to delete all candidates', err);
            this.notify.error('Failed to remove participants.');
          }
        });
      }
    );
  }

  // Add Participant Modal
  showAddModal = signal(false);
  isSavingParticipant = signal(false);
  newParticipant = signal({
    firstname: '',
    lastname: '',
    email: '',
    organization: '',
    title: '',
    sendInvite: true
  });

  openAddModal() {
    this.newParticipant.set({
      firstname: '',
      lastname: '',
      email: '',
      organization: '',
      title: '',
      sendInvite: true
    });
    this.showAddModal.set(true);
  }

  closeAddModal() {
    this.showAddModal.set(false);
  }

  saveParticipant() {
    const data = this.newParticipant();
    if (!data.email || !data.firstname || !data.lastname) {
      this.notify.warning('Name and email are required.');
      return;
    }
    const companyId = this.authService.getCompanyId();

    this.isSavingParticipant.set(true);
    this.apiService.addRegistration(data, data.sendInvite, companyId || undefined).subscribe({
      next: () => {
        this.notify.success(data.sendInvite ? 'Participant added and invitation sent!' : 'Participant added!');
        this.isSavingParticipant.set(false);
        this.closeAddModal();
        this.page.set(1);
        this.loadRegistrations();
      },
      error: (err: any) => {
        console.error('Failed to add participant', err);
        this.notify.error('Failed to add participant.');
        this.isSavingParticipant.set(false);
      }
    });
  }

  goToMatches(id: string) {
    if (id === '00000000-0000-0000-0000-000000000000') return;
    const url = `/matches?id=${id}`;
    window.open(url, '_blank');
  }

  previewUserView() {
    const companyId = this.authService.getCompanyId();
    if (!companyId) return;
    const url = `${window.location.origin}/?companyId=${companyId}`;
    window.open(url, '_blank');
  }

  switchAccount() {
    this.router.navigate(['/select-event']);
  }

  // Create Event Logic
  showCreateEventModal = signal(false);
  isCreatingEvent = signal(false);
  newEventData = signal({
    projectName: '',
    city: '',
    password: ''
  });

  openCreateEventModal() {
    this.newEventData.set({
      projectName: '',
      city: '',
      password: ''
    });
    this.showCreateEventModal.set(true);
  }

  closeCreateEventModal() {
    this.showCreateEventModal.set(false);
  }

  createEvent() {
    const data = this.newEventData();
    if (!data.projectName || !data.city || !data.password) {
      this.notify.warning('Please fill in all fields.');
      return;
    }

    this.isCreatingEvent.set(true);
    
    const currentCompanyId = this.authService.getCompanyId();
    // If we are in a dashboard, link this new event to the current company context
    this.authService.getMe().subscribe({
      next: (me) => {
        // Resolve the correct parentId for the new event:
        // 1. If the current company already has a parent, the new event shares that parent (sibling).
        // 2. If the current company has no parent, IT IS the parent for the new event.
        const parentId = me.parentId || currentCompanyId;

        const payload = {
          projectName: data.projectName,
          city: data.city,
          parentId: parentId,
          superAdmin: {
            firstName: me.firstName,
            lastName: me.lastName,
            email: me.username, // Using username (email) from the 'me' response
            password: data.password
          }
        };

        this.authService.setupSystem(payload).subscribe({
          next: (res: any) => {
            this.notify.success('New event created successfully!');
            this.isCreatingEvent.set(false);
            this.showCreateEventModal.set(false);
            
            // Navigate to event selector to see the new event in the list
            this.router.navigate(['/select-event']);
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
