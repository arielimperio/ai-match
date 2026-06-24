import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'filterByRole',
  standalone: true
})
export class FilterByRolePipe implements PipeTransform {
  transform(questions: any[], role: 'All' | 'Student' | 'Company'): any[] {
    if (!questions) return [];
    if (role === 'All') {
      return questions.filter(q => !q.targetRole || q.targetRole === 'All');
    }
    return questions.filter(q => q.targetRole === role);
  }
}
