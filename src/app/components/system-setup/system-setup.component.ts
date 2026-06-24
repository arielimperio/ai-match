import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { NotificationService } from '../../services/notification.service';

@Component({
  selector: 'app-system-setup',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './system-setup.component.html',
  styleUrls: ['./system-setup.component.css']
})
export class SystemSetupComponent {
  private authService = inject(AuthService);
  private notify = inject(NotificationService);
  private router = inject(Router);

  setupData = signal({
    firstName: '',
    lastName: '',
    email: '',
    password: '',
    confirmPassword: ''
  });

  isSubmitting = signal(false);
  showPassword = signal(false);
  showConfirmPassword = signal(false);

  togglePasswordVisibility() {
    this.showPassword.update(v => !v);
  }

  toggleConfirmPasswordVisibility() {
    this.showConfirmPassword.update(v => !v);
  }

  onSubmit() {
    const data = this.setupData();
    
    // Basic validation
    if (!data.firstName || !data.lastName || !data.email || !data.password) {
      this.notify.warning('Please fill in all required fields.');
      return;
    }

    if (data.password !== data.confirmPassword) {
      this.notify.warning('Passwords do not match.');
      return;
    }

    this.isSubmitting.set(true);

    const payload = {
      superAdmin: {
        firstName: data.firstName,
        lastName: data.lastName,
        email: data.email,
        password: data.password
      }
    };

    this.authService.setupSystem(payload).subscribe({
      next: (res: any) => {
        if (res.requiresVerification) {
          this.notify.success('Account created successfully! Please check your email to verify your account before logging in.');
        } else {
          this.notify.success('Account created successfully! You can log in immediately.');
        }
        this.isSubmitting.set(false);
        this.router.navigate(['/login']);
      },
      error: (err) => {
        console.error('Setup failed', err);
        const errorMessage = typeof err.error === 'string' ? err.error : 'Failed to create account. Please check the console for details.';
        this.notify.error(errorMessage);
        this.isSubmitting.set(false);
      }
    });
  }
}

export default SystemSetupComponent;
