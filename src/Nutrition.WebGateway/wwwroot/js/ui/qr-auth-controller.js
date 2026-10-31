/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
import { encodeQrSvg } from './qr-code-encoder.js?v=1.9.3';


export class QrAuthController {
  constructor({ authService, toastService, onAuthSuccess }) {
    this.auth = authService;
    this.toast = toastService;
    this.onAuthSuccess = onAuthSuccess;

    this.currentSessionId = null;
    this.currentChallengeNonce = null;
    this.currentBrowserSecret = null;
    this.pollInterval = null;
    this.countdownTimer = null;
    this.remainingSeconds = 90;
    this.pendingRecoveryKit = null;

    this.initElements();
    this.bindEvents();
  }

  initElements() {
    this.qrForm = document.getElementById('form-qr');
    this.qrError = document.getElementById('qr-error');
    this.canvasContainer = document.getElementById('qr-canvas-container');
    this.matchHud = document.getElementById('qr-match-hud');
    this.matchCode = document.getElementById('qr-match-code');
    this.matchGlyph = document.getElementById('qr-match-glyph');
    this.countdownSeconds = document.getElementById('qr-countdown-seconds');
    this.btnRefresh = document.getElementById('btn-refresh-qr');
    this.btnSimulateMobile = document.getElementById('btn-simulate-mobile-scan');

    // Anonymous Registration elements
    this.btnModeStandard = document.getElementById('btn-reg-mode-standard');
    this.btnModeAnon = document.getElementById('btn-reg-mode-anon');
    this.standardFields = document.getElementById('standard-reg-fields');
    this.anonFields = document.getElementById('anon-reg-fields');
    this.anonDisplayName = document.getElementById('reg-anon-displayname');
    this.btnSubmitRegister = document.getElementById('btn-submit-register');

    // Recovery Kit Modal elements
    this.recoveryModal = document.getElementById('recovery-kit-modal');
    this.mnemonicGrid = document.getElementById('recovery-mnemonic-grid');
    this.btnDownloadJson = document.getElementById('btn-download-recovery-json');
    this.btnCopyMnemonic = document.getElementById('btn-copy-mnemonic-phrase');
    this.chkConfirmSaved = document.getElementById('chk-confirm-recovery-saved');
    this.btnCompleteRecovery = document.getElementById('btn-complete-recovery-kit');

    // Disaster Recovery Form elements
    this.recoverForm = document.getElementById('form-recover');
    this.recoverError = document.getElementById('recover-error');
    this.recoverMnemonicInput = document.getElementById('recover-mnemonic-input');
    this.recoveryDropzone = document.getElementById('recovery-dropzone');
    this.recoveryFileInput = document.getElementById('recovery-file-input');

    this.isAnonymousMode = false;
  }

  bindEvents() {
    this.btnRefresh?.addEventListener('click', () => this.startNewQrSession());
    this.btnSimulateMobile?.addEventListener('click', () => this.simulateMobileApproval());

    // Registration mode toggling
    this.btnModeStandard?.addEventListener('click', () => this.setRegistrationMode(false));
    this.btnModeAnon?.addEventListener('click', () => this.setRegistrationMode(true));

    // Recovery Kit modal actions
    this.btnDownloadJson?.addEventListener('click', () => this.downloadRecoveryJson());
    this.btnCopyMnemonic?.addEventListener('click', () => this.copyMnemonicToClipboard());
    this.chkConfirmSaved?.addEventListener('change', () => {
      if (this.btnCompleteRecovery) {
        this.btnCompleteRecovery.disabled = !this.chkConfirmSaved.checked;
      }
    });
    this.btnCompleteRecovery?.addEventListener('click', () => {
      this.closeRecoveryModal();
      if (this.pendingRecoveryKit?.user) {
        this.onAuthSuccess(this.pendingRecoveryKit.user);
      }
    });

    // Disaster Recovery Form actions
    this.recoverForm?.addEventListener('submit', (e) => this.handleRecoverSubmit(e));
    this.recoveryDropzone?.addEventListener('click', () => this.recoveryFileInput?.click());
    this.recoveryFileInput?.addEventListener('change', (e) => this.handleFileSelect(e));

    // Drag-and-drop for JSON recovery file
    this.recoveryDropzone?.addEventListener('dragover', (e) => {
      e.preventDefault();
      this.recoveryDropzone.classList.add('dragover');
    });
    this.recoveryDropzone?.addEventListener('dragleave', () => {
      this.recoveryDropzone.classList.remove('dragover');
    });
    this.recoveryDropzone?.addEventListener('drop', (e) => {
      e.preventDefault();
      this.recoveryDropzone.classList.remove('dragover');
      if (e.dataTransfer?.files?.length > 0) {
        this.processRecoveryFile(e.dataTransfer.files[0]);
      }
    });
  }

  setRegistrationMode(isAnonymous) {
    this.isAnonymousMode = isAnonymous;
    if (isAnonymous) {
      this.btnModeAnon?.classList.replace('btn-outline', 'btn-primary');
      this.btnModeStandard?.classList.replace('btn-primary', 'btn-outline');
      if (this.standardFields) this.standardFields.style.display = 'none';
      if (this.anonFields) this.anonFields.style.display = 'block';

      // Disable 'required' on standard inputs to prevent form submission block
      document.querySelectorAll('#standard-reg-fields input').forEach(inp => inp.required = false);

      if (this.btnSubmitRegister) {
        this.btnSubmitRegister.querySelector('.btn-text').textContent = 'Create Zero-PII Anonymous Account ➔';
      }
    } else {
      this.btnModeStandard?.classList.replace('btn-outline', 'btn-primary');
      this.btnModeAnon?.classList.replace('btn-primary', 'btn-outline');
      if (this.standardFields) this.standardFields.style.display = 'block';
      if (this.anonFields) this.anonFields.style.display = 'none';

      document.querySelectorAll('#standard-reg-fields input').forEach(inp => {
        if (inp.id !== 'reg-confirm-password') inp.required = true;
      });

      if (this.btnSubmitRegister) {
        this.btnSubmitRegister.querySelector('.btn-text').textContent = 'Create Account';
      }
    }
  }

  async startNewQrSession() {
    this.stopPolling();
    this.clearError();

    try {
      // 1. Generate 256-bit cryptographically secure in-memory browser secret
      const randomBytes = new Uint8Array(32);
      window.crypto.getRandomValues(randomBytes);
      this.currentBrowserSecret = Array.from(randomBytes).map(b => b.toString(16).padStart(2, '0')).join('');
      sessionStorage.setItem('dd_qr_browser_secret', this.currentBrowserSecret);

      // 2. Compute SHA-256 hash of the browser secret
      const encoded = new window.TextEncoder().encode(this.currentBrowserSecret);
      const hashBuffer = await window.crypto.subtle.digest('SHA-256', encoded);
      const browserSecretHash = Array.from(new Uint8Array(hashBuffer)).map(b => b.toString(16).padStart(2, '0')).join('');

      // 3. Initiate ephemeral session on server
      const res = await this.auth.initiateQrSession(browserSecretHash);
      if (!res.sessionId) {
        throw new Error(res.error || 'Failed to initiate QR session.');
      }

      this.currentSessionId = res.sessionId;
      this.currentChallengeNonce = res.challengeNonce;
      this.remainingSeconds = res.ttlSeconds || 90;

      // 4. Render Authoritative Server-Generated ISO/IEC 18004 Vector SVG
      if (res.qrSvg && this.canvasContainer) {
        this.canvasContainer.innerHTML = res.qrSvg;
      } else {
        const webApprovalUrl = `${window.location.origin}/qr-approve.html?sid=${encodeURIComponent(res.sessionId)}&nonce=${encodeURIComponent(res.challengeNonce)}&code=${encodeURIComponent(res.confirmationCode)}&glyph=${encodeURIComponent(res.confirmationGlyph)}`;
        const svg = encodeQrSvg(webApprovalUrl, { size: 190, eccLevel: 'M', border: 4 });
        if (this.canvasContainer) {
          this.canvasContainer.innerHTML = svg;
        }
      }

      // 5. Update Visual Match Code HUD
      if (this.matchCode) this.matchCode.textContent = `[ ${res.confirmationCode} ]`;
      if (this.matchGlyph) this.matchGlyph.textContent = res.confirmationGlyph;
      if (this.matchHud) this.matchHud.style.display = 'flex';

      // 6. Start countdown and polling
      this.startCountdown();
      this.startPolling();
    } catch (err) {
      this.showError(err.message || 'Unable to establish secure QR login session.');
    }
  }

  startCountdown() {
    clearInterval(this.countdownTimer);
    if (this.countdownSeconds) this.countdownSeconds.textContent = this.remainingSeconds.toString();

    this.countdownTimer = setInterval(() => {
      this.remainingSeconds--;
      if (this.countdownSeconds) {
        this.countdownSeconds.textContent = Math.max(0, this.remainingSeconds).toString();
      }

      if (this.remainingSeconds <= 0) {
        clearInterval(this.countdownTimer);
        this.stopPolling();
        if (this.canvasContainer) {
          this.canvasContainer.innerHTML = `
            <div style="width: 180px; height: 180px; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 8px; color: #888; font-size: 0.8rem; background: rgba(0,0,0,0.05); border-radius: 8px;">
              <span>⌛ Code Expired</span>
              <button type="button" class="btn btn-xs btn-primary" id="btn-qr-reload-expired">Click to Refresh</button>
            </div>`;
          document.getElementById('btn-qr-reload-expired')?.addEventListener('click', () => this.startNewQrSession());
        }
      }
    }, 1000);
  }

  startPolling() {
    this.pollInterval = setInterval(async () => {
      if (!this.currentSessionId) return;

      try {
        const status = await this.auth.getQrStatus(this.currentSessionId);
        if (!status) return;

        if (status.status === 'Scanned') {
          if (this.matchGlyph) {
            this.matchGlyph.textContent = `📱 Scanned on Mobile! Verify [ ${this.matchCode?.textContent || ''} ]`;
          }
        } else if (status.status === 'Approved' && status.exchangeTicket) {
          // Mobile approved! Exchange ticket with in-memory secret
          this.stopPolling();
          await this.completeTicketExchange(status.exchangeTicket);
        } else if (status.status === 'Rejected' || status.isExpired) {
          this.stopPolling();
          this.showError('QR session rejected or expired. Please refresh code.');
        }
      } catch (e) {
        // Silent catch on network blip, continue polling
      }
    }, 1500);
  }

  async completeTicketExchange(exchangeTicket) {
    try {
      const secret = this.currentBrowserSecret || sessionStorage.getItem('dd_qr_browser_secret');
      if (!secret) {
        throw new Error('Browser secret token missing from memory or session storage.');
      }
      const exchangeResult = await this.auth.exchangeQrTicket(
        this.currentSessionId,
        exchangeTicket,
        secret
      );

      this.stopPolling();
      this.toast?.show('⚡ Cross-device authentication successful! Welcome to Diet-Dost.', 'success');
      this.onAuthSuccess(exchangeResult.user);
    } catch (err) {
      this.showError(err.message || 'Ticket exchange verification failed.');
    }
  }

  async simulateMobileApproval() {
    if (!this.currentSessionId || !this.currentChallengeNonce) {
      this.toast?.show('Please initiate a QR session first.', 'warning');
      return;
    }

    try {
      this.toast?.show('Simulating mobile scan and biometric approval...', 'info');

      // 1. First attempt dedicated simulation endpoint on gateway
      const simRes = await fetch('/api/auth/qr/simulate-approval', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          sessionId: this.currentSessionId,
          challengeNonce: this.currentChallengeNonce,
          demoEmail: 'basic@dietdost.app'
        })
      });

      if (simRes.ok) {
        const simData = await simRes.json();
        this.toast?.show('Mobile approval simulated! Exchanging ticket...', 'success');
        if (simData.exchangeTicket) {
          await this.completeTicketExchange(simData.exchangeTicket);
        }
        return;
      }

      // 2. Fallback: Authenticate as demo user in background to simulate an active mobile session
      let token = this.auth.getToken();
      if (!token) {
        const demoLoginRes = await fetch('/api/auth/login', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            identifier: 'basic@dietdost.app',
            password: 'DietDost@Demo2026!'
          })
        });

        if (demoLoginRes.ok) {
          const demoData = await demoLoginRes.json();
          token = demoData.token;
        }
      }

      // 3. Call standard mobile approval endpoint
      const response = await fetch('/api/auth/qr/approve', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token || ''}`
        },
        body: JSON.stringify({
          sessionId: this.currentSessionId,
          challengeNonce: this.currentChallengeNonce,
          mobilePublicKey: 'simulated-mobile-p256-key'
        })
      });

      if (!response.ok) {
        const errJson = await response.json().catch(() => ({}));
        throw new Error(errJson.detail || errJson.title || 'Mobile approval failed.');
      }

      this.toast?.show('Mobile approval simulated! Waiting for ticket exchange...', 'success');
    } catch (err) {
      this.showError(err.message || 'Simulation requires an active demo session.');
    }
  }

  stopPolling() {
    if (this.pollInterval) {
      clearInterval(this.pollInterval);
      this.pollInterval = null;
    }
    if (this.countdownTimer) {
      clearInterval(this.countdownTimer);
      this.countdownTimer = null;
    }
  }

  showRecoveryModal(recoveryData) {
    this.pendingRecoveryKit = recoveryData;
    if (!this.recoveryModal || !this.mnemonicGrid) return;

    const words = (recoveryData.mnemonicPhrase || '').split(' ');
    this.mnemonicGrid.innerHTML = words.map((w, i) => `
      <div class="mnemonic-pill">
        <span class="mnemonic-num">#${(i + 1).toString().padStart(2, '0')}</span>
        <span class="mnemonic-word">${w}</span>
      </div>
    `).join('');

    if (this.chkConfirmSaved) this.chkConfirmSaved.checked = false;
    if (this.btnCompleteRecovery) this.btnCompleteRecovery.disabled = true;

    this.recoveryModal.style.display = 'flex';
  }

  closeRecoveryModal() {
    if (this.recoveryModal) {
      this.recoveryModal.style.display = 'none';
    }
  }

  downloadRecoveryJson() {
    if (!this.pendingRecoveryKit?.jsonBackupPayload) {
      this.toast?.show('No recovery data available to download.', 'error');
      return;
    }

    const blob = new Blob([this.pendingRecoveryKit.jsonBackupPayload], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `diet-dost-disaster-recovery-${this.pendingRecoveryKit.recoveryLookupId || 'kit'}.json`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);

    this.toast?.show('Disaster Recovery Kit JSON downloaded safely.', 'success');
    if (this.chkConfirmSaved) {
      this.chkConfirmSaved.checked = true;
      if (this.btnCompleteRecovery) this.btnCompleteRecovery.disabled = false;
    }
  }

  copyMnemonicToClipboard() {
    if (!this.pendingRecoveryKit?.mnemonicPhrase) return;
    navigator.clipboard.writeText(this.pendingRecoveryKit.mnemonicPhrase).then(() => {
      this.toast?.show('12-word mnemonic phrase copied to clipboard!', 'success');
      if (this.chkConfirmSaved) {
        this.chkConfirmSaved.checked = true;
        if (this.btnCompleteRecovery) this.btnCompleteRecovery.disabled = false;
      }
    }).catch(() => {
      this.toast?.show('Failed to copy to clipboard. Please copy manually.', 'error');
    });
  }

  handleFileSelect(e) {
    if (e.target.files && e.target.files.length > 0) {
      this.processRecoveryFile(e.target.files[0]);
    }
  }

  processRecoveryFile(file) {
    const reader = new FileReader();
    reader.onload = (event) => {
      try {
        const json = JSON.parse(event.target.result);
        const mnemonic = json.mnemonicPhrase || json.mnemonic_phrase || json.mnemonic || json.phrase;
        const key = json.recoveryKey || json.master_recovery_key_hex || json.recovery_key || json.masterKey;

        if (mnemonic && this.recoverMnemonicInput) {
          this.recoverMnemonicInput.type = 'password';
          this.recoverMnemonicInput.value = mnemonic;
          this.toast?.show('Loaded 12-word recovery phrase from JSON file!', 'success');
          if (typeof this.recoverForm?.requestSubmit === 'function') {
            this.recoverForm.requestSubmit();
          } else {
            this.handleRecoverSubmit(new Event('submit'));
          }
        } else if (key && this.recoverMnemonicInput) {
          this.recoverMnemonicInput.type = 'password';
          this.recoverMnemonicInput.value = key;
          this.toast?.show('Loaded recovery key from JSON file!', 'success');
          if (typeof this.recoverForm?.requestSubmit === 'function') {
            this.recoverForm.requestSubmit();
          } else {
            this.handleRecoverSubmit(new Event('submit'));
          }
        } else {
          throw new Error('Unrecognized recovery file structure.');
        }
      } catch {
        this.showRecoverError('Invalid Disaster Recovery JSON file. Please ensure it is a valid Diet-Dost recovery kit.');
      }
    };
    reader.readAsText(file);
  }

  async handleRecoverSubmit(e) {
    e.preventDefault();
    this.clearRecoverError();

    const input = this.recoverMnemonicInput?.value?.trim();
    if (!input) {
      this.showRecoverError('Please enter your 12-word mnemonic phrase or recovery key.');
      return;
    }

    try {
      const submitBtn = document.getElementById('btn-submit-recover');
      const textSpan = submitBtn?.querySelector('.btn-text');
      const spinnerSpan = submitBtn?.querySelector('.btn-spinner');

      if (textSpan) textSpan.style.display = 'none';
      if (spinnerSpan) spinnerSpan.style.display = 'inline';
      if (submitBtn) submitBtn.disabled = true;

      const res = await this.auth.recoverAccount(input);

      this.toast?.show(`🔑 Welcome back, ${res.user?.displayName || 'User'}! Signed in successfully.`, 'success');

      // Dismiss auth gate and enter dashboard immediately
      if (res.user) {
        this.onAuthSuccess(res.user);
      }
    } catch (err) {
      this.showRecoverError(err.message || 'Recovery failed. Invalid mnemonic checksum or phrase.');
    } finally {
      const submitBtn = document.getElementById('btn-submit-recover');
      const textSpan = submitBtn?.querySelector('.btn-text');
      const spinnerSpan = submitBtn?.querySelector('.btn-spinner');
      if (textSpan) textSpan.style.display = 'inline';
      if (spinnerSpan) spinnerSpan.style.display = 'none';
      if (submitBtn) submitBtn.disabled = false;
    }
  }

  showError(msg) {
    if (this.qrError) {
      this.qrError.textContent = msg;
      this.qrError.style.display = 'block';
    }
  }

  clearError() {
    if (this.qrError) {
      this.qrError.textContent = '';
      this.qrError.style.display = 'none';
    }
  }

  showRecoverError(msg) {
    if (this.recoverError) {
      this.recoverError.textContent = msg;
      this.recoverError.style.display = 'block';
    }
  }

  clearRecoverError() {
    if (this.recoverError) {
      this.recoverError.textContent = '';
      this.recoverError.style.display = 'none';
    }
  }
}
