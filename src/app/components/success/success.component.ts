import { Component, inject, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatchmakingService } from '../../services/matchmaking.service';
import { ApiService } from '../../services/api.service';
import { QuestionOption } from '../../models';

@Component({
  selector: 'app-success',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './success.component.html',
  styleUrls: ['./success.component.css']
})
export class SuccessComponent {
  private router = inject(Router);
  public service = inject(MatchmakingService);
  private apiService = inject(ApiService);

  successMessage = signal<string>('Loading...');

  constructor() {
    const cid = localStorage.getItem('companyId') || undefined;
    this.apiService.getSetting('SuccessMessage', cid).subscribe({
      next: (res: any) => {
        if (res && res.value) {
          this.successMessage.set(res.value);
        } else {
          this.successMessage.set('We will send an email with your personal matches no later than 48 hours before the event opens.');
        }
      },
      error: () => {
        this.successMessage.set('We will send an email with your personal matches no later than 48 hours before the event opens.');
      }
    });
  }

  superpowerDisplay = computed(() => {
    const a = this.service.answers();
    const q1Val = a.dynamic['q1'];
    if (q1Val === 'other') return a.dynamicOther['q1'] || 'Other';
    if (q1Val) {
      const q1Content = this.service.content()['q1'];
      const opt = q1Content?.options?.find(o => o.id === q1Val);
      return opt ? opt.title : 'Not selected';
    }
    return 'Not selected';
  });

  companyDescriptionDisplay = computed(() => {
    return this.service.answers().dynamic['q5'] || '';
  });

  bioDisplay = computed(() => {
    return this.service.answers().dynamic['q4'] || '';
  });

  next() {
    const queryParams: any = { ...this.router.routerState.snapshot.root.queryParams };
    const partId = this.service.currentParticipantId();
    if (partId) queryParams.id = partId;

    this.service.resetAnswers();
    this.router.navigate(['/matches'], { queryParams });
  }
}
