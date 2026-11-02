/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
import { modalStack } from './modal-stack-manager.js';

export class RecoveryKitModalController {
  constructor({ authService, toastService }) {
    this._auth = authService;
    this._toast = toastService;
    this._pending = null;
    this._elements = {
      modal: document.getElementById('recovery-kit-modal'),
      grid: document.getElementById('recovery-mnemonic-grid'),
      download: document.getElementById('btn-download-recovery-json'),
      copy: document.getElementById('btn-copy-mnemonic-phrase'),
      confirm: document.getElementById('chk-confirm-recovery-saved'),
      complete: document.getElementById('btn-complete-recovery-kit'),
      cancel: document.getElementById('btn-cancel-recovery-kit'),
      error: document.getElementById('recovery-kit-error'),
      subtitle: document.getElementById('recovery-kit-subtitle')
    };

    this._elements.download?.addEventListener('click', () => this._downloadJson());
    this._elements.copy?.addEventListener('click', () => this._copyMnemonic());
    this._elements.confirm?.addEventListener('change', () => {
      if (this._elements.complete) {
        this._elements.complete.disabled = !this._elements.confirm.checked;
      }
    });
    this._elements.complete?.addEventListener('click', () => this._complete());
    this._elements.cancel?.addEventListener('click', () => this._cancel());
  }

  show(recoveryData, { activationRequired = false, onComplete, onCancel } = {}) {
    const el = this._elements;
    if (!el.modal || !el.grid || !recoveryData?.mnemonicPhrase || !recoveryData?.jsonBackupPayload) {
      return false;
    }

    this._clearDom();
    this._returnFocusElement = document.activeElement;
    this._pending = { recoveryData, activationRequired, onComplete, onCancel };

    const words = recoveryData.mnemonicPhrase.trim().split(/\s+/);
    if (words.length !== 12) {
      this._pending = null;
      return false;
    }
    words.forEach((word, index) => {
      const pill = document.createElement('div');
      pill.className = 'mnemonic-pill';

      const number = document.createElement('span');
      number.className = 'mnemonic-num';
      number.textContent = `#${String(index + 1).padStart(2, '0')}`;

      const value = document.createElement('span');
      value.className = 'mnemonic-word';
      value.textContent = word;

      pill.append(number, value);
      el.grid.appendChild(pill);
    });

    if (el.confirm) el.confirm.checked = false;
    if (el.complete) {
      el.complete.disabled = true;
      el.complete.textContent = activationRequired
        ? 'I Saved My Key & Activate ➔'
        : 'I Have Saved My Key • Enter Dashboard ➔';
    }
    if (el.cancel) {
      el.cancel.textContent = activationRequired
        ? 'Keep current key and close'
        : 'Continue without saving';
    }
    if (el.subtitle) {
      el.subtitle.textContent = activationRequired
        ? 'Your current recovery key stays active until you confirm that this replacement is safely saved.'
        : 'Save your existing recovery kit now, or continue to the dashboard without downloading it.';
    }

    modalStack.push(el.modal);
    el.modal.style.display = 'flex';
    el.cancel?.focus();
    return true;
  }

  _downloadJson() {
    const kit = this._pending?.recoveryData;
    if (!kit?.jsonBackupPayload) return;

    const blob = new Blob([kit.jsonBackupPayload], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    const safeId = (kit.recoveryLookupId || 'kit').replace(/[^a-z0-9-]/gi, '');
    link.href = url;
    link.download = `diet-dost-disaster-recovery-${safeId}.json`;
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.setTimeout(() => URL.revokeObjectURL(url), 1000);
    this._toast?.show('Recovery kit download started. Confirm only after you have saved it safely.', 'success');
  }

  async _copyMnemonic() {
    const phrase = this._pending?.recoveryData?.mnemonicPhrase;
    if (!phrase) return;

    try {
      await navigator.clipboard.writeText(phrase);
      this._toast?.show('Recovery phrase copied. Save it somewhere secure before confirming.', 'success');
    } catch {
      this._toast?.show('Could not access the clipboard. Copy the words manually.', 'error');
    }
  }

  async _complete() {
    const pending = this._pending;
    if (!pending || !this._elements.confirm?.checked) return;

    const button = this._elements.complete;
    if (button) button.disabled = true;
    this._setError('');

    try {
      if (pending.activationRequired) {
        await this._auth.activateRecoveryKit(pending.recoveryData.recoveryKitIssueId);
      }

      const callback = pending.onComplete;
      this._close();
      callback?.();
    } catch (error) {
      this._setError(error.message || 'Could not confirm the recovery kit. Your current key remains active.');
      if (button) button.disabled = false;
    }
  }

  _cancel() {
    const callback = this._pending?.onCancel;
    this._close();
    callback?.();
  }

  _close() {
    const modal = this._elements.modal;
    if (modal) {
      modal.style.display = 'none';
      modalStack.pop(modal);
    }
    this._pending = null;
    this._clearDom();
    if (this._returnFocusElement?.isConnected) this._returnFocusElement.focus();
    this._returnFocusElement = null;
  }

  _clearDom() {
    const el = this._elements;
    if (el.grid) el.grid.replaceChildren();
    if (el.error) {
      el.error.textContent = '';
      el.error.style.display = 'none';
    }
    if (el.confirm) el.confirm.checked = false;
    if (el.complete) el.complete.disabled = true;
  }

  _setError(message) {
    const error = this._elements.error;
    if (!error) return;
    error.textContent = message;
    error.style.display = message ? 'block' : 'none';
  }
}
