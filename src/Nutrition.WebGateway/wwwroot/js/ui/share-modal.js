/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

import { modalStack } from './modal-stack-manager.js';

/**
 * ShareModalController
 * Controller for the Obsidian-dark Social Share & Export Studio.
 * Manages card preview rendering, aspect ratio toggling, 1-click clipboard image copy,
 * direct web intents (WhatsApp Web, X, Facebook, Email), and PNG downloads.
 */
export class ShareModalController {
  /**
   * @param {Object} options
   * @param {import('../services/share-service.js').ShareService} options.shareService
   * @param {import('../core/event-bus.js').EventBus} options.eventBus
   * @param {import('./toast.js').ToastService} [options.toastService]
   */
  constructor({ shareService, eventBus, toastService = null }) {
    this._service = shareService;
    this._bus = eventBus;
    this._toast = toastService;

    this._currentType = 'meal'; // 'meal' | 'daily'
    this._currentData = null;
    this._currentText = '';
    this._currentBlob = null;
    this._currentDataUrl = null;
    this._aspectRatio = 'square'; // 'square' | 'story'

    this._bindBusEvents();
  }

  get elements() {
    return {
      modal: document.getElementById('share-modal'),
      title: document.getElementById('share-modal-title'),
      subtitle: document.getElementById('share-modal-subtitle'),
      cardImg: document.getElementById('share-card-img'),
      previewFrame: document.getElementById('share-card-preview-frame'),
      loading: document.getElementById('share-card-loading'),
      btnClose: document.getElementById('btn-close-share-modal'),
      btnAspectSquare: document.getElementById('btn-aspect-square'),
      btnAspectStory: document.getElementById('btn-aspect-story'),
      togglePhoto: document.getElementById('share-toggle-photo'),
      togglePhotoRow: document.getElementById('toggle-photo-row'),
      toggleAdvice: document.getElementById('share-toggle-advice'),
      toggleAdviceRow: document.getElementById('toggle-advice-row'),
      toggleAnonymize: document.getElementById('share-toggle-anonymize'),
      btnCopyImage: document.getElementById('btn-copy-share-image'),
      btnWhatsApp: document.getElementById('btn-share-whatsapp-web'),
      btnDownload: document.getElementById('btn-download-share-card'),
      btnCopyText: document.getElementById('btn-copy-share-text'),
      btnFacebook: document.getElementById('btn-share-facebook'),
      btnTwitter: document.getElementById('btn-share-twitter'),
      btnEmail: document.getElementById('btn-share-email'),
      btnSystemShare: document.getElementById('btn-system-share'),
      appLinkDisplay: document.getElementById('share-app-link-display')
    };
  }

  _bindBusEvents() {
    if (this._bus) {
      this._bus.on('share:open', (payload) => this.open(payload));
    }
  }

  _initEvents() {
    const el = this.elements;
    if (!el.modal || this._eventsBound) return;
    this._eventsBound = true;

    if (el.btnClose) el.btnClose.addEventListener('click', () => this.close());
    el.modal.addEventListener('click', (e) => {
      if (e.target === el.modal) this.close();
    });

    // Aspect ratio toggles
    if (el.btnAspectSquare) {
      el.btnAspectSquare.addEventListener('click', () => this._setAspectRatio('square'));
    }
    if (el.btnAspectStory) {
      el.btnAspectStory.addEventListener('click', () => this._setAspectRatio('story'));
    }

    // Customization toggles
    if (el.togglePhoto) el.togglePhoto.addEventListener('change', () => this._rerender());
    if (el.toggleAdvice) el.toggleAdvice.addEventListener('change', () => this._rerender());
    if (el.toggleAnonymize) el.toggleAnonymize.addEventListener('change', () => this._rerender());

    // Action buttons
    if (el.btnCopyImage) {
      el.btnCopyImage.addEventListener('click', async () => {
        if (!this._currentBlob) return;
        const success = await this._service.copyImageToClipboard(this._currentBlob);
        if (!success && this._toast) {
          this._toast.info('Clipboard image copy not supported on this browser. Use "Download PNG".');
        }
      });
    }

    if (el.btnCopyText) {
      el.btnCopyText.addEventListener('click', async () => {
        await this._service.copyTextToClipboard(this._currentText);
      });
    }

    if (el.btnWhatsApp) {
      el.btnWhatsApp.addEventListener('click', () => {
        const text = encodeURIComponent(this._currentText);
        const url = `https://web.whatsapp.com/send?text=${text}`;
        window.open(url, '_blank', 'noopener,noreferrer');
      });
    }

    if (el.btnDownload) {
      el.btnDownload.addEventListener('click', () => {
        if (!this._currentDataUrl) return;
        const filename = this._currentType === 'meal'
          ? `diet-dost-meal-${Date.now()}.png`
          : `diet-dost-daily-${Date.now()}.png`;
        const a = document.createElement('a');
        a.href = this._currentDataUrl;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        if (this._toast) this._toast.success('Card image downloaded!');
      });
    }

    if (el.btnFacebook) {
      el.btnFacebook.addEventListener('click', () => {
        const shareUrl = encodeURIComponent(this._service.appUrl);
        const quote = encodeURIComponent(this._currentText);
        window.open(`https://www.facebook.com/sharer/sharer.php?u=${shareUrl}&quote=${quote}`, '_blank', 'noopener,noreferrer');
      });
    }

    if (el.btnTwitter) {
      el.btnTwitter.addEventListener('click', () => {
        const text = encodeURIComponent(this._currentText);
        window.open(`https://twitter.com/intent/tweet?text=${text}`, '_blank', 'noopener,noreferrer');
      });
    }

    if (el.btnEmail) {
      el.btnEmail.addEventListener('click', () => {
        const subject = encodeURIComponent(this._currentType === 'meal'
          ? `Diet Dost - Meal Nutrition Summary`
          : `Diet Dost - Daily Nutrition Pulse Summary`);
        const body = encodeURIComponent(this._currentText);
        window.location.href = `mailto:?subject=${subject}&body=${body}`;
      });
    }

    if (el.btnSystemShare) {
      el.btnSystemShare.addEventListener('click', async () => {
        if (!this._currentBlob) return;
        try {
          const file = new File([this._currentBlob], 'diet-dost-share.png', { type: 'image/png' });
          if (navigator.canShare && navigator.canShare({ files: [file] })) {
            await navigator.share({
              title: 'Diet Dost Nutrition Report',
              text: this._currentText,
              files: [file],
              url: this._service.appUrl
            });
          } else if (navigator.share) {
            await navigator.share({
              title: 'Diet Dost Nutrition Report',
              text: this._currentText,
              url: this._service.appUrl
            });
          }
        } catch (err) {
          if (err.name !== 'AbortError') console.warn('System share error:', err);
        }
      });
    }
  }

  _setAspectRatio(aspect) {
    if (this._aspectRatio === aspect) return;
    this._aspectRatio = aspect;
    const el = this.elements;
    if (el.btnAspectSquare) el.btnAspectSquare.classList.toggle('active', aspect === 'square');
    if (el.btnAspectStory) el.btnAspectStory.classList.toggle('active', aspect === 'story');
    if (el.previewFrame) el.previewFrame.classList.toggle('story-mode', aspect === 'story');
    this._rerender();
  }

  async _rerender() {
    const el = this.elements;
    if (!el.cardImg) return;

    if (el.loading) el.loading.style.display = 'flex';
    el.cardImg.style.opacity = '0.4';

    const opts = {
      aspectRatio: this._aspectRatio,
      includePhoto: el.togglePhoto ? el.togglePhoto.checked : true,
      includeAdvice: el.toggleAdvice ? el.toggleAdvice.checked : true,
      anonymize: el.toggleAnonymize ? el.toggleAnonymize.checked : false
    };

    try {
      let result;
      if (this._currentType === 'meal') {
        result = await this._service.renderer.renderMealCard(this._currentData, opts);
      } else {
        result = await this._service.renderer.renderDailyCard(this._currentData, opts);
      }

      this._currentBlob = result.blob;
      this._currentDataUrl = result.dataUrl;
      el.cardImg.src = result.dataUrl;
    } catch (err) {
      console.error('[ShareModalController] Failed to render card:', err);
    } finally {
      if (el.loading) el.loading.style.display = 'none';
      el.cardImg.style.opacity = '1';
    }
  }

  /**
   * Opens the Share Studio Modal with target data.
   * @param {Object} payload
   * @param {'meal'|'daily'} payload.type
   * @param {Object} payload.data
   * @param {string} [payload.text]
   */
  async open(payload) {
    if (!payload || !payload.data) return;
    this._initEvents();

    const el = this.elements;
    if (!el.modal) return;

    this._currentType = payload.type || 'meal';
    this._currentData = payload.data;
    this._currentText = payload.text || (this._currentType === 'meal'
      ? this._service.generateMealShareText(payload.data)
      : this._service.generateDailyShareText(payload.data));

    // Update titles and visibility of meal-specific toggles
    if (this._currentType === 'meal') {
      if (el.title) el.title.textContent = 'Share Meal Entry';
      if (el.subtitle) el.subtitle.textContent = `${this._currentData.dishName || 'Meal'} · High-DPI Nutrition Card`;
      if (el.togglePhotoRow) el.togglePhotoRow.style.display = 'flex';
      if (el.toggleAdviceRow) el.toggleAdviceRow.style.display = 'flex';
    } else {
      if (el.title) el.title.textContent = 'Share Daily Nutrition Pulse';
      if (el.subtitle) el.subtitle.textContent = 'Daily Calorie Balance, Macros & Consistency Streak';
      if (el.togglePhotoRow) el.togglePhotoRow.style.display = 'none';
      if (el.toggleAdviceRow) el.toggleAdviceRow.style.display = 'none';
    }

    // System share button visibility
    if (el.btnSystemShare) {
      el.btnSystemShare.style.display = (typeof navigator !== 'undefined' && typeof navigator.share === 'function')
        ? 'inline-flex'
        : 'none';
    }

    if (el.appLinkDisplay) {
      el.appLinkDisplay.href = this._service.appUrl;
      el.appLinkDisplay.textContent = this._service.appUrl;
    }

    // Push into modal stack
    modalStack.push(el.modal);
    el.modal.style.display = 'flex';
    el.modal.classList.add('active');

    // Trigger initial render
    await this._rerender();
  }

  /**
   * Closes the Share Studio Modal.
   */
  close() {
    const el = this.elements;
    if (!el.modal) return;
    el.modal.style.display = 'none';
    el.modal.classList.remove('active');
    modalStack.pop(el.modal);
  }
}

