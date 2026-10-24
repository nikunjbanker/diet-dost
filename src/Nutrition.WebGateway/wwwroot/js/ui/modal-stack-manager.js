/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

/**
 * ModalStackManager
 * Provides strict LIFO modal layering, dynamic z-index assignment, body scroll lock management,
 * and seamless multi-modal stacking (e.g. sub-modals, edit dialogs, delete confirmations, and lightboxes).
 */
export class ModalStackManager {
  constructor(baseZIndex = 100010, step = 20) {
    this._stack = [];
    this._baseZIndex = baseZIndex;
    this._step = step;
    this._bindGlobalKeydown();
  }

  /**
   * Pushes a modal element onto the stack, calculates its z-index, and ensures body scroll lock.
   * @param {HTMLElement} modalEl
   * @returns {number} Assigned z-index
   */
  push(modalEl) {
    if (!modalEl) return this._baseZIndex;

    // Avoid duplicate entries of the same modal
    const existingIndex = this._stack.indexOf(modalEl);
    if (existingIndex !== -1) {
      this._stack.splice(existingIndex, 1);
    }

    this._stack.push(modalEl);
    const assignedZ = this._baseZIndex + (this._stack.length - 1) * this._step;
    modalEl.style.zIndex = `${assignedZ}`;

    if (this._stack.length === 1) {
      document.body.classList.add('modal-open');
    }

    return assignedZ;
  }

  /**
   * Removes a modal element from the stack and restores body scroll lock once the stack is empty.
   * @param {HTMLElement} modalEl
   */
  pop(modalEl) {
    if (!modalEl) return;

    const idx = this._stack.indexOf(modalEl);
    if (idx !== -1) {
      this._stack.splice(idx, 1);
    }

    if (this._stack.length === 0) {
      document.body.classList.remove('modal-open');
    }
  }

  /**
   * Returns the topmost active modal in the stack.
   * @returns {HTMLElement|null}
   */
  top() {
    return this._stack.length > 0 ? this._stack[this._stack.length - 1] : null;
  }

  /**
   * Returns current active modal count.
   * @returns {number}
   */
  get count() {
    return this._stack.length;
  }

  /**
   * Clears the entire stack and unlocks body scroll.
   */
  clear() {
    this._stack = [];
    document.body.classList.remove('modal-open');
  }

  _bindGlobalKeydown() {
    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape' && this._stack.length > 0) {
        const topEl = this.top();
        if (topEl) {
          // Find standard close button in topmost modal if available
          const closeBtn = topEl.querySelector('#btn-close-lightbox, .btn-close, .modal-close-btn, #btn-cancel-create-user, #btn-close-edit-user-modal, #btn-review-header-close');
          if (closeBtn && typeof closeBtn.click === 'function') {
            closeBtn.click();
          }
        }
      }
    });
  }
}

export const modalStack = new ModalStackManager();
if (typeof window !== 'undefined') {
  window.modalStack = modalStack;
}
