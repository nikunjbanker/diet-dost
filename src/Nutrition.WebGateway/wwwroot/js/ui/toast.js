/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
/**
 * ToastNotificationService
 * Manages Obsidian Dark animated toasts with action buttons, status variants, and dismiss timers.
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
   * Supports both object syntax: show({ title, message, type, ... })
   * and shorthand string syntax: show('Message text', 'warning'|'success'|'error'|'info')
   *
   * @param {Object|string} optionsOrMessage
   * @param {string|Object} [typeOrOptions='info']
   */
  show(optionsOrMessage, typeOrOptions = 'info') {
    const parent = this.container;
    if (!parent) return;

    let opts = {};
    if (typeof optionsOrMessage === 'string') {
      opts.message = optionsOrMessage;
      if (typeof typeOrOptions === 'string') {
        opts.type = typeOrOptions;
      } else if (typeof typeOrOptions === 'object' && typeOrOptions !== null) {
        Object.assign(opts, typeOrOptions);
      }
    } else if (typeof optionsOrMessage === 'object' && optionsOrMessage !== null) {
      opts = { ...optionsOrMessage };
    }

    const type = opts.type || 'info';
    const typeIcons = {
      success: '✓',
      error: '✕',
      warning: '⚠',
      info: 'ℹ'
    };
    const defaultTitles = {
      success: 'Success',
      error: 'Error',
      warning: 'Notice',
      info: 'Information'
    };

    const icon = opts.icon || typeIcons[type] || 'ℹ';
    const title = opts.title ?? defaultTitles[type] ?? '';
    const message = opts.message || '';
    const duration = opts.duration ?? 6500;
    const actionText = opts.actionText;
    const onAction = opts.onAction;

    const toast = document.createElement('div');
    toast.className = `toast-item toast-${type}`;
    toast.innerHTML = `
      <div class="toast-icon">${icon}</div>
      <div class="toast-content">
        ${title ? `
        <div class="toast-title">
          <span>${title}</span>
          <button type="button" class="toast-close" title="Close">✕</button>
        </div>` : `
        <div style="display: flex; justify-content: flex-end;">
          <button type="button" class="toast-close" title="Close">✕</button>
        </div>`}
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
    if (duration > 0) {
      setTimeout(dismiss, duration);
    }
  }

  /**
   * Shorthand helper for success toasts.
   * @param {string} message
   * @param {string} [title='Success']
   */
  success(message, title = 'Success') {
    this.show({ title, message, type: 'success' });
  }

  /**
   * Shorthand helper for error toasts.
   * @param {string} message
   * @param {string} [title='Error']
   */
  error(message, title = 'Error') {
    this.show({ title, message, type: 'error' });
  }

  /**
   * Shorthand helper for warning toasts.
   * @param {string} message
   * @param {string} [title='Notice']
   */
  warning(message, title = 'Notice') {
    this.show({ title, message, type: 'warning' });
  }

  /**
   * Shorthand helper for informational toasts.
   * @param {string} message
   * @param {string} [title='Information']
   */
  info(message, title = 'Information') {
    this.show({ title, message, type: 'info' });
  }
}

export const toastService = new ToastNotificationService();
