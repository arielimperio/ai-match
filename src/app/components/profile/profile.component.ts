import { Component, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute } from '@angular/router';
import { MatchmakingService } from '../../services/matchmaking.service';
import { FormsModule } from '@angular/forms';
import { NotificationService } from '../../services/notification.service';
import { toSignal } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './profile.component.html',
  styleUrls: ['./profile.component.css']
})
export class ProfileComponent {
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  public service = inject(MatchmakingService);
  private notify = inject(NotificationService);

  constructor() {
    // Check for query params (legacy/backward compatibility if needed)
    this.route.queryParams.subscribe((params: any) => {
      if (params['regId']) {
        this.service.loadRegistration(params['regId']);
      }
    });
  }

  update(field: any, val: string) {
    this.service.updateProfile(field, val);
  }

  handlePhoto(files: FileList | null) {
    if (files && files[0]) {
      this.service.handlePhotoUpload(files[0]);
    }
  }

  isSubmitting = false;
  showChoiceModal = false;

  finish() {
    const answers = this.service.answers();

    if (!answers.firstName?.trim()) {
      this.notify.warning('Please enter your first name.');
      return;
    }
    if (!answers.lastName?.trim()) {
      this.notify.warning('Please enter your last name.');
      return;
    }
    if (!answers.email?.trim()) {
      this.notify.warning('Please enter your email.');
      return;
    }
    if (!answers.hasAcceptedTerms) {
      this.notify.warning('Please accept the data processing terms to continue.');
      return;
    }
    if (this.service.selectedRole() === 'Company') {
      this.submitAndNavigate('later');
      return;
    }

    if (this.service.selectedRole() === 'Student') {
      this.submitAndNavigate('now');
      return;
    }

    this.showChoiceModal = true;
  }

  cancelModal() {
    this.showChoiceModal = false;
  }

  submitAndNavigate(option: 'now' | 'later') {
    this.isSubmitting = true;
    const cid = this.route.snapshot.queryParams['companyId'] || localStorage.getItem('companyId') || undefined;
    this.service.submitRegistration(cid).subscribe({
      next: (response: any) => {
        this.isSubmitting = false;
        this.showChoiceModal = false;

        const partId = response?.participantId || response?.id || this.service.currentParticipantId();
        const queryParams = { ...this.route.snapshot.queryParams };
        if (partId) {
          queryParams['id'] = partId;
        }

        if (option === 'now') {
          this.router.navigate(['/matches'], { queryParams });
        } else {
          this.router.navigate(['/success'], { queryParams });
        }
      },
      error: (err: any) => {
        console.error('Submission failed', err);
        const errorMsg = err.error || 'Something went wrong while saving. Please try again.';
        this.notify.error(errorMsg);
        this.isSubmitting = false;
      }
    });
  }

  back() {
    const queryParams = this.route.snapshot.queryParams;
    const ids = this.service.questionIds();
    const lastId = ids[ids.length - 1];
    if (lastId) {
      this.router.navigate(['/question', lastId], { queryParams });
    } else {
      this.router.navigate(['/'], { queryParams });
    }
  }
}
