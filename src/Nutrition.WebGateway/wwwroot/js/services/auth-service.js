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
      const user = await this.api.get('/api/auth/me');
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
