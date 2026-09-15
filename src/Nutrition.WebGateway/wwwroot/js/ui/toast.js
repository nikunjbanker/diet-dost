/**
 * ToastNotificationService
 * Manages Obsidian Dark animated toasts with action buttons and dismiss timers.
 */
export class ToastNotificationService {
  constructor(containerId = 'toast-container') {
    this.containerId = containerId;
  }

  get container() {
    return document.getElementById(this.containerId);
  }

  /**
   * Display a styled toast notification.
   * @param {Object} options
   * @param {string} options.title
   * @param {string} options.message
   * @param {string} [options.actionText]
   * @param {Function} [options.onAction]
   * @param {number} [options.duration=6500]
   */
  show({ title, message, actionText, onAction, duration = 6500 }) {
    const parent = this.container;
    if (!parent) return;

    const toast = document.createElement('div');
    toast.className = 'toast-item';
    toast.innerHTML = `
      <div class="toast-icon">✓</div>
      <div class="toast-content">
        <div class="toast-title">
          <span>${title}</span>
          <button type="button" class="toast-close" title="Close">✕</button>
        </div>
        <div class="toast-body">${message}</div>
        ${actionText ? `<button type="button" class="toast-action-btn">${actionText}</button>` : ''}
      </div>
    `;

    const dismiss = () => {
      toast.classList.add('toast-leave');
      setTimeout(() => toast.remove(), 250);
    };

    const closeBtn = toast.querySelector('.toast-close');
    if (closeBtn) closeBtn.addEventListener('click', dismiss);

    const actionBtn = toast.querySelector('.toast-action-btn');
    if (actionBtn && onAction) {
      actionBtn.addEventListener('click', () => {
        try {
          onAction();
        } finally {
          dismiss();
        }
      });
    }

    parent.appendChild(toast);
    setTimeout(dismiss, duration);
  }
}

export const toastService = new ToastNotificationService();
