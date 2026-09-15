/**
 * ProgressPhotosService
 * Handles visual progress photo comparison data, chronologically sorted gallery, and photo upload.
 */
export class ProgressPhotosService {
  /**
   * @param {import('./api-client.js').ApiClient} apiClient
   */
  constructor(apiClient) {
    this._api = apiClient;
  }

  /**
   * Fetch progress comparison summary (baseline vs latest face & full body, delta kg, days).
   * @param {string} userId
   */
  async getComparison(userId) {
    return this._api.get('/api/progress-photos/comparison', { userId });
  }

  /**
   * Upload a new progress check-in photo.
   * @param {FormData} formData
   */
  async uploadPhoto(formData) {
    return this._api.postForm('/api/progress-photos/upload', formData);
  }

  /**
   * Retrieve all progress photos for user.
   * @param {string} userId
   */
  async getUserPhotos(userId) {
    return this._api.get('/api/progress-photos', { userId });
  }
}
