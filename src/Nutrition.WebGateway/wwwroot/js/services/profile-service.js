/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
/**
 * ProfileService
 * Handles clinical intake profile retrieval, target recalculation, and daily ledger data.
 */
export class ProfileService {
  /**
   * @param {import('./api-client.js').ApiClient} apiClient
   */
  constructor(apiClient) {
    this._api = apiClient;
  }

  /**
   * Get user profile, clinical budget targets, and macro split.
   * @param {string} userId
   */
  async getProfile(userId) {
    return this._api.get(`/api/profile/${userId}`);
  }

  /**
   * Save profile and recalculate ICMR-NIN / WHO clinical targets.
   * @param {Object} profileData
   */
  async saveProfile(profileData) {
    return this._api.postJson('/api/profile', profileData);
  }

  /**
   * Get daily calorie ledger and macro consumption for today.
   * @param {string} userId
   */
  async getDailyLedger(userId) {
    return this._api.get(`/api/meals/daily-ledger`, { userId });
  }
}
