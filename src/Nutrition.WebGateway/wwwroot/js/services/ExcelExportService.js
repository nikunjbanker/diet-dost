/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

/**
 * ExcelExportService
 * Single Responsibility: Pure data export serialization (CSV/Excel RFC 4180 with UTF-8 BOM),
 * entitlement gating checks, and paywall notifications for meal history export.
 */
export class ExcelExportService {
  /**
   * @param {Object} options
   * @param {import('./auth-service.js').AuthService} [options.authService]
   * @param {import('../ui/toast.js').ToastService} [options.toastService]
   * @param {import('./meals-service.js').MealsService} [options.mealsService]
   */
  constructor({ authService = null, toastService = null, mealsService = null }) {
    this._authService = authService;
    this._toast = toastService;
    this._meals = mealsService;
  }

  /**
   * Checks whether the current authenticated user has data export permissions.
   * @returns {boolean}
   */
  isExportAllowed() {
    const user = this._authService?.currentUser;
    return user?.entitlements?.allowDataExport ?? this._authService?.isAdmin() ?? false;
  }

  /**
   * Export loaded meal data to Microsoft Excel (RFC 4180 CSV with UTF-8 BOM).
   * @param {Array<Object>} meals - Array of meal objects to export
   * @param {string} [period='7D'] - Active timeline period
   * @param {string|null} [userId=null] - User ID for server export fallback
   * @returns {boolean} True if export succeeded, false if gated or failed
   */
  export(meals = [], period = '7D', userId = null) {
    if (!this.isExportAllowed()) {
      if (this._toast) {
        this._toast.show('Exporting meal history (Excel / CSV) is a Premium tier feature. Please upgrade your plan.', 'warning');
      }
      window.dispatchEvent(new CustomEvent('tier:upgrade_required', {
        detail: {
          error: 'FeatureTierUpgradeRequired',
          message: 'Exporting meal history (Excel / CSV) is a Premium tier feature. Please upgrade your plan.'
        }
      }));
      return false;
    }

    if (!meals || meals.length === 0) {
      if (this._toast) {
        this._toast.show('No meal data available to export in this period.', 'warning');
      }
      return false;
    }

    if (this._meals && typeof this._meals.exportMealsClientCsv === 'function') {
      const success = this._meals.exportMealsClientCsv(meals, period);
      if (success && this._toast) {
        this._toast.show(`📥 Successfully exported ${meals.length} meals to Excel!`, 'success');
      }
      return success;
    } else {
      // Direct server export fallback
      const uid = userId || this._authService?.currentUser?.id || '';
      const url = `/api/meals/export?userId=${encodeURIComponent(uid)}&period=${encodeURIComponent(period)}&format=csv`;
      window.location.href = url;
      return true;
    }
  }
}
