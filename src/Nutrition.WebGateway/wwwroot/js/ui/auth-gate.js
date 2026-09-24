/**
 * Obsidian Authentication & Verification Gate UI Controller
 * Blocks the entire dashboard when unauthenticated or unverified,
 * enforcing India DPDPA 2023 dual-consent and 6-digit email OTP activation.
 */
export class AuthGateController {
  constructor({ authService, toastService, eventBus }) {
    this.authService = authService;
    this.toastService = toastService;
    this.eventBus = eventBus;

    this.pendingEmail = null;
    this.pendingMobile = null;
    this.resendTimer = null;
    this.resendSeconds = 0;

    this.initElements();
    this.bindEvents();
  }

  initElements() {
    this.modal = document.getElementById('auth-gate-modal');
    this.tabSignIn = document.getElementById('btn-tab-signin');
    this.tabRegister = document.getElementById('btn-tab-register');
    this.tabVerify = document.getElementById('btn-tab-verify');

    this.formSignIn = document.getElementById('form-signin');
    this.formRegister = document.getElementById('form-register');
    this.formVerify = document.getElementById('form-verify-otp');

    this.signinError = document.getElementById('signin-error');
    this.registerError = document.getElementById('register-error');
    this.verifyError = document.getElementById('verify-error');

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

    document.getElementById('link-goto-register')?.addEventListener('click', () => this.switchTab('register'));
    document.getElementById('link-goto-signin')?.addEventListener('click', () => this.switchTab('signin'));

    // Legal modal openers (stop propagation to prevent label toggling the checkbox)
    document.getElementById('link-open-terms')?.addEventListener('click', (e) => {
      e.preventDefault();
      e.stopPropagation();
      this.openLegalModal('terms');
    });
    document.getElementById('link-open-health-consent')?.addEventListener('click', (e) => {
      e.preventDefault();
      e.stopPropagation();
      this.openLegalModal('health');
    });
    document.getElementById('btn-close-terms-modal')?.addEventListener('click', () => this.closeLegalModal('terms'));
    document.getElementById('btn-close-health-modal')?.addEventListener('click', () => this.closeLegalModal('health'));
    
    // Backdrop clicks to close legal modals
    this.legalTermsModal?.addEventListener('click', (e) => {
      if (e.target === this.legalTermsModal) this.closeLegalModal('terms');
    });
    this.legalHealthModal?.addEventListener('click', (e) => {
      if (e.target === this.legalHealthModal) this.closeLegalModal('health');
    });

    // Escape key closes open legal modals
    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') {
        if (this.legalTermsModal && this.legalTermsModal.style.display === 'flex') this.closeLegalModal('terms');
        if (this.legalHealthModal && this.legalHealthModal.style.display === 'flex') this.closeLegalModal('health');
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

    // Form Submissions
    this.formSignIn?.addEventListener('submit', (e) => this.handleSignIn(e));
    this.formRegister?.addEventListener('submit', (e) => this.handleRegister(e));
    this.formVerify?.addEventListener('submit', (e) => this.handleVerifyOtp(e));

    // Dev OTP auto-fill
    this.btnFillDevOtp?.addEventListener('click', () => {
      if (this.devOtpCode && this.otpCodeInput) {
        this.otpCodeInput.value = this.devOtpCode.textContent.trim();
      }
    });

    // Resend OTP
    this.btnResendOtp?.addEventListener('click', () => this.handleResendOtp());
  }

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

    if (this.formSignIn) this.formSignIn.style.display = tab === 'signin' ? 'block' : 'none';
    if (this.formRegister) this.formRegister.style.display = tab === 'register' ? 'block' : 'none';
    if (this.formVerify) this.formVerify.style.display = tab === 'verify' ? 'block' : 'none';

    if (tab === 'verify' && this.tabVerify) {
      this.tabVerify.style.display = 'inline-block';
    }
  }

  clearErrors() {
    if (this.signinError) this.signinError.style.display = 'none';
    if (this.registerError) this.registerError.style.display = 'none';
    if (this.verifyError) this.verifyError.style.display = 'none';
  }

  showError(container, message) {
    if (container) {
      container.textContent = message;
      container.style.display = 'block';
    }
  }

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
      this.toastService?.success(`Welcome back, ${res.user.name || 'Friend'}! 🥑`);
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

  async handleRegister(e) {
    e.preventDefault();
    this.clearErrors();

    const name = document.getElementById('reg-name')?.value.trim();
    const email = document.getElementById('reg-email')?.value.trim();
    const mobileNumber = document.getElementById('reg-mobile')?.value.trim();
    const password = document.getElementById('reg-password')?.value;
    const acceptTerms = document.getElementById('reg-consent-terms')?.checked;
    const acceptHealthConsent = document.getElementById('reg-consent-health')?.checked;
    const submitBtn = document.getElementById('btn-submit-register');

    if (!acceptTerms || !acceptHealthConsent) {
      this.showError(this.registerError, 'You must accept both legal agreements to proceed.');
      return;
    }

    this.setButtonLoading(submitBtn, true);
    try {
      const res = await this.authService.register({
        name,
        email,
        mobileNumber,
        password,
        acceptTerms,
        acceptHealthConsent
      });

      this.pendingEmail = email;
      this.pendingMobile = mobileNumber;

      if (this.otpTargetDisplay) this.otpTargetDisplay.textContent = email;

      // Handle dev OTP display if returned in non-prod
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
      this.toastService?.success('Account verified and activated successfully! 🎉');
      this.eventBus?.emit('auth:success', res.user);
    } catch (err) {
      this.showError(this.verifyError, err.data?.error || err.message || 'Invalid or expired OTP code.');
    } finally {
      this.setButtonLoading(submitBtn, false);
    }
  }

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

  openLegalModal(type) {
    if (type === 'terms' && this.legalTermsModal) this.legalTermsModal.style.display = 'flex';
    if (type === 'health' && this.legalHealthModal) this.legalHealthModal.style.display = 'flex';
  }

  closeLegalModal(type) {
    if (type === 'terms' && this.legalTermsModal) this.legalTermsModal.style.display = 'none';
    if (type === 'health' && this.legalHealthModal) this.legalHealthModal.style.display = 'none';
  }

  setButtonLoading(btn, isLoading) {
    if (!btn) return;
    btn.disabled = isLoading;
    const txt = btn.querySelector('.btn-text');
    const spinner = btn.querySelector('.btn-spinner');
    if (txt) txt.style.display = isLoading ? 'none' : 'inline';
    if (spinner) spinner.style.display = isLoading ? 'inline' : 'none';
  }
}
