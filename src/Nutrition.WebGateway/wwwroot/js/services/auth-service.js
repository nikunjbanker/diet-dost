/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
/**
 * Authentication and User Identity Service
 * Handles registration, OTP verification, session management, and current user state.
 */
export class AuthService {
  constructor(apiClient) {
    this.api = apiClient;
    this.currentUser = null;
  }

  async getCurrentUser() {
    try {
      const res = await this.api.get('/api/auth/me');
      // /api/auth/me returns { isAuthenticated, user: { id, email, name, role, tier, ... } }
      // Unwrap the envelope so callers receive the user object directly.
      const user = res?.user ?? null;
      this.currentUser = user;
      return user;
    } catch (err) {
      this.currentUser = null;
      return null;
    }
  }

  async login(emailOrMobile, password) {
    const res = await this.api.post('/api/auth/login', {
      emailOrMobile,
      password
    });
    if (res.token) {
      localStorage.setItem('dd_jwt_token', res.token);
    }
    this.currentUser = res.user;
    return res;
  }

  async register(data) {
    return await this.api.post('/api/auth/register', data);
  }

  async verifyOtp(target, channel, code) {
    const res = await this.api.post('/api/auth/verify-otp', {
      target,
      channel,
      code
    });
    if (res.token) {
      localStorage.setItem('dd_jwt_token', res.token);
    }
    this.currentUser = res.user;
    return res;
  }

  async resendOtp(target, channel) {
    return await this.api.post('/api/auth/resend-otp', {
      target,
      channel
    });
  }

  async logout() {
    try {
      await this.api.post('/api/auth/logout', {});
    } finally {
      localStorage.removeItem('dd_jwt_token');
      this.currentUser = null;
    }
  }

  async deleteAccount() {
    const res = await this.api.post('/api/auth/delete-account', {});
    localStorage.removeItem('dd_jwt_token');
    this.currentUser = null;
    return res;
  }

  /**
   * Step 1 of password reset: request a 6-digit OTP to the registered email.
   * The server returns a generic success even if the email is not found
   * (anti-enumeration, OWASP A07:2021).
   */
  async forgotPassword(email) {
    return await this.api.post('/api/auth/forgot-password', { email });
  }

  /**
   * Step 2 of password reset: submit the OTP + new password.
   * Both newPassword and confirmNewPassword must match (server also validates).
   */
  async resetPassword(email, otpCode, newPassword, confirmNewPassword) {
    return await this.api.post('/api/auth/reset-password', {
      email,
      otpCode,
      newPassword,
      confirmNewPassword
    });
  }


  getToken() {
    return localStorage.getItem('dd_jwt_token');
  }

  isAdmin() {
    return this.currentUser?.role === 'Admin' || this.currentUser?.role === 'SuperAdmin';
  }

  isSuperAdmin() {
    return this.currentUser?.role === 'SuperAdmin';
  }
}
