import { Injectable, inject, EventEmitter } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private apiUrl = `${environment.apiUrl}/auth`;
  private http = inject(HttpClient);
  private router = inject(Router);
  
  public logoChanged$ = new EventEmitter<void>();

  triggerLogoRefresh() {
    this.logoChanged$.emit();
  }

  login(credentials: any): Observable<any> {
    return this.http.post<{ token: string, companies: any[] }>(`${this.apiUrl}/login`, credentials)
      .pipe(
        tap(response => {
          sessionStorage.setItem('token', response.token);
          if (response.companies) {
            sessionStorage.setItem('companies', JSON.stringify(response.companies));
          }
        })
      );
  }

  setupSystem(data: any): Observable<any> {
    return this.http.post(`${this.apiUrl}/setup`, data);
  }

  verifyEmail(token: string): Observable<any> {
    return this.http.get(`${this.apiUrl}/verify-email?token=${encodeURIComponent(token)}`);
  }

  logout() {
    sessionStorage.removeItem('token');
    this.router.navigate(['/login']);
  }

  forgotPassword(email: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/forgot-password`, { email });
  }

  resetPassword(email: string, token: string, newPassword: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/reset-password`, { email, token, newPassword });
  }

  isLoggedIn(): boolean {
    return !!sessionStorage.getItem('token');
  }

  getToken(): string | null {
    return sessionStorage.getItem('token');
  }

  getCurrentUser(): string | null {
    const token = this.getToken();
    if (!token) return null;
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      // The API uses ClaimTypes.NameIdentifier or specific sub claim for username.
      // Usually it's in the 'sub' or 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier' field.
      return payload.sub || payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] || null;
    } catch (e) {
      return null;
    }
  }

  updatePassword(currentPassword: string, newPassword: string): Observable<any> {
    const token = this.getToken();
    const headers = { 'Authorization': `Bearer ${token}` };
    return this.http.patch(`${this.apiUrl}/update-password`, { currentPassword, newPassword }, { headers });
  }

  getCompanies(): Observable<any[]> {
    const token = this.getToken();
    const headers = { 'Authorization': `Bearer ${token}` };
    return this.http.get<any[]>(`${this.apiUrl}/companies`, { headers });
  }

  updateCompany(id: string, name: string): Observable<any> {
    const token = this.getToken();
    const headers = { 'Authorization': `Bearer ${token}` };
    return this.http.put(`${this.apiUrl}/companies/${id}`, { name }, { headers });
  }

  getMe(): Observable<any> {
    const token = this.getToken();
    const headers = { 'Authorization': `Bearer ${token}` };
    return this.http.get<any>(`${this.apiUrl}/me`, { headers });
  }

  switchCompany(companyId: string): Observable<any> {
    const token = this.getToken();
    const headers = { 'Authorization': `Bearer ${token}` };
    return this.http.post<{ token: string }>(`${this.apiUrl}/switch-company/${companyId}`, {}, { headers })
      .pipe(
        tap(response => {
          sessionStorage.setItem('token', response.token);
        })
      );
  }

  getUserRole(): string | null {
    const token = this.getToken();
    if (!token) return null;
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || payload.role || null;
    } catch (e) {
      return null;
    }
  }

  getCompanyId(): string | null {
    const token = this.getToken();
    if (!token) return null;
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload.companyId || null;
    } catch (e) {
      return null;
    }
  }
}
