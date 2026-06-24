import { Component, inject, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatchmakingService } from '../../services/matchmaking.service';
import { ApiService } from '../../services/api.service';
import { toSignal } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-question',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './question.component.html',
  styleUrls: ['./question.component.css']
})
export class QuestionComponent {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  public service = inject(MatchmakingService);
  private apiService = inject(ApiService);

  constructor() {
    const companyId = this.route.snapshot.queryParams['companyId'];
    
    this.apiService.getSetting('SurveyOpen', companyId).subscribe({
      next: (s: any) => {
        if (s && s.value && s.value.toLowerCase() === 'false') {
          this.router.navigate(['/'], { queryParams: { companyId } });
        }
      }
    });

    // Ensure questions are loaded if they are missing (e.g. on direct navigation)
    if (this.service.questionIds().length === 0) {
      this.service.loadQuestions(companyId);
    }

    this.loadCustomLabels(companyId);
  }

  backLabel = signal<string>('Back');
  nextLabel = signal<string>('Next');
  selectedText = signal<string>('selected');
  otherLabel = signal<string>('Other:');
  otherPlaceholder = signal<string>('Please specify...');

  loadCustomLabels(companyId?: string) {
    const keys = ['QuestionBackButton', 'QuestionNextButton', 'QuestionSelectedText', 'QuestionOtherLabel', 'QuestionOtherPlaceholder'];
    keys.forEach(key => {
      this.apiService.getSetting(key, companyId).subscribe({
        next: (res: any) => {
          const val = res?.value || '';
          if (key === 'QuestionBackButton') this.backLabel.set(val || 'Back');
          else if (key === 'QuestionNextButton') this.nextLabel.set(val || 'Next');
          else if (key === 'QuestionSelectedText') this.selectedText.set(val || 'selected');
          else if (key === 'QuestionOtherLabel') this.otherLabel.set(val || 'Other:');
          else if (key === 'QuestionOtherPlaceholder') this.otherPlaceholder.set(val || 'Please specify...');
        },
        error: () => {
          // Keep blank on error
        }
      });
    });
  }

  qId = toSignal(this.route.params);

  currentQId = computed(() => (this.qId() as any)?.['id'] || 'q1');
  content = computed(() => this.service.content()[this.currentQId()]);

  progress = computed(() => {
    const id = this.currentQId();
    const ids = this.service.questionIds();
    const idx = ids.indexOf(id);
    return ((idx + 1) / (ids.length + 1)) * 100;
  });

  otherValue = computed(() => {
    const q = this.currentQId();
    const a = this.service.answers();
    return a.dynamicOther[q] || '';
  });

  isSelected(optId: string): boolean {
    return this.service.isSelected(this.currentQId(), optId);
  }

  selectOption(optId: string) {
    this.service.selectOption(this.currentQId(), optId);
  }

  updateText(event: Event) {
    const val = (event.target as HTMLTextAreaElement).value;
    this.service.selectOption(this.currentQId(), val);
  }

  updateOther(event: Event) {
    const val = (event.target as HTMLInputElement).value;
    const q = this.currentQId();
    this.service.updateOther(q, val);
  }

  next() {
    const queryParams = this.route.snapshot.queryParams;
    const ids = this.service.questionIds();
    const current = this.currentQId();
    const currentIdx = ids.indexOf(current);

    if (currentIdx < ids.length - 1) {
      const nextId = ids[currentIdx + 1];
      this.router.navigate(['/question', nextId], { queryParams });
    } else {
      this.router.navigate(['/profile'], { queryParams });
    }
  }

  back() {
    const queryParams = this.route.snapshot.queryParams;
    const ids = this.service.questionIds();
    const current = this.currentQId();
    const currentIdx = ids.indexOf(current);

    if (currentIdx > 0) {
      const prevId = ids[currentIdx - 1];
      this.router.navigate(['/question', prevId], { queryParams });
    } else {
      this.router.navigate(['/'], { queryParams });
    }
  }
}
