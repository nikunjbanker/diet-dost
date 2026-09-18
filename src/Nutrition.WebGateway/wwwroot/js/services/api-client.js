/**
 * ApiClient - Robust HTTP Client Wrapper
 * Centralizes request execution, JSON/FormData formatting, and error handling.
 */
export class ApiClient {
  constructor(baseUrl = '') {
    this.baseUrl = baseUrl;
  }

  /**
   * Perform HTTP GET request.
   * @param {string} url
   * @param {Object} [params]
   * @returns {Promise<any>}
   */
  async get(url, params = {}) {
    const query = new URLSearchParams();
    for (const [key, value] of Object.entries(params)) {
      if (value !== undefined && value !== null) {
        query.append(key, value);
      }
    }
    const queryString = query.toString() ? `?${query.toString()}` : '';
    const fullUrl = `${this.baseUrl}${url}${queryString}`;

    const res = await fetch(fullUrl, {
      method: 'GET',
      headers: { 'Accept': 'application/json' }
    });

    return this._handleResponse(res);
  }

  /**
   * Perform HTTP POST request (JSON).
   * @param {string} url
   * @param {Object} body
   * @returns {Promise<any>}
   */
  async postJson(url, body) {
    const fullUrl = `${this.baseUrl}${url}`;
    const res = await fetch(fullUrl, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json'
      },
      body: JSON.stringify(body)
    });

    return this._handleResponse(res);
  }

  /**
   * Perform HTTP POST request (FormData for file uploads).
   * @param {string} url
   * @param {FormData} formData
   * @returns {Promise<any>}
   */
  async postForm(url, formData) {
    const fullUrl = `${this.baseUrl}${url}`;
    const res = await fetch(fullUrl, {
      method: 'POST',
      body: formData
    });

    return this._handleResponse(res);
  }

  /**
   * Perform HTTP PUT request (JSON).
   * @param {string} url
   * @param {Object} body
   * @returns {Promise<any>}
   */
  async putJson(url, body) {
    const fullUrl = `${this.baseUrl}${url}`;
    const res = await fetch(fullUrl, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json'
      },
      body: JSON.stringify(body)
    });

    return this._handleResponse(res);
  }

  /**
   * Perform HTTP DELETE request.
   * @param {string} url
   * @returns {Promise<any>}
   */
  async delete(url) {
    const fullUrl = `${this.baseUrl}${url}`;
    const res = await fetch(fullUrl, {
      method: 'DELETE',
      headers: {
        'Accept': 'application/json'
      }
    });

    return this._handleResponse(res);
  }

  /**
   * Unified response handler with typed error parsing.
   * @param {Response} res
   */
  async _handleResponse(res) {
    let data;
    const contentType = res.headers.get('content-type') || '';
    if (contentType.includes('application/json')) {
      data = await res.json();
    } else {
      data = await res.text();
    }

    if (!res.ok) {
      const errorMsg = data?.error || data?.message || `HTTP ${res.status}: ${res.statusText}`;
      const error = new Error(errorMsg);
      error.status = res.status;
      error.data = data;
      throw error;
    }

    return data;
  }
}

export const apiClient = new ApiClient();
