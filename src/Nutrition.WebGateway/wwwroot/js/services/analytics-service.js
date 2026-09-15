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

