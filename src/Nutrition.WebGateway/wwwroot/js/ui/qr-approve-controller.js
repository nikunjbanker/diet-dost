/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

class QrApproveController {
  constructor() {
    this.sessionId = null;
    this.challengeNonce = null;
    this.confirmationCode = null;
    this.confirmationGlyph = null;
    this.remainingSeconds = 90;
    this.timerInterval = null;

    this.initElements();
    this.parseQueryParams();
    this.bindEvents();
    this.checkSessionState();
  }

  initElements() {
    this.dispMatchCode = document.getElementById('disp-match-code');
    this.dispMatchGlyph = document.getElementById('disp-match-glyph');
    this.dispCountdown = document.getElementById('disp-countdown');
    this.hudTimerBar = document.getElementById('hud-timer-bar');

    this.sectionActiveUser = document.getElementById('section-active-user');
    this.dispActiveEmail = document.getElementById('disp-active-email');
    this.dispActiveTier = document.getElementById('disp-active-tier');
    this.btnApproveLogin = document.getElementById('btn-approve-login');
    this.btnDenyLogin = document.getElementById('btn-deny-login');

    this.sectionUnauthUser = document.getElementById('section-unauth-user');
    this.formMobileSignin = document.getElementById('form-mobile-signin');
    this.inpEmail = document.getElementById('inp-email');
    this.inpPassword = document.getElementById('inp-password');
    this.btnFastDemoBasic = document.getElementById('btn-fast-demo-basic');
    this.btnFastDemoPremium = document.getElementById('btn-fast-demo-premium');
    this.formMobileRecover = document.getElementById('form-mobile-recover');
    this.inpMobileMnemonic = document.getElementById('inp-mobile-mnemonic');
    this.btnRecoverAndApprove = document.getElementById('btn-recover-and-approve');

    this.sectionSuccess = document.getElementById('section-success');
    this.sectionDenied = document.getElementById('section-denied');
    this.dispErrorTitle = document.getElementById('disp-error-title');
    this.dispErrorMsg = document.getElementById('disp-error-msg');
  }

  parseQueryParams() {
    const params = new URLSearchParams(window.location.search);
    this.sessionId = params.get('sid') || params.get('session');
    this.challengeNonce = params.get('nonce') || params.get('challengeNonce');
    this.confirmationCode = params.get('code') || '--';
    this.confirmationGlyph = params.get('glyph') || 'Diet-Dost Device';

    if (this.dispMatchCode) {
      this.dispMatchCode.textContent = `[ ${this.confirmationCode} ]`;
    }
    if (this.dispMatchGlyph) {
      this.dispMatchGlyph.textContent = decodeURIComponent(this.confirmationGlyph);
    }

    if (!this.sessionId || !this.challengeNonce) {
      this.showError('Invalid QR Code Link', 'The scanned QR code is missing required session identifiers.');
      return;
    }

    this.startCountdown();
  }

  startCountdown() {
    clearInterval(this.timerInterval);
    this.timerInterval = setInterval(() => {
      this.remainingSeconds--;
      if (this.dispCountdown) {
        this.dispCountdown.textContent = Math.max(0, this.remainingSeconds).toString();
      }
      if (this.remainingSeconds <= 0) {
        clearInterval(this.timerInterval);
        this.showError('QR Code Expired', 'This login authorization session has expired. Please refresh the QR code on your computer.');
      }
    }, 1000);
  }

  bindEvents() {
    this.btnApproveLogin?.addEventListener('click', () => this.approveWithExistingToken());
    this.btnDenyLogin?.addEventListener('click', () => this.denySession());
    this.formMobileSignin?.addEventListener('submit', (e) => this.handleSigninAndApprove(e));

    this.btnFastDemoBasic?.addEventListener('click', () => this.fastDemoApprove('basic@dietdost.app'));
    this.btnFastDemoPremium?.addEventListener('click', () => this.fastDemoApprove('premium@dietdost.app'));
    this.formMobileRecover?.addEventListener('submit', (e) => this.handleRecoverAndApprove(e));
  }

  async checkSessionState() {
    if (!this.sessionId || !this.challengeNonce) return;

    // Check if token exists in localStorage
    const token = localStorage.getItem('auth_token') || sessionStorage.getItem('auth_token');
    if (!token) {
      this.showUnauthState();
      return;
    }

    try {
      const res = await fetch('/api/auth/me', {
        headers: { 'Authorization': `Bearer ${token}` }
      });

      if (res.ok) {
        const user = await res.json();
        this.showActiveUserState(user, token);
      } else {
        this.showUnauthState();
      }
    } catch {
      this.showUnauthState();
    }
  }

  showActiveUserState(user, token) {
    this.activeToken = token;
    if (this.dispActiveEmail) {
      this.dispActiveEmail.textContent = user.email || user.displayName || 'Authorized User';
    }
    if (this.dispActiveTier) {
      this.dispActiveTier.textContent = `⭐ ${user.tier || 'Member'}`;
    }
    if (this.sectionActiveUser) this.sectionActiveUser.style.display = 'flex';
    if (this.sectionUnauthUser) this.sectionUnauthUser.style.display = 'none';
  }

  showUnauthState() {
    if (this.sectionActiveUser) this.sectionActiveUser.style.display = 'none';
    if (this.sectionUnauthUser) this.sectionUnauthUser.style.display = 'flex';
  }

  async approveWithExistingToken() {
    if (!this.activeToken) {
      this.showUnauthState();
      return;
    }

    this.setLoading(this.btnApproveLogin, true, 'Approving Desktop...');
    try {
      const res = await fetch('/api/auth/qr/approve', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${this.activeToken}`
        },
        body: JSON.stringify({
          sessionId: this.sessionId,
          challengeNonce: this.challengeNonce,
          mobilePublicKey: 'web-mobile-scan'
        })
      });

      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.detail || err.title || 'Failed to approve desktop login.');
      }

      this.showSuccess();
    } catch (err) {
      alert(err.message || 'Approval failed. Please check connection.');
      this.setLoading(this.btnApproveLogin, false, '✅ Approve Desktop Login');
    }
  }

  async handleSigninAndApprove(e) {
    e.preventDefault();
    const email = this.inpEmail.value.trim();
    const password = this.inpPassword.value;
    const btn = document.getElementById('btn-signin-and-approve');

    this.setLoading(btn, true, 'Authenticating...');
    try {
      const loginRes = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ identifier: email, emailOrMobile: email, password: password })
      });

      if (!loginRes.ok) {
        const err = await loginRes.json().catch(() => ({}));
        throw new Error(err.detail || err.title || 'Invalid username or password.');
      }

      const loginData = await loginRes.json();
      localStorage.setItem('auth_token', loginData.token);
      this.activeToken = loginData.token;

      // Approve desktop session
      this.setLoading(btn, true, 'Authorizing Desktop...');
      const approveRes = await fetch('/api/auth/qr/approve', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${this.activeToken}`
        },
        body: JSON.stringify({
          sessionId: this.sessionId,
          challengeNonce: this.challengeNonce,
          mobilePublicKey: 'web-mobile-scan'
        })
      });

      if (!approveRes.ok) {
        const err = await approveRes.json().catch(() => ({}));
        throw new Error(err.detail || err.title || 'Failed to approve session.');
      }

      this.showSuccess();
    } catch (err) {
      alert(err.message);
      this.setLoading(btn, false, 'Sign In & Authorize Desktop ➔');
    }
  }

  async fastDemoApprove(demoEmail) {
    const btn = demoEmail.includes('premium') ? this.btnFastDemoPremium : this.btnFastDemoBasic;
    this.setLoading(btn, true, 'Approving Demo Session...');

    try {
      const res = await fetch('/api/auth/qr/simulate-approval', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          sessionId: this.sessionId,
          challengeNonce: this.challengeNonce,
          demoEmail: demoEmail
        })
      });

      if (!res.ok) {
        // Fallback: try authenticating as demo user directly
        const loginRes = await fetch('/api/auth/login', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ identifier: demoEmail, emailOrMobile: demoEmail, password: 'DietDost@Demo2026!' })
        });

        if (loginRes.ok) {
          const loginData = await loginRes.json();
          this.activeToken = loginData.token;
          localStorage.setItem('auth_token', loginData.token);

          const approveRes = await fetch('/api/auth/qr/approve', {
            method: 'POST',
            headers: {
              'Content-Type': 'application/json',
              'Authorization': `Bearer ${this.activeToken}`
            },
            body: JSON.stringify({
              sessionId: this.sessionId,
              challengeNonce: this.challengeNonce,
              mobilePublicKey: 'web-mobile-scan'
            })
          });

          if (approveRes.ok) {
            this.showSuccess();
            return;
          }
        }

        const err = await res.json().catch(() => ({}));
        throw new Error(err.detail || err.title || 'Simulation failed.');
      }

      this.showSuccess();
    } catch (err) {
      alert(err.message);
      this.setLoading(btn, false, `⚡ Demo Approval (${demoEmail})`);
    }
  }

  async handleRecoverAndApprove(e) {
    e.preventDefault();
    const mnemonic = this.inpMobileMnemonic?.value?.trim();
    if (!mnemonic) {
      alert('Please enter your 12-word recovery mnemonic phrase.');
      return;
    }

    this.setLoading(this.btnRecoverAndApprove, true, 'Recovering & Authorizing...');
    try {
      const recRes = await fetch('/api/auth/recover', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ recoveryKeyOrMnemonic: mnemonic })
      });

      if (!recRes.ok) {
        const err = await recRes.json().catch(() => ({}));
        throw new Error(err.detail || err.title || 'Recovery credentials invalid.');
      }

      const recData = await recRes.json();
      this.activeToken = recData.token;
      localStorage.setItem('auth_token', recData.token);

      // Approve desktop session
      const approveRes = await fetch('/api/auth/qr/approve', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${this.activeToken}`
        },
        body: JSON.stringify({
          sessionId: this.sessionId,
          challengeNonce: this.challengeNonce,
          mobilePublicKey: 'web-mobile-scan'
        })
      });

      if (!approveRes.ok) {
        const err = await approveRes.json().catch(() => ({}));
        throw new Error(err.detail || err.title || 'Failed to approve session.');
      }

      this.showSuccess();
    } catch (err) {
      alert(err.message);
      this.setLoading(this.btnRecoverAndApprove, false, 'Authorize via Recovery Phrase ➔');
    }
  }

  async denySession() {
    if (!confirm('Are you sure you want to deny this desktop login request?')) return;
    try {
      await fetch('/api/auth/qr/reject', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ sessionId: this.sessionId, challengeNonce: this.challengeNonce })
      });
    } catch {
      // Best-effort
    }
    this.showError('Request Denied', 'You have denied this desktop sign-in request.');
  }

  showSuccess() {
    clearInterval(this.timerInterval);
    if (this.sectionActiveUser) this.sectionActiveUser.style.display = 'none';
    if (this.sectionUnauthUser) this.sectionUnauthUser.style.display = 'none';
    if (this.hudTimerBar) this.hudTimerBar.style.display = 'none';
    if (this.sectionSuccess) this.sectionSuccess.style.display = 'flex';
  }

  showError(title, msg) {
    clearInterval(this.timerInterval);
    if (this.sectionActiveUser) this.sectionActiveUser.style.display = 'none';
    if (this.sectionUnauthUser) this.sectionUnauthUser.style.display = 'none';
    if (this.hudTimerBar) this.hudTimerBar.style.display = 'none';
    if (this.dispErrorTitle) this.dispErrorTitle.textContent = title;
    if (this.dispErrorMsg) this.dispErrorMsg.textContent = msg;
    if (this.sectionDenied) this.sectionDenied.style.display = 'flex';
  }

  setLoading(button, isLoading, text) {
    if (!button) return;
    button.disabled = isLoading;
    button.textContent = text;
  }
}

document.addEventListener('DOMContentLoaded', () => {
  new QrApproveController();
});
