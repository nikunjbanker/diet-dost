/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
/**
 * AnalyticsService
 * Handles deficit projections, historical trends, and compliance metrics (7D, 30D, 90D, 365D).
 */
export class AnalyticsService {
  /**
   * @param {import('./api-client.js').ApiClient} apiClient
   */
  constructor(apiClient) {
    this._api = apiClient;
  }

  /**
   * Retrieve projections and daily trend bars for a given time window.
   * @param {string} userId
   * @param {string} period - '7D', '30D', '90D', or '365D'
   */
  async getProjections(userId, period) {
    return this._api.get('/api/analytics/projections', { userId, period });
  }

  /**
   * Retrieve daily calorie balance and macro consumption ledger for today.
   * @param {string} userId
   */
  async getDailyLedger(userId) {
    return this._api.get('/api/analytics/daily', { userId });
  }
}

