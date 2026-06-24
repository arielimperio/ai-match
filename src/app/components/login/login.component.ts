import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css']
})
export class LoginComponent implements OnInit {
  authService = inject(AuthService);
  router = inject(Router);
  route = inject(ActivatedRoute);

  username = '';
  password = '';
  errorMessage = signal('');
  successMessage = signal('');
  showPassword = signal(false);

  ngOnInit() {
    this.route.queryParams.subscribe(params => {
      const token = params['verifyToken'];
      if (token) {
        this.authService.verifyEmail(token).subscribe({
          next: () => {
             this.successMessage.set('Email verified successfully! You can now log in.');
          },
          error: (err) => {
             this.errorMessage.set('Verification link is invalid or expired.');
          }
        });
      }
    });
  }

  login() {
    this.authService.login({ username: this.username, password: this.password }).subscribe({
      next: (res) => {
        const role = this.authService.getUserRole();
        const companies = res.companies || [];
        
        if (role === 'SuperAdmin' || companies.length > 1 || companies.length === 0) {
          this.router.navigate(['/select-event']);
        } else {
          this.router.navigate(['/admin/dashboard']);
        }
      },
      error: (err) => {
        console.error('Login failed', err);
        if (typeof err.error === 'string') {
          this.errorMessage.set(err.error);
        } else {
          this.errorMessage.set('Invalid username or password');
        }
      }
    });
  }

  togglePasswordVisibility() {
    this.showPassword.update(v => !v);
  }
}
