import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private http = inject(HttpClient);
  // Default .NET Web API port is usually 5000-5200. We'll assume http://localhost:5000 for now, 
  // but we should verify from the backend launch settings. 
  // For VS Code 'dotnet run', it usually picks a random port or reads from launchSettings.json.
  // I will assume standard 5000/5001 for now or 5110 based on typical .NET 8 templates.
  // Let's use a relative path proxy or environment variable in a real app, 
  // but for now I'll hardcode the likely port.
  // Actually, I should check the backend launchSettings.json to be sure.

  // Update: I will use a placeholder and we might need to adjust it. 
  private apiUrl = environment.apiUrl;

  uploadRegistrations(file: File, companyId?: string): Observable<any> {
    const formData = new FormData();
    formData.append('file', file);
    if (companyId) formData.append('companyId', companyId);

    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    const url = companyId ? `${this.apiUrl}/import/upload?companyId=${companyId}` : `${this.apiUrl}/import/upload`;

    return this.http.post(url, formData, { headers });
  }

  getImportTemplate(): Observable<Blob> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    return this.http.get(`${this.apiUrl}/import/template`, { 
      headers, 
      responseType: 'blob' 
    });
  }

  getRegistrations(companyId?: string): Observable<any[]> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    const url = companyId ? `${this.apiUrl}/registrations?companyId=${companyId}` : `${this.apiUrl}/registrations`;
    return this.http.get<any[]>(url, { headers });
  }

  exportRegistrations(companyId?: string): Observable<any[]> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    const url = companyId ? `${this.apiUrl}/registrations/export?companyId=${companyId}` : `${this.apiUrl}/registrations/export`;
    return this.http.get<any[]>(url, { headers });
  }

  getRegistration(id: string): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/registrations/${id}`);
  }

  getParticipantSummaries(page: number = 1, pageSize: number = 20, search: string = '', status: string = '', companyId?: string, role: string = ''): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (search) params = params.set('search', search);
    if (status) params = params.set('status', status);
    if (role) params = params.set('role', role);
    if (companyId) params = params.set('companyId', companyId);

    return this.http.get<any>(`${this.apiUrl}/registrations/summaries`, { headers, params });
  }

  getStats(companyId?: string): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    const url = companyId ? `${this.apiUrl}/registrations/stats?companyId=${companyId}` : `${this.apiUrl}/registrations/stats`;
    return this.http.get<any>(url, { headers });
  }

  deleteRegistration(id: string): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    return this.http.delete(`${this.apiUrl}/registrations/${id}`, { headers });
  }

  deleteAllRegistrations(companyId?: string): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    const url = companyId ? `${this.apiUrl}/registrations/all?companyId=${companyId}` : `${this.apiUrl}/registrations/all`;
    return this.http.delete(url, { headers });
  }

  getParticipant(id: string): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/registrations/participant/${id}`);
  }

  submitEventFeedback(participantId: string, rating: number, comment: string): Observable<any> {
    return this.http.post(
      `${this.apiUrl}/registrations/participant/${participantId}/event-feedback`,
      { rating, comment }
    );
  }

  // Questions
  getQuestions(companyId?: string, includeHidden: boolean = false): Observable<any[]> {
    const url = companyId 
      ? `${this.apiUrl}/Questions?companyId=${companyId}&includeHidden=${includeHidden}` 
      : `${this.apiUrl}/Questions?includeHidden=${includeHidden}`;
    return this.http.get<any[]>(url);
  }

  updateQuestion(id: string, question: any, companyId?: string): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    const url = companyId ? `${this.apiUrl}/Questions/${id}?companyId=${companyId}` : `${this.apiUrl}/Questions/${id}`;
    return this.http.put(url, question, { headers });
  }

  createQuestion(question: any, companyId?: string): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    const url = companyId ? `${this.apiUrl}/Questions?companyId=${companyId}` : `${this.apiUrl}/Questions`;
    return this.http.post(url, { ...question, companyId }, { headers });
  }

  deleteQuestion(id: string): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    return this.http.delete(`${this.apiUrl}/Questions/${id}`, { headers });
  }

  reorderQuestions(orders: { id: string, order: number }[], companyId?: string): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    const url = companyId ? `${this.apiUrl}/Questions/reorder?companyId=${companyId}` : `${this.apiUrl}/Questions/reorder`;
    return this.http.post(url, orders, { headers });
  }

  sendInvitations(companyId?: string): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    const url = companyId ? `${this.apiUrl}/Registrations/send-invitations?companyId=${companyId}` : `${this.apiUrl}/Registrations/send-invitations`;
    return this.http.post(url, {}, { headers });
  }

  sendReminders(companyId?: string): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    const url = companyId ? `${this.apiUrl}/Registrations/send-reminders?companyId=${companyId}` : `${this.apiUrl}/Registrations/send-reminders`;
    return this.http.post(url, {}, { headers });
  }

  sendResults(companyId?: string): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    const url = companyId ? `${this.apiUrl}/Registrations/send-results?companyId=${companyId}` : `${this.apiUrl}/Registrations/send-results`;
    return this.http.post(url, {}, { headers });
  }

  sendFeedbackRequests(companyId?: string): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    const url = companyId ? `${this.apiUrl}/Registrations/send-feedback-requests?companyId=${companyId}` : `${this.apiUrl}/Registrations/send-feedback-requests`;
    return this.http.post(url, {}, { headers });
  }

  sendFeedbackRequest(id: string): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    const url = `${this.apiUrl}/Registrations/send-feedback-request/${id}`;
    return this.http.post(url, {}, { headers });
  }



  generateMatches(companyId?: string): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    const url = companyId ? `${this.apiUrl}/Registrations/generate-matches?companyId=${companyId}` : `${this.apiUrl}/Registrations/generate-matches`;
    return this.http.post(url, {}, { headers });
  }

  seedData(): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    return this.http.post(`${this.apiUrl}/Seed`, {}, { headers });
  }

  getSetting(key: string, companyId?: string): Observable<any> {
    const url = companyId ? `${this.apiUrl}/settings/${key}?companyId=${companyId}` : `${this.apiUrl}/settings/${key}`;
    return this.http.get(url);
  }

  updateSetting(key: string, value: string, companyId?: string): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    const url = companyId ? `${this.apiUrl}/Settings/${key}?companyId=${companyId}` : `${this.apiUrl}/Settings/${key}`;
    const body: any = { Key: key, Value: value };
    if (companyId) {
      body.CompanyId = companyId;
    }
    return this.http.put(url, body, { headers });
  }

  sendTestEmail(payload: { type: string, email: string, template: string }, companyId?: string): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    const url = companyId ? `${this.apiUrl}/Settings/test-email?companyId=${companyId}` : `${this.apiUrl}/Settings/test-email`;
    return this.http.post(url, payload, { headers });
  }

  recommendEventSetup(description: string): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    return this.http.post(`${this.apiUrl}/auth/recommend-event-setup`, { description }, { headers });
  }

  createEvent(eventData: any): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    return this.http.post(`${this.apiUrl}/auth/create-event`, eventData, { headers });
  }

  matchFromCsv(file: File, companyId?: string): Observable<Blob> {
    const formData = new FormData();
    formData.append('file', file);
    if (companyId) formData.append('companyId', companyId);

    const token = sessionStorage.getItem('token');
    const url = companyId ? `${this.apiUrl}/AdminMatching/match-csv?companyId=${companyId}` : `${this.apiUrl}/AdminMatching/match-csv`;

    return this.http.post(url, formData, {
      headers: { 'Authorization': `Bearer ${token}` },
      responseType: 'blob'
    });
  }

  getMatchingProgress(companyId?: string): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    const url = companyId ? `${this.apiUrl}/registrations/matching-progress?companyId=${companyId}` : `${this.apiUrl}/registrations/matching-progress`;
    return this.http.get<any>(url, { headers });
  }

  resetMatches(companyId?: string): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    const url = companyId ? `${this.apiUrl}/Matches/reset?companyId=${companyId}` : `${this.apiUrl}/Matches/reset`;
    return this.http.delete(url, { headers });
  }

  cancelMatching(companyId?: string): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    const url = companyId ? `${this.apiUrl}/Registrations/cancel-matching?companyId=${companyId}` : `${this.apiUrl}/Registrations/cancel-matching`;
    return this.http.post(url, {}, { headers });
  }

  addRegistration(data: any, sendInvite: boolean, companyId?: string): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    const url = companyId ? `${this.apiUrl}/Registrations/add?companyId=${companyId}` : `${this.apiUrl}/Registrations/add`;
    return this.http.post(url, { ...data, sendInvite, companyId }, { headers });
  }

  importMatches(file: File, companyId?: string): Observable<any> {
    const formData = new FormData();
    formData.append('file', file);
    if (companyId) formData.append('companyId', companyId);
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    const url = companyId ? `${this.apiUrl}/import/matches?companyId=${companyId}` : `${this.apiUrl}/import/matches`;
    return this.http.post(url, formData, { headers });
  }

  importAiMatches(file: File): Observable<any> {
    const formData = new FormData();
    formData.append('file', file);
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    return this.http.post(`${this.apiUrl}/import/ai-matches`, formData, { headers });
  }

  mapMatches(file: File, companyId?: string): Observable<Blob> {
    const formData = new FormData();
    formData.append('file', file);
    if (companyId) formData.append('companyId', companyId);
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    const url = companyId ? `${this.apiUrl}/import/map-matches?companyId=${companyId}` : `${this.apiUrl}/import/map-matches`;
    return this.http.post(url, formData, { headers, responseType: 'blob' });
  }

  exportMatches(companyId?: string): Observable<Blob> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    const url = companyId ? `${this.apiUrl}/matches/export?companyId=${companyId}` : `${this.apiUrl}/matches/export`;
    return this.http.get(url, { headers, responseType: 'blob' });
  }

  getMutualMatches(search: string = '', companyId?: string): Observable<any[]> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    let url = `${this.apiUrl}/Matches/mutual?search=${encodeURIComponent(search)}`;
    if (companyId) url += `&companyId=${companyId}`;
    return this.http.get<any[]>(url, { headers });
  }


  deleteCompany(id: string): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    return this.http.delete(`${this.apiUrl}/auth/companies/${id}`, { headers });
  }

  resendVerification(userId: string): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    return this.http.post(`${this.apiUrl}/auth/resend-verification/${userId}`, {}, { headers });
  }

  manualVerifyAccount(userId: string): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    return this.http.post(`${this.apiUrl}/auth/manual-verify/${userId}`, {}, { headers });
  }

  deactivateAccount(userId: string): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    return this.http.post(`${this.apiUrl}/auth/deactivate/${userId}`, {}, { headers });
  }

  getEvents(companyId: string): Observable<any[]> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    return this.http.get<any[]>(`${this.apiUrl}/auth/companies/${companyId}/events`, { headers });
  }

  // Admin Management
  getAdmins(companyId?: string): Observable<any[]> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    const url = companyId ? `${this.apiUrl}/administrators/fetch?companyId=${companyId}` : `${this.apiUrl}/administrators/fetch`;
    return this.http.post<any[]>(url, {}, { headers });
  }

  addAdmin(admin: any): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    return this.http.post(`${this.apiUrl}/administrators/add`, admin, { headers });
  }

  updateAdmin(id: string, admin: any): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    return this.http.put(`${this.apiUrl}/administrators/update/${id}`, admin, { headers });
  }

  deleteAdmin(id: string, companyId?: string): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    const url = companyId ? `${this.apiUrl}/administrators/delete/${id}?companyId=${companyId}` : `${this.apiUrl}/administrators/delete/${id}`;
    return this.http.delete(url, { headers });
  }

  // Schedule Management
  getEventScheduleSettings(eventId: string): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    return this.http.get(`${this.apiUrl}/Schedule/event/${eventId}`, { headers });
  }

  updateEventScheduleSettings(eventId: string, settings: any): Observable<any> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    return this.http.post(`${this.apiUrl}/Schedule/event/${eventId}`, settings, { headers });
  }

  getAllEventSlots(eventId: string): Observable<any[]> {
    const token = sessionStorage.getItem('token');
    const headers = { 'Authorization': `Bearer ${token}` };
    return this.http.get<any[]>(`${this.apiUrl}/Schedule/event/${eventId}/slots`, { headers });
  }
}
