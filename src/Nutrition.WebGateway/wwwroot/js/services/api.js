/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
/**
 * API Service & Web BFF Client Facade
 * Provides high-level typed API calls, including single-roundtrip Web BFF dashboard hydration.
 */
import { apiClient, ApiClient } from './api-client.js';

export { ApiClient, apiClient };

/**
 * Retrieves the composite web dashboard payload in a single HTTP roundtrip.
 * Invokes GET /api/web/v1/dashboard?period=${period}
 * @param {string} [period='7D']
 * @returns {Promise<any>}
 */
export async function getWebDashboard(period = '7D') {
  return apiClient.getWebDashboard(period);
}
