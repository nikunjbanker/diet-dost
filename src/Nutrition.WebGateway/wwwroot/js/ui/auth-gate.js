/**
 * Obsidian Authentication & Verification Gate UI Controller
 * Blocks the entire dashboard when unauthenticated or unverified,
 * enforcing India DPDPA 2023 dual-consent and 6-digit email OTP activation.
 *
 * Features:
 *  – Password strength meter (OWASP A07 compliant 10-30 char policy)
 *  – Confirm-password live match check on Register & Reset flows
 *  – Forgot-Password OTP flow (2 steps: email → OTP + new password)
 */
export class AuthGateController {
  constructor({ authService, toastService, eventBus }) {
    this.authService = authService;
    this.toastService = toastService;
    this.eventBus = eventBus;

    this.pendingEmail = null;
    this.pendingMobile = null;
    this.pendingResetEmail = null;
    this.resendTimer = null;
    this.resendSeconds = 0;
    this.resetResendTimer = null;
    this.resetResendSeconds = 0;

    this.initElements();
    this.bindEvents();
  }

  initElements() {
    this.modal = document.getElementById('auth-gate-modal');
    this.tabSignIn = document.getElementById('btn-tab-signin');
    this.tabRegister = document.getElementById('btn-tab-register');
    this.tabVerify = document.getElementById('btn-tab-verify');
    this.tabReset = document.getElementById('btn-tab-reset');

    this.formSignIn = document.getElementById('form-signin');
    this.formRegister = document.getElementById('form-register');
    this.formVerify = document.getElementById('form-verify-otp');
    this.formReset = document.getElementById('form-reset-password');

    this.signinError = document.getElementById('signin-error');
    this.registerError = document.getElementById('register-error');
    this.verifyError = document.getElementById('verify-error');
    this.resetError = document.getElementById('reset-error');
    this.resetSuccess = document.getElementById('reset-success');

    this.otpTargetDisplay = document.getElementById('otp-target-display');
    this.otpCodeInput = document.getElementById('otp-code-input');
    this.devOtpHolder = document.getElementById('dev-otp-helper');
    this.devOtpCode = document.getElementById('dev-otp-code');
    this.btnFillDevOtp = document.getElementById('btn-fill-dev-otp');
    this.btnResendOtp = document.getElementById('btn-resend-otp');
    this.resendCountdown = document.getElementById('resend-countdown');

    // Legal sub-modals
    this.legalTermsModal = document.getElementById('legal-terms-modal');
    this.legalHealthModal = document.getElementById('legal-health-modal');
  }

  bindEvents() {
    this.tabSignIn?.addEventListener('click', () => this.switchTab('signin'));
    this.tabRegister?.addEventListener('click', () => this.switchTab('register'));
    this.tabVerify?.addEventListener('click', () => this.switchTab('verify'));
    this.tabReset?.addEventListener('click', () => this.switchTab('reset'));

    document.getElementById('link-goto-register')?.addEventListener('click', () => this.switchTab('register'));
    document.getElementById('link-goto-signin')?.addEventListener('click', () => this.switchTab('signin'));
    document.getElementById('link-goto-forgot-password')?.addEventListener('click', () => this.switchTab('reset'));
    document.getElementById('link-back-to-signin-from-reset')?.addEventListener('click', () => this.switchTab('signin'));

    // Legal modal openers (stop propagation to prevent label toggling the checkbox)
    document.getElementById('link-open-terms')?.addEventListener('click', (e) => {
      e.preventDefault(); e.stopPropagation();
      this.openLegalModal('terms');
    });
    document.getElementById('link-open-health-consent')?.addEventListener('click', (e) => {
      e.preventDefault(); e.stopPropagation();
      this.openLegalModal('health');
    });
    document.getElementById('btn-close-terms-modal')?.addEventListener('click', () => this.closeLegalModal('terms'));
    document.getElementById('btn-close-health-modal')?.addEventListener('click', () => this.closeLegalModal('health'));

    this.legalTermsModal?.addEventListener('click', (e) => {
      if (e.target === this.legalTermsModal) this.closeLegalModal('terms');
    });
    this.legalHealthModal?.addEventListener('click', (e) => {
      if (e.target === this.legalHealthModal) this.closeLegalModal('health');
    });

    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') {
        if (this.legalTermsModal?.style.display === 'flex') this.closeLegalModal('terms');
        if (this.legalHealthModal?.style.display === 'flex') this.closeLegalModal('health');
      }
    });

    document.getElementById('btn-accept-terms-modal')?.addEventListener('click', () => {
      const cb = document.getElementById('reg-consent-terms');
      if (cb) cb.checked = true;
      this.closeLegalModal('terms');
    });
    document.getElementById('btn-accept-health-modal')?.addEventListener('click', () => {
      const cb = document.getElementById('reg-consent-health');
      if (cb) cb.checked = true;
      this.closeLegalModal('health');
    });

    // Form submissions
    this.formSignIn?.addEventListener('submit', (e) => this.handleSignIn(e));
    this.formRegister?.addEventListener('submit', (e) => this.handleRegister(e));
    this.formVerify?.addEventListener('submit', (e) => this.handleVerifyOtp(e));

    // Dev OTP auto-fill (verification)
    this.btnFillDevOtp?.addEventListener('click', () => {
      if (this.devOtpCode && this.otpCodeInput)
        this.otpCodeInput.value = this.devOtpCode.textContent.trim();
    });

    // Resend OTP (verification)
    this.btnResendOtp?.addEventListener('click', () => this.handleResendOtp());

    // ── Password strength meters ────────────────────────────────────────────
    document.getElementById('reg-password')?.addEventListener('input', (e) =>
      this.updateStrengthMeter(e.target.value, 'reg-pwd-strength-fill', 'reg-pwd-strength-label'));

    document.getElementById('reg-confirm-password')?.addEventListener('input', () =>
      this.checkPasswordMatch(
        document.getElementById('reg-password')?.value,
        document.getElementById('reg-confirm-password')?.value,
        'reg-confirm-match-label'));

    document.getElementById('reset-new-password')?.addEventListener('input', (e) =>
      this.updateStrengthMeter(e.target.value, 'reset-pwd-strength-fill', 'reset-pwd-strength-label'));

    document.getElementById('reset-confirm-password')?.addEventListener('input', () =>
      this.checkPasswordMatch(
        document.getElementById('reset-new-password')?.value,
        document.getElementById('reset-confirm-password')?.value,
        'reset-confirm-match-label'));

    // ── Reset-password flow ─────────────────────────────────────────────────
    document.getElementById('btn-send-reset-otp')?.addEventListener('click', () => this.handleSendResetOtp());
    document.getElementById('btn-submit-reset')?.addEventListener('click', () => this.handleSubmitReset());
    document.getElementById('btn-resend-reset-otp')?.addEventListener('click', () => this.handleResendResetOtp());
    document.getElementById('btn-fill-dev-reset-otp')?.addEventListener('click', () => {
      const code = document.getElementById('dev-reset-otp-code');
      const inp = document.getElementById('reset-otp-code');
      if (code && inp) inp.value = code.textContent.trim();
    });
  }

  // ── Tab routing ────────────────────────────────────────────────────────────
  show(defaultTab = 'signin') {
    if (this.modal) {
      this.modal.style.display = 'flex';
      document.body.style.overflow = 'hidden';
      this.switchTab(defaultTab);
    }
  }

  hide() {
    if (this.modal) {
      this.modal.style.display = 'none';
      document.body.style.overflow = '';
    }
  }

  switchTab(tab) {
    this.clearErrors();

    this.tabSignIn?.classList.toggle('active', tab === 'signin');
    this.tabRegister?.classList.toggle('active', tab === 'register');
    this.tabVerify?.classList.toggle('active', tab === 'verify');
    this.tabReset?.classList.toggle('active', tab === 'reset');

    if (this.formSignIn) this.formSignIn.style.display = tab === 'signin' ? 'block' : 'none';
    if (this.formRegister) this.formRegister.style.display = tab === 'register' ? 'block' : 'none';
    if (this.formVerify) this.formVerify.style.display = tab === 'verify' ? 'block' : 'none';
    if (this.formReset) this.formReset.style.display = tab === 'reset' ? 'block' : 'none';

    if (tab === 'verify' && this.tabVerify) this.tabVerify.style.display = 'inline-block';
    if (tab === 'reset' && this.tabReset) this.tabReset.style.display = 'inline-block';
  }

  clearErrors() {
    for (const id of ['signin-error', 'register-error', 'verify-error', 'reset-error'])  {
      const el = document.getElementById(id);
      if (el) el.style.display = 'none';
    }
    if (this.resetSuccess) this.resetSuccess.style.display = 'none';
  }

  showError(container, message) {
    if (container) { container.textContent = message; container.style.display = 'block'; }
  }

  showSuccess(container, message) {
    if (container) { container.textContent = message; container.style.display = 'block'; }
  }

  // ── Password strength meter ────────────────────────────────────────────────
  /**
   * Evaluates password strength against the Diet Dost policy
   * (mirrors PasswordPolicy.cs server-side rules).
   * Policy: min 10, max 30, upper, lower, digit, safe special char.
   */
  evaluatePasswordStrength(password) {
    if (!password || password.length < 1) return { score: 0, label: '', color: '' };

    let score = 0;
    // Banned chars (mirrors server regex [<>;'"\\\/`~\x00-\x1F\x7F])
    const hasBanned = /[<>;'"\\\/`~\x00-\x1F\x7F]/.test(password);
    if (hasBanned) return { score: 0, label: '❌ Contains disallowed characters (< > ; \' " \\ / ` ~)', color: '#ef4444' };

    if (password.length >= 10) score++;
    if (password.length >= 14) score++;
    if (/[A-Z]/.test(password)) score++;
    if (/[a-z]/.test(password)) score++;
    if (/[0-9]/.test(password)) score++;
    if (/[!@#$%^&*()\-_=+\[\]{}|:,.?]/.test(password)) score++;
    if (password.length > 20) score++;

    if (score <= 2) return { score: 2, label: '🔴 Weak — add length, uppercase, digit & special char', color: '#ef4444' };
    if (score <= 4) return { score: 4, label: '🟡 Fair — getting better, keep going', color: '#f59e0b' };
    if (score <= 5) return { score: 5, label: '🟢 Good password', color: '#22c55e' };
    return { score: 7, label: '💪 Strong password', color: '#27c380' };
  }

  updateStrengthMeter(password, fillId, labelId) {
    const fill = document.getElementById(fillId);
    const label = document.getElementById(labelId);
    if (!fill || !label) return;

    const { score, label: text, color } = this.evaluatePasswordStrength(password);
    const pct = password.length === 0 ? 0 : Math.min(100, Math.round((score / 7) * 100));
    fill.style.width = `${pct}%`;
    fill.style.background = color || 'transparent';
    label.textContent = text;
    label.style.color = color || 'var(--text-muted)';
  }

  checkPasswordMatch(password, confirm, labelId) {
    const label = document.getElementById(labelId);
    if (!label) return;
    if (!confirm) { label.textContent = ''; return; }
    if (password === confirm) {
      label.textContent = '✅ Passwords match';
      label.style.color = '#27c380';
    } else {
      label.textContent = '❌ Passwords do not match';
      label.style.color = '#ef4444';
    }
  }

  // ── Sign In ────────────────────────────────────────────────────────────────
  async handleSignIn(e) {
    e.preventDefault();
    this.clearErrors();

    const idInput = document.getElementById('signin-identifier');
    const pwdInput = document.getElementById('signin-password');
    const submitBtn = document.getElementById('btn-submit-signin');

    const identifier = idInput?.value.trim();
    const password = pwdInput?.value;

    if (!identifier || !password) {
      this.showError(this.signinError, 'Please provide both email/mobile and password.');
      return;
    }

    this.setButtonLoading(submitBtn, true);
    try {
      const res = await this.authService.login(identifier, password);
      this.hide();
      try { this.toastService?.success(`Welcome back, ${res.user?.name || 'Friend'}! 🥑`); } catch { /* noop */ }
      this.eventBus?.emit('auth:success', res.user);
    } catch (err) {
      if (err.status === 403 && err.data?.requireEmailVerification) {
        this.pendingEmail = err.data.email || identifier;
        if (this.otpTargetDisplay) this.otpTargetDisplay.textContent = this.pendingEmail;
        this.switchTab('verify');
        this.toastService?.warning('Please verify your email address to activate your account.');
      } else {
        this.showError(this.signinError, err.data?.error || err.message || 'Invalid credentials.');
      }
    } finally {
      this.setButtonLoading(submitBtn, false);
    }
  }

  // ── Register ───────────────────────────────────────────────────────────────
  async handleRegister(e) {
    e.preventDefault();
    this.clearErrors();

    const name = document.getElementById('reg-name')?.value.trim();
    const email = document.getElementById('reg-email')?.value.trim();
    const mobileNumber = document.getElementById('reg-mobile')?.value.trim();
    const password = document.getElementById('reg-password')?.value;
    const confirmPassword = document.getElementById('reg-confirm-password')?.value;
    const acceptTerms = document.getElementById('reg-consent-terms')?.checked;
    const acceptHealthConsent = document.getElementById('reg-consent-health')?.checked;
    const submitBtn = document.getElementById('btn-submit-register');

    // ── Client-side pre-flight checks ─────────────────────────────────────
    if (!acceptTerms || !acceptHealthConsent) {
      this.showError(this.registerError, 'You must accept both legal agreements to proceed.');
      return;
    }
    if (password !== confirmPassword) {
      this.showError(this.registerError, 'Password and Confirm Password do not match.');
      return;
    }
    const { score } = this.evaluatePasswordStrength(password);
    if (score <= 2) {
      this.showError(this.registerError, 'Password is too weak. Use at least 10 characters with uppercase, lowercase, digit and a special character.');
      return;
    }

    this.setButtonLoading(submitBtn, true);
    try {
      const res = await this.authService.register({
        name, email, mobileNumber, password,
        acceptTerms, acceptHealthConsent
      });

      this.pendingEmail = email;
      this.pendingMobile = mobileNumber;
      if (this.otpTargetDisplay) this.otpTargetDisplay.textContent = email;

      if (res.devOtpCode && this.devOtpHolder && this.devOtpCode) {
        this.devOtpCode.textContent = res.devOtpCode;
        this.devOtpHolder.style.display = 'flex';
      }

      this.switchTab('verify');
      this.startResendCountdown(60);
      this.toastService?.success('Account created! Enter the 6-digit verification code sent to your email.');
    } catch (err) {
      this.showError(this.registerError, err.data?.error || err.message || 'Registration failed.');
    } finally {
      this.setButtonLoading(submitBtn, false);
    }
  }

  // ── Verify OTP ─────────────────────────────────────────────────────────────
  async handleVerifyOtp(e) {
    e.preventDefault();
    this.clearErrors();

    const code = this.otpCodeInput?.value.trim();
    const submitBtn = document.getElementById('btn-submit-verify');

    if (!code || code.length !== 6) {
      this.showError(this.verifyError, 'Please enter a valid 6-digit OTP code.');
      return;
    }

    this.setButtonLoading(submitBtn, true);
    try {
      const res = await this.authService.verifyOtp(this.pendingEmail, 0, code); // 0 = Email
      this.hide();
      try { this.toastService?.success('Account verified and activated successfully! 🎉'); } catch { /* noop */ }
      this.eventBus?.emit('auth:success', res.user);
    } catch (err) {
      this.showError(this.verifyError, err.data?.error || err.message || 'Invalid or expired OTP code.');
    } finally {
      this.setButtonLoading(submitBtn, false);
    }
  }

  // ── Resend OTP (account verification) ──────────────────────────────────────
  async handleResendOtp() {
    if (!this.pendingEmail || this.resendSeconds > 0) return;
    try {
      const res = await this.authService.resendOtp(this.pendingEmail, 0);
      if (res.devOtpCode && this.devOtpHolder && this.devOtpCode) {
        this.devOtpCode.textContent = res.devOtpCode;
        this.devOtpHolder.style.display = 'flex';
      }
      this.toastService?.info('New verification code sent to your email.');
      this.startResendCountdown(60);
    } catch (err) {
      this.showError(this.verifyError, err.data?.error || err.message || 'Failed to resend code.');
    }
  }

  // ── Forgot Password — Step 1: send OTP ────────────────────────────────────
  async handleSendResetOtp() {
    this.clearErrors();
    const email = document.getElementById('reset-email')?.value.trim();
    const btn = document.getElementById('btn-send-reset-otp');

    if (!email) {
      this.showError(this.resetError, 'Please enter your registered email address.');
      return;
    }

    this.setButtonLoading(btn, true);
    try {
      const res = await this.authService.forgotPassword(email);
      this.pendingResetEmail = email;

      // Show dev OTP if returned
      const devHelper = document.getElementById('dev-reset-otp-helper');
      const devCode = document.getElementById('dev-reset-otp-code');
      if (res.devOtpCode && devHelper && devCode) {
        devCode.textContent = res.devOtpCode;
        devHelper.style.display = 'flex';
      }

      // Advance to Step B
      document.getElementById('reset-step-email').style.display = 'none';
      const stepB = document.getElementById('reset-step-newpwd');
      if (stepB) stepB.style.display = 'block';
      const emailDisplay = document.getElementById('reset-otp-email-display');
      if (emailDisplay) emailDisplay.textContent = email;
      this.startResetResendCountdown(60);
      this.toastService?.info('If an active account was found, a reset code has been sent.');
    } catch (err) {
      this.showError(this.resetError, err.data?.error || err.message || 'Failed to send reset code.');
    } finally {
      this.setButtonLoading(btn, false);
    }
  }

  // ── Forgot Password — Step 2: submit new password ─────────────────────────
  async handleSubmitReset() {
    this.clearErrors();
    const otpCode = document.getElementById('reset-otp-code')?.value.trim();
    const newPassword = document.getElementById('reset-new-password')?.value;
    const confirmPassword = document.getElementById('reset-confirm-password')?.value;
    const btn = document.getElementById('btn-submit-reset');

    if (!otpCode || otpCode.length !== 6) {
      this.showError(this.resetError, 'Please enter the 6-digit reset code.');
      return;
    }
    if (!newPassword || !confirmPassword) {
      this.showError(this.resetError, 'Please enter and confirm your new password.');
      return;
    }
    if (newPassword !== confirmPassword) {
      this.showError(this.resetError, 'New password and confirmation do not match.');
      return;
    }
    const { score } = this.evaluatePasswordStrength(newPassword);
    if (score <= 2) {
      this.showError(this.resetError, 'Password is too weak. Use at least 10 characters with uppercase, lowercase, digit and a special character.');
      return;
    }

    this.setButtonLoading(btn, true);
    try {
      await this.authService.resetPassword(this.pendingResetEmail, otpCode, newPassword, confirmPassword);
      this.showSuccess(this.resetSuccess, '✅ Password reset successful! Please sign in with your new password.');
      // Hide the form inputs, redirect to sign-in after a short delay
      document.getElementById('reset-step-newpwd').style.display = 'none';
      setTimeout(() => this.switchTab('signin'), 2500);
      this.toastService?.success('Password reset! Sign in with your new credentials. 🔑');
    } catch (err) {
      this.showError(this.resetError, err.data?.error || err.data?.message || err.message || 'Reset failed. Check the code and try again.');
    } finally {
      this.setButtonLoading(btn, false);
    }
  }

  // ── Resend reset OTP ────────────────────────────────────────────────────────
  async handleResendResetOtp() {
    if (!this.pendingResetEmail || this.resetResendSeconds > 0) return;
    try {
      const email = document.getElementById('reset-email')?.value.trim() || this.pendingResetEmail;
      const res = await this.authService.forgotPassword(email);
      const devHelper = document.getElementById('dev-reset-otp-helper');
      const devCode = document.getElementById('dev-reset-otp-code');
      if (res.devOtpCode && devHelper && devCode) {
        devCode.textContent = res.devOtpCode;
        devHelper.style.display = 'flex';
      }
      this.toastService?.info('A new reset code has been sent.');
      this.startResetResendCountdown(60);
    } catch (err) {
      this.showError(this.resetError, err.data?.error || err.message || 'Failed to resend reset code.');
    }
  }

  // ── Countdown helpers ──────────────────────────────────────────────────────
  startResendCountdown(seconds) {
    this.resendSeconds = seconds;
    if (this.btnResendOtp) this.btnResendOtp.disabled = true;
    if (this.resendCountdown) {
      this.resendCountdown.style.display = 'inline';
      this.resendCountdown.textContent = `(wait ${this.resendSeconds}s)`;
    }
    clearInterval(this.resendTimer);
    this.resendTimer = setInterval(() => {
      this.resendSeconds--;
      if (this.resendCountdown) this.resendCountdown.textContent = `(wait ${this.resendSeconds}s)`;
      if (this.resendSeconds <= 0) {
        clearInterval(this.resendTimer);
        if (this.btnResendOtp) this.btnResendOtp.disabled = false;
        if (this.resendCountdown) this.resendCountdown.style.display = 'none';
      }
    }, 1000);
  }

  startResetResendCountdown(seconds) {
    this.resetResendSeconds = seconds;
    const btn = document.getElementById('btn-resend-reset-otp');
    const countdown = document.getElementById('reset-resend-countdown');
    if (btn) btn.disabled = true;
    if (countdown) { countdown.style.display = 'inline'; countdown.textContent = `(wait ${seconds}s)`; }
    clearInterval(this.resetResendTimer);
    this.resetResendTimer = setInterval(() => {
      this.resetResendSeconds--;
      if (countdown) countdown.textContent = `(wait ${this.resetResendSeconds}s)`;
      if (this.resetResendSeconds <= 0) {
        clearInterval(this.resetResendTimer);
        if (btn) btn.disabled = false;
        if (countdown) countdown.style.display = 'none';
      }
    }, 1000);
  }

  // ── Legal modals ───────────────────────────────────────────────────────────
  openLegalModal(type) {
    if (type === 'terms' && this.legalTermsModal) this.legalTermsModal.style.display = 'flex';
    if (type === 'health' && this.legalHealthModal) this.legalHealthModal.style.display = 'flex';
  }

  closeLegalModal(type) {
    if (type === 'terms' && this.legalTermsModal) this.legalTermsModal.style.display = 'none';
    if (type === 'health' && this.legalHealthModal) this.legalHealthModal.style.display = 'none';
  }

  // ── Button state helper ────────────────────────────────────────────────────
  setButtonLoading(btn, isLoading) {
    if (!btn) return;
    btn.disabled = isLoading;
    const txt = btn.querySelector('.btn-text');
    const spinner = btn.querySelector('.btn-spinner');
    if (txt) txt.style.display = isLoading ? 'none' : 'inline';
    if (spinner) spinner.style.display = isLoading ? 'inline' : 'none';
  }
}
