import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DragDropModule, CdkDragDrop, moveItemInArray } from '@angular/cdk/drag-drop';
import { ApiService } from '../../../services/api.service';
import { AuthService } from '../../../services/auth.service';
import { NotificationService } from '../../../services/notification.service';
import { FilterByRolePipe } from '../../../pipes/filter-by-role.pipe';

@Component({
  selector: 'app-admin-questions',
  standalone: true,
  imports: [CommonModule, FormsModule, DragDropModule, FilterByRolePipe],
  templateUrl: './admin-questions.component.html',
  styleUrls: ['./admin-questions.component.css']
})
export class AdminQuestionsComponent {
  private apiService = inject(ApiService);
  private authService = inject(AuthService);
  private notify = inject(NotificationService);
  private router = inject(Router);
  public userRole = signal<string | null>(this.authService.getUserRole());

  questions = signal<any[]>([]);
  selectedQuestion = signal<any>(null);
  isNewQuestion = signal<boolean>(false);
  isLoading = signal<boolean>(false);
  isRoleSelectionEnabled = signal<boolean>(false);

  roleStudentName = signal<string>('Students Only');
  roleCompanyName = signal<string>('Projects / Exhibitors Only');

  constructor() {
    this.loadQuestions();
    this.loadSettings();
  }

  loadSettings() {
    const companyId = this.authService.getCompanyId();
    if (companyId) {
      this.apiService.getSetting('RoleSelectionEnabled', companyId).subscribe(s => {
        if (s && s.value) this.isRoleSelectionEnabled.set(s.value === 'true');
      });
      this.apiService.getSetting('RoleStudentName', companyId).subscribe(s => {
        if (s && s.value) this.roleStudentName.set(s.value + ' Only');
      });
      this.apiService.getSetting('RoleCompanyName', companyId).subscribe(s => {
        if (s && s.value) this.roleCompanyName.set(s.value + ' Only');
      });
    }
  }

  loadQuestions() {
    this.isLoading.set(true);
    const companyId = this.authService.getCompanyId();
    if (!companyId) {
       this.isLoading.set(false);
       return;
    }

    this.apiService.getQuestions(companyId, true).subscribe({
      next: (data) => {
        this.questions.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error(err);
        this.isLoading.set(false);
      }
    });
  }

  selectQuestion(q: any) {
    this.isNewQuestion.set(false);
    // Clone to avoid direct mutation before save
    const cloned = JSON.parse(JSON.stringify(q));
    if (!cloned.targetRole) {
      cloned.targetRole = 'All';
    }
    this.selectedQuestion.set(cloned);
  }

  updateTargetRole(val: string) {
    const q = this.selectedQuestion();
    if (q) {
      this.selectedQuestion.set({ ...q, targetRole: val });
    }
  }

  addOption() {
    const q = this.selectedQuestion();
    if (!q) return;

    if (!q.options) {
      q.options = [];
    }

    q.options.unshift({
      id: 0,
      questionId: q.id,
      title: '',
      description: '',
      value: '', // Ensure backend handles this or user inputs it
      icon: '✨',
      order: q.options.length + 1,
      isHidden: false
    });
  }

  removeOption(index: number) {
    const q = this.selectedQuestion();
    if (!q || !q.options) return;

    // Confirm? Maybe too annoying for simple UI, let's just delete from UI array
    q.options.splice(index, 1);
  }

  saveQuestion() {
    const q = this.selectedQuestion();
    if (!this.selectedQuestion().id || !this.selectedQuestion().title) {
      this.notify.error('ID and Title are required.');
      return;
    }

    // Update order based on current list position to be safe
    if (q.options) {
      q.options.forEach((opt: any, index: number) => {
        opt.order = index + 1;
      });
    }

    this.isLoading.set(true);
    const companyId = this.authService.getCompanyId();

    const action = this.isNewQuestion()
      ? this.apiService.createQuestion(q, companyId || undefined)
      : this.apiService.updateQuestion(q.id, q, companyId || undefined);

    action.subscribe({
      next: () => {
        this.notify.success(this.isNewQuestion() ? 'Question created!' : 'Question saved!');
        this.loadQuestions();
        this.isNewQuestion.set(false);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error(err);
        this.notify.error('Something went wrong. Check ID (if new).');
        this.isLoading.set(false);
      }
    });
  }

  cancelEdit() {
    this.selectedQuestion.set(null);
  }

  deleteQuestion(q: any, event: Event) {
    event.stopPropagation(); // prevent selecting the question card
    if (!confirm(`Are you sure you want to delete "${q.title}"? This cannot be undone.`)) {
      return;
    }

    this.isLoading.set(true);
    this.apiService.deleteQuestion(q.id).subscribe({
      next: () => {
        this.notify.success('Question deleted.');
        this.loadQuestions();
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error(err);
        this.notify.error('Could not delete the question.');
        this.isLoading.set(false);
      }
    });
  }

  addQuestion() {
    // Auto-generate next available question ID (e.g. q6, q7...)
    const existingIds = this.questions().map((q: any) => q.id as string);
    const existingNums = existingIds
      .map((id: string) => parseInt(id.replace(/^\D+/, ''), 10))
      .filter((n: number) => !isNaN(n));
    const nextNum = existingNums.length > 0 ? Math.max(...existingNums) + 1 : 1;
    const generatedId = `q${nextNum}`;

    this.isNewQuestion.set(true);
    this.selectedQuestion.set({
      id: generatedId,
      title: '',
      description: '',
      type: 'Choice',
      isHidden: false,
      maxLength: null,
      targetRole: 'All',
      options: []
    });
  }

  drop(event: CdkDragDrop<any[]>) {
    const list = [...this.questions()];
    moveItemInArray(list, event.previousIndex, event.currentIndex);
    this.questions.set(list);
    this.saveOrder();
  }

  dropOption(event: CdkDragDrop<any[]>) {
    const q = this.selectedQuestion();
    if (!q || !q.options) return;

    moveItemInArray(q.options, event.previousIndex, event.currentIndex);
    // Angular handles the reference update but we can be explicit or just let it be
  }

  private saveOrder() {
    const orders = this.questions().map((q, i) => ({
      id: q.id,
      order: i + 1
    }));

    const companyId = this.authService.getCompanyId();
    this.apiService.reorderQuestions(orders, companyId || undefined).subscribe({
      next: () => {
        this.notify.success('Order saved');
        this.loadQuestions(); // Ensure UI is perfectly in sync with DB order
      },
      error: () => this.notify.error('Could not save order')
    });
  }

  trackById(index: number, item: any) {
    return item.id;
  }

  previewSurvey() {
    const companyId = this.authService.getCompanyId();
    if (companyId) {
      window.open(`/?companyId=${companyId}`, '_blank');
    } else {
      window.open(`/`, '_blank');
    }
  }

  switchAccount() {
    this.router.navigate(['/select-company']);
  }
}
