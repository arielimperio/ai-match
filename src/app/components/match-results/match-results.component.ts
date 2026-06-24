import { Component, signal, inject, OnInit, OnDestroy, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatchmakingService } from '../../services/matchmaking.service';
import { Router, ActivatedRoute } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { NotificationService } from '../../services/notification.service';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { catchError, of } from 'rxjs';

interface Match {
  id: string; // Participant ID
  name: string;
  title: string;
  img: string;
  percentage: number;
  description: string;
  interested: boolean;
  mutual: boolean;
  matchId: string; // UserMatch ID
  feedback: number; // 1 for 👍, -1 for 👎, 0 for none
  superpower?: string;
  companyDescription?: string;
  bio?: string;
  company?: string;
}

@Component({
  selector: 'app-match-results',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './match-results.component.html',
  styleUrls: ['./match-results.component.css']
})
export class MatchResultsComponent implements OnInit, OnDestroy {
  private service = inject(MatchmakingService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private apiService = inject(ApiService);
  private authService = inject(AuthService);
  private sanitizer = inject(DomSanitizer);
  private notify = inject(NotificationService);

  // True when an admin is previewing the matches view for a participant
  isAdminPreview = signal(false);
  // True when the admin has sent this participant a feedback request email
  feedbackRequestSent = signal(false);

  activeChatId = signal<string | null>(null);
  chatMessages = signal<Record<string, any[] | undefined>>({});
  newMessage = '';
  matchComments = signal<Record<string, string>>({});

  matches = signal<Match[]>([]);
  isLoading = signal(true);
  error = signal('');
  participantName = signal<string>('');
  eventRating = signal<number>(0);
  eventComment = signal<string>('');
  eventFeedbackSubmitted = signal<boolean>(false);

  // Schedule State
  activeTab = signal<'matches' | 'schedule'>('matches');
  scheduleSlots = signal<any[]>([]);
  isGeneratingSlots = signal(false);
  hasScheduleConfig = signal(false); // To check if the admin has set up the event schedule
  participantRole = signal<string | null>(null); // 'Student', 'Company', or null
  isCompanyRole = computed(() => {
    const r = this.participantRole();
    if (!r) return true; // default to showing if role unknown (backwards compat)
    return r.toLowerCase() === 'company' || r.toLowerCase() === 'företag';
  });

  private pollingIntervals: any[] = [];

  dynamicLogo = signal<string | null>(null);
  sanitizedLogo = computed<SafeHtml | null>(() => {
    const logo = this.dynamicLogo();
    return logo ? this.sanitizer.bypassSecurityTrustHtml(logo) : null;
  });

  ngOnInit() {
    document.body.classList.add('matches-mode');

    // Detect if an admin is previewing this participant's matches
    this.isAdminPreview.set(this.authService.isLoggedIn());

    const queryId = this.route.snapshot.queryParamMap.get('id');
    let partId = queryId || this.service.currentParticipantId();

    if (!partId) {
      const stored = localStorage.getItem('participantId');
      if (stored) partId = stored;
    }

    if (!partId) {
      this.error.set('No profile found. Please start over.');
      this.isLoading.set(false);
      return;
    }

    // If we have a query ID, we MUST update the service's current ID to match the view
    // This ensures that 'isMe()' checks against the correct user (the one we are viewing)
    if (queryId) {
      this.service.currentParticipantId.set(queryId);
    } else if (this.service.currentParticipantId() === null && partId) {
      // Fallback: Set it if not set
      this.service.currentParticipantId.set(partId);
    }

    this.service.getParticipant(partId).subscribe({
      next: (data: any) => {
        if (data && data.name) {
          this.participantName.set(data.name);
        }
        if (data && data.companyId) {
          this.loadLogo(data.companyId);
          this.service.applyBranding(data.companyId);
        }
        if (data && data.eventRating) {
          this.eventRating.set(data.eventRating);
          this.eventComment.set(data.eventComment || '');
          this.eventFeedbackSubmitted.set(true);
        }
        // Show feedback card if admin sent them a feedback request email
        if (data?.feedbackRequestSent) {
          this.feedbackRequestSent.set(true);
        }
        // Set participant role to conditionally show schedule features
        if (data?.role) {
          this.participantRole.set(data.role);
        }
      },
      error: (err) => console.error('Failed to load participant', err)
    });

    this.loadMatches(partId);
    this.setupChatPolling();
    this.setupMatchesPolling(partId);
    // Logo loading is handled inside getParticipant subscribe

    // Also load schedule settings and slots
    this.checkEventScheduleConfig();
  }

  checkEventScheduleConfig() {
    const eventId = this.authService.getCompanyId();
    if (!eventId) return;

    this.apiService.getEventScheduleSettings(eventId).pipe(catchError(() => of(null))).subscribe({
      next: (res: any) => {
        if (res && res.isActive !== false && res.eventStartTime && res.eventEndTime) {
          this.hasScheduleConfig.set(true);
          // Only auto-generate/load slots if we are not just previewing
          const partId = this.service.currentParticipantId();
          if (partId) {
            this.loadScheduleSlots(eventId, partId);
          }
        }
      }
    });
  }

  loadScheduleSlots(eventId: string, partId: string) {
    this.service.getCompanySlots(eventId, partId).subscribe({
      next: (slots) => {
        this.scheduleSlots.set(slots || []);
      },
      error: (err) => console.error('Failed to load slots', err)
    });
  }

  generateSlots() {
    const eventId = this.authService.getCompanyId();
    const partId = this.service.currentParticipantId();
    if (!eventId || !partId) return;

    this.isGeneratingSlots.set(true);
    this.service.generateSlots(eventId, partId).subscribe({
      next: (slots) => {
        this.scheduleSlots.set(slots || []);
        this.isGeneratingSlots.set(false);
        this.notify.success('Schedule slots generated successfully!');
      },
      error: (err) => {
        this.isGeneratingSlots.set(false);
        if (err.status === 400 && err.error) {
          this.notify.error(err.error);
        } else {
          this.notify.error('Failed to generate slots. Make sure the admin has configured the schedule.');
        }
      }
    });
  }

  toggleSlotAvailability(slotId: string, isAvailable: boolean) {
    this.service.toggleSlotAvailability(slotId, isAvailable).subscribe({
      next: (updatedSlot: any) => {
        this.scheduleSlots.update(slots => slots.map(s => s.id === slotId ? { ...s, isAvailable: updatedSlot.isAvailable } : s));
      },
      error: () => this.notify.error('Failed to update slot')
    });
  }

  markNoShow(slotId: string, isNoShow: boolean) {
    this.service.markNoShow(slotId, isNoShow).subscribe({
      next: (updatedSlot: any) => {
        this.scheduleSlots.update(slots => slots.map(s => s.id === slotId ? { ...s, companyMarkedNoShow: updatedSlot.companyMarkedNoShow } : s));
      },
      error: () => this.notify.error('Failed to update slot')
    });
  }

  assignStudent(slotId: string, studentId: string) {
    this.service.assignStudentToSlot(slotId, studentId).subscribe({
      next: (updatedSlot: any) => {
        this.notify.success('Student assigned and notified!');
        // Reload slots
        const eventId = this.authService.getCompanyId();
        const partId = this.service.currentParticipantId();
        if (eventId && partId) this.loadScheduleSlots(eventId, partId);
      },
      error: (err) => {
        const msg = err.error || 'Failed to assign student.';
        this.notify.error(typeof msg === 'string' ? msg : 'Failed to assign student.');
      }
    });
  }

  loadLogo(companyId?: string) {
    this.apiService.getSetting('WelcomeLogo', companyId).pipe(catchError(() => of(null))).subscribe(s => {
      if (s && s.value) this.dynamicLogo.set(s.value);
    });
  }

  ngOnDestroy() {
    document.body.classList.remove('matches-mode');
    this.pollingIntervals.forEach(clearInterval);
  }

  loadMatches(partId: string) {
    this.service.getMatches(partId).subscribe({
      next: (data: any[]) => {
        const oldMatches = this.matches();
        const mappedMatches = data.map(m => ({
          id: m.id,
          name: m.name,
          title: m.title,
          img: m.img || `https://ui-avatars.com/api/?name=${m.name}&background=random`,
          percentage: m.percentage,
          description: m.description,
          interested: m.isInterested,
          mutual: m.isMutual,
          matchId: m.matchId,
          feedback: m.feedback || 0,
          superpower: m.superpower,
          companyDescription: m.companyDescription,
          bio: m.bio,
          company: m.company
        }));

        this.matches.set(mappedMatches);

        // Populate initial comments
        const comments: Record<string, string> = {};
        data.forEach(m => {
          if (m.feedbackReason) comments[m.id] = m.feedbackReason;
        });
        this.matchComments.set(comments);
        this.isLoading.set(false);

        // Auto-open chat if a NEW mutual match is found and no chat is active
        if (!this.activeChatId()) {
          const newMutual = mappedMatches.find(m => {
            const old = oldMatches.find(om => om.id === m.id);
            return m.mutual && (!old || !old.mutual);
          });
          if (newMutual) {
            this.openChat(newMutual);
          }
        }
      },
      error: (err) => {
        console.error(err);
        this.error.set('Could not fetch matches.');
        this.isLoading.set(false);
      }
    });
  }

  setupMatchesPolling(partId: string) {
    const interval = setInterval(() => {
      this.loadMatches(partId);
    }, 10000); // Poll matches every 10 seconds
    this.pollingIntervals.push(interval);
  }

  setupChatPolling() {
    // Poll every 5 seconds if a chat is active
    const interval = setInterval(() => {
      const activeId = this.activeChatId();
      if (!activeId) return;

      const match = this.matches().find(m => m.id === activeId);
      if (match && match.matchId && match.mutual) {
        this.service.getChat(match.matchId).subscribe(messages => {
          this.chatMessages.update(msgs => ({
            ...msgs,
            [activeId]: messages
          }));
        });
      }
    }, 5000);
    this.pollingIntervals.push(interval);
  }

  submitFeedback(match: Match, rating: number) {
    if (!match.matchId) return;

    const comment = this.matchComments()[match.id] || '';

    // Optimistic update
    this.matches.update(list => list.map(m =>
      m.id === match.id ? { ...m, feedback: rating } : m
    ));

    this.service.setFeedback(match.matchId, rating, comment)?.subscribe({
      next: () => {
        this.notify.success('Feedback saved!');
      },
      error: (err) => {
        console.error('Failed to submit feedback', err);
        // Revert
        this.matches.update(list => list.map(m =>
          m.id === match.id ? { ...m, feedback: 0 } : m
        ));
      }
    });
  }

  updateMatchComment(matchId: string, comment: string) {
    this.matchComments.update(cmts => ({
      ...cmts,
      [matchId]: comment
    }));
  }

  toggleInterest(match: Match) {
    // Optimistic update
    this.matches.update(list => list.map(m =>
      m.id === match.id ? { ...m, interested: true } : m
    ));

    this.service.setInterest(match.id)?.subscribe({
      next: (res: any) => {
        // Update with real data from backend
        const isMutual = res.status === 1; // MatchStatus.Matched = 1

        this.matches.update(list => {
          const updatedList = list.map(m =>
            m.id === match.id ? {
              ...m,
              matchId: res.id,
              mutual: isMutual,
              interested: true
            } : m
          );

          // If it's a mutual match, open the chat automatically
          if (isMutual) {
            const updatedMatch = updatedList.find(m => m.id === match.id);
            if (updatedMatch) {
              setTimeout(() => this.openChat(updatedMatch), 100);
            }
          }

          return updatedList;
        });
      },
      error: (err) => {
        console.error('Failed to set interest', err);
        // Revert on error
        this.matches.update(list => list.map(m =>
          m.id === match.id ? { ...m, interested: false } : m
        ));
      }
    });
  }

  removeInterest(match: Match) {
    // Optimistic update
    this.matches.update(list => list.map(m =>
      m.id === match.id ? { ...m, interested: false, mutual: false } : m
    ));

    this.service.removeInterest(match.id)?.subscribe({
      next: (res: any) => {
        this.notify.success('Interest removed');
        // Ensure state is in sync with backend result
        this.matches.update(list => list.map(m =>
          m.id === match.id ? {
            ...m,
            interested: false,
            mutual: false
          } : m
        ));

        // Close chat if it was open for this match
        if (this.activeChatId() === match.id) {
          this.activeChatId.set(null);
        }
      },
      error: (err) => {
        console.error('Failed to remove interest', err);
        this.notify.error('Could not remove interest');
        // Revert on error - back to interested
        this.matches.update(list => list.map(m =>
          m.id === match.id ? { ...m, interested: true, mutual: match.mutual } : m
        ));
      }
    });
  }

  openChat(match: Match) {
    if (this.activeChatId() === match.id) {
      this.activeChatId.set(null);
      return;
    }
    this.activeChatId.set(match.id);

    // Initial load
    if (match.matchId) {
      this.service.getChat(match.matchId).subscribe({
        next: (messages) => {
          this.chatMessages.update(msgs => ({
            ...msgs,
            [match.id]: messages
          }));
        },
        error: (err) => console.error('Failed to load chat', err)
      });
    }
  }

  sendMessage(match: Match) {
    if (!this.newMessage.trim() || !match.matchId) return;

    const content = this.newMessage;
    this.newMessage = ''; // Clear input immediately

    this.service.sendMessage(match.matchId, content)?.subscribe({
      next: (res: any) => {
        // Chat polling will pick up the new message, but we can also add it manually for responsiveness
        const newMsg = {
          senderId: this.service.currentParticipantId(),
          content: content,
          timestamp: new Date()
        };

        this.chatMessages.update(msgs => {
          const current = msgs[match.id] || [];
          return {
            ...msgs,
            [match.id]: [...current, newMsg]
          };
        });
      },
      error: (err) => {
        console.error('Failed to send message', err);
        this.newMessage = content; // Restore on error
      }
    });
  }

  isMe(senderId: string): boolean {
    return senderId === this.service.currentParticipantId();
  }

  setEventRating(n: number) {
    this.eventRating.set(n);
  }

  submitEventFeedback() {
    const partId = this.service.currentParticipantId();
    if (!partId || this.eventRating() === 0) return;

    this.apiService.submitEventFeedback(partId, this.eventRating(), this.eventComment()).subscribe({
      next: () => {
        this.eventFeedbackSubmitted.set(true);
        this.notify.success('Thank you for your feedback!');
      },
      error: (err) => {
        console.error('Failed to submit event feedback', err);
        this.notify.error('Could not save feedback. Please try again.');
      }
    });
  }
}
