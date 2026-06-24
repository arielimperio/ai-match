import { Component, inject, computed } from '@angular/core';
import { Router } from '@angular/router';
import { MatchmakingService } from '../../services/matchmaking.service';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-text-question',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './text-question.component.html',
  styleUrls: ['./text-question.component.css']
})
export class TextQuestionComponent {
  private router = inject(Router);
  public service = inject(MatchmakingService);
  content = computed(() => this.service.content()['q4']);

  updateBio(val: string) {
    this.service.updateProfile('bio', val);
  }

  next() {
    this.router.navigate(['/q5']);
  }
}
