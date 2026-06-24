import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NotificationService, NotificationType } from '../../../services/notification.service';

@Component({
  selector: 'app-notification',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="notification-container">
      @for (n of service.notifications(); track n.id) {
        <div class="notification" [class]="n.type" (click)="service.remove(n.id)">
          <div class="icon">
            @if (n.type === 'success') { ✓ }
            @else if (n.type === 'error') { ✕ }
            @else if (n.type === 'warning') { ⚠ }
            @else { ℹ }
          </div>
          <div class="message">{{ n.message }}</div>
          <div class="close">×</div>
        </div>
      }
    </div>
  `,
  styles: [`
    .notification-container {
      position: fixed;
      top: 24px;
      right: 24px;
      z-index: 9999;
      display: flex;
      flex-direction: column;
      gap: 12px;
      max-width: 400px;
      width: calc(100% - 48px);
    }

    .notification {
      display: flex;
      align-items: center;
      gap: 16px;
      padding: 16px 20px;
      border-radius: 16px;
      background: rgba(15, 23, 42, 0.8);
      backdrop-filter: blur(12px);
      -webkit-backdrop-filter: blur(12px);
      border: 1px solid rgba(255, 255, 255, 0.1);
      box-shadow: 0 8px 32px rgba(0, 0, 0, 0.3);
      color: white;
      cursor: pointer;
      animation: slideIn 0.4s cubic-bezier(0.16, 1, 0.3, 1), fadeIn 0.3s ease-out;
      transition: transform 0.2s, opacity 0.2s;
    }

    .notification:hover {
      transform: translateY(-2px);
      background: rgba(30, 41, 59, 0.9);
    }

    .notification:active {
      transform: scale(0.98);
    }

    .icon {
      width: 28px;
      height: 28px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 16px;
      font-weight: bold;
      flex-shrink: 0;
    }

    .success .icon { background: #10b981; color: white; }
    .error .icon { background: #ef4444; color: white; }
    .warning .icon { background: #f59e0b; color: white; }
    .info .icon { background: #3b82f6; color: white; }

    .message {
      flex: 1;
      font-size: 14px;
      font-weight: 500;
      line-height: 1.4;
    }

    .close {
      font-size: 20px;
      opacity: 0.5;
      margin-left: 8px;
    }

    .notification.success { border-left: 4px solid #10b981; }
    .notification.error { border-left: 4px solid #ef4444; }
    .notification.warning { border-left: 4px solid #f59e0b; }
    .notification.info { border-left: 4px solid #3b82f6; }

    @keyframes slideIn {
      from { transform: translateX(100%) scale(0.9); }
      to { transform: translateX(0) scale(1); }
    }

    @keyframes fadeIn {
      from { opacity: 0; }
      to { opacity: 1; }
    }
  `]
})
export class NotificationComponent {
  public service = inject(NotificationService);
}
