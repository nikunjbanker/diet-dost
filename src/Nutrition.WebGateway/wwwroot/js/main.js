/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
/**
 * Main Application Composition Root
 * Bootstraps the Dependency Injection container, instantiates services,
 * initializes UI controllers, and sets up window facades for legacy HTML compatibility.
 */
import { container } from './core/di-container.js';
import { eventBus } from './core/event-bus.js';
import { appState } from './core/state.js';

import { ApiClient, apiClient, getWebDashboard } from './services/api.js?v=1.3.9';
import { AuthService } from './services/auth-service.js?v=1.3.9';
import { AdminService } from './services/admin-service.js?v=1.3.9';
import { MealsService } from './services/meals-service.js?v=1.3.9';
import { ProfileService } from './services/profile-service.js?v=1.3.9';
import { AnalyticsService } from './services/analytics-service.js?v=1.3.9';
import { ProgressPhotosService } from './services/progress-service.js?v=1.3.9';
import { MedicationService } from './services/medication-service.js?v=1.3.9';

import { toastService } from './ui/toast.js?v=1.3.9';
import { confettiService } from './ui/confetti.js?v=1.3.9';
import { DailyHudController } from './ui/daily-hud.js?v=1.3.9';
import { MealLoggerController } from './ui/meal-logger.js?v=1.3.9';
import { ReviewModalController } from './ui/review-modal.js?v=1.3.9';
import { AnalyticsChartController } from './ui/analytics-chart.js?v=1.3.9';
import { ExcelExportService } from './services/ExcelExportService.js?v=1.3.9';
import { ProfileModalController } from './ui/profile-modal.js?v=1.3.9';
import { TransparencyModalController } from './ui/transparency-modal.js?v=1.3.9';
import { ProgressModalController } from './ui/progress-modal.js?v=1.3.9';
import { AuthGateController } from './ui/auth-gate.js?v=1.3.9';
import { AdminModalController } from './ui/admin-modal.js?v=1.3.9';
import { QuotaModalController } from './ui/quota-modal.js?v=1.3.9';

// ============================================================================
// Global Image Fallback Handler (Capturing phase catches all failed <img> loads)
// ============================================================================
window.addEventListener('error', (event) => {
  const target = event.target;
  if (target && target.tagName === 'IMG') {
    if (!target.dataset.hasFallback) {
      target.dataset.hasFallback = 'true';
      const id = target.id || '';
      const cls = typeof target.className === 'string' ? target.className : '';
      const isProgress = id.includes('face') || id.includes('body') || cls.includes('progress') || cls.includes('timeline-thumb');
      target.src = isProgress ? '/assets/placeholder-progress.svg' : '/assets/placeholder-meal.svg';
    }
  }
}, true);

// ============================================================================
// 1. Dependency Injection Registration (DIP & IoC)
// ============================================================================
container.register('eventBus', eventBus);
container.register('appState', appState);
container.register('apiClient', apiClient);

container.register('toastService', toastService);
container.register('confettiService', confettiService);

container.register('authService', (c) => new AuthService(c.resolve('apiClient')));
container.register('adminService', (c) => new AdminService(c.resolve('apiClient')));

container.register('mealsService', (c) => new MealsService(c.resolve('apiClient')));
container.register('profileService', (c) => new ProfileService(c.resolve('apiClient')));
container.register('analyticsService', (c) => new AnalyticsService(c.resolve('apiClient')));
container.register('progressService', (c) => new ProgressPhotosService(c.resolve('apiClient')));
container.register('medicationService', () => new MedicationService());

container.register('dailyHud', (c) => new DailyHudController({
  analyticsService: c.resolve('analyticsService'),
  appState: c.resolve('appState'),
  eventBus: c.resolve('eventBus')
}));

container.register('mealLogger', (c) => new MealLoggerController({
  mealsService: c.resolve('mealsService'),
  toastService: c.resolve('toastService'),
  appState: c.resolve('appState'),
  eventBus: c.resolve('eventBus')
}));

container.register('reviewModal', (c) => new ReviewModalController({
  mealsService: c.resolve('mealsService'),
  toastService: c.resolve('toastService'),
  confettiService: c.resolve('confettiService'),
  appState: c.resolve('appState'),
  eventBus: c.resolve('eventBus')
}));

container.register('excelExportService', (c) => new ExcelExportService({
  authService: c.resolve('authService'),
  toastService: c.resolve('toastService'),
  mealsService: c.resolve('mealsService')
}));

container.register('analyticsChart', (c) => new AnalyticsChartController({
  analyticsService: c.resolve('analyticsService'),
  mealsService: c.resolve('mealsService'),
  apiClient: c.resolve('apiClient'),
  toastService: c.resolve('toastService'),
  authService: c.resolve('authService'),
  appState: c.resolve('appState'),
  eventBus: c.resolve('eventBus'),
  excelExportService: c.resolve('excelExportService')
}));

container.register('profileModal', (c) => new ProfileModalController({
  profileService: c.resolve('profileService'),
  medicationService: c.resolve('medicationService'),
  toastService: c.resolve('toastService'),
  confettiService: c.resolve('confettiService'),
  appState: c.resolve('appState'),
  eventBus: c.resolve('eventBus')
}));

container.register('transparencyModal', (c) => new TransparencyModalController({
  profileService: c.resolve('profileService'),
  appState: c.resolve('appState'),
  eventBus: c.resolve('eventBus')
}));

container.register('progressModal', (c) => new ProgressModalController({
  progressService: c.resolve('progressService'),
  toastService: c.resolve('toastService'),
  confettiService: c.resolve('confettiService'),
  authService: c.resolve('authService'),
  appState: c.resolve('appState'),
  eventBus: c.resolve('eventBus')
}));

container.register('authGate', (c) => new AuthGateController({
  authService: c.resolve('authService'),
  toastService: c.resolve('toastService'),
  eventBus: c.resolve('eventBus')
}));

container.register('adminModal', (c) => new AdminModalController({
  adminService: c.resolve('adminService'),
  toastService: c.resolve('toastService'),
  eventBus: c.resolve('eventBus')
}));

container.register('quotaModal', (c) => new QuotaModalController({
  apiClient: c.resolve('apiClient'),
  toastService: c.resolve('toastService'),
  eventBus: c.resolve('eventBus')
}));

/**
 * Asynchronously loads modular HTML partials defined by [data-include="path/to/file.html"]
 * Enables zero-bundler modular architecture while keeping index.html clean and minimal.
 */
async function loadPartials() {
  const elements = document.querySelectorAll('[data-include]');
  await Promise.all(Array.from(elements).map(async (el) => {
    const file = el.getAttribute('data-include');
    try {
      const res = await fetch(file, { cache: 'no-cache' });
      if (!res.ok) throw new Error(`HTTP ${res.status} while loading ${file}`);
      const html = await res.text();
      el.outerHTML = html;
    } catch (err) {
      console.error(`Failed to load partial: ${file}`, err);
    }
  }));
}

// ============================================================================
// 2. Application Bootstrap
// ============================================================================
async function initApp() {
  // Load modular HTML partials into the DOM before initializing controllers
  await loadPartials();

  // Resolve and initialize all controllers (DOM is now guaranteed to be populated)
  const authService = container.resolve('authService');
  const authGate = container.resolve('authGate');
  const adminModal = container.resolve('adminModal');
  const quotaModal = container.resolve('quotaModal');

  const dailyHud = container.resolve('dailyHud');
  const mealLogger = container.resolve('mealLogger');
  const reviewModal = container.resolve('reviewModal');
  const analyticsChart = container.resolve('analyticsChart');
  const profileModal = container.resolve('profileModal');
  const transparencyModal = container.resolve('transparencyModal');
  const progressModal = container.resolve('progressModal');

  // Helper: Synchronize user header badges and visibility
  function updateUserUI(user, featureFlags = null, quota = null) {
    const avatarEl = document.getElementById('header-user-avatar');
    const nameEl = document.getElementById('header-user-name');
    const tierPillEl = document.getElementById('header-tier-pill');
    const quotaBadgeEl = document.getElementById('ai-quota-badge');
    const dropdownNameEl = document.getElementById('dropdown-user-fullname');
    const dropdownEmailEl = document.getElementById('dropdown-user-email');
    const adminMenuItem = document.getElementById('menu-open-admin');
    const quotaPreviewEl = document.getElementById('menu-quota-preview');
    const mainContainer = document.querySelector('main.container');

    if (!user) {
      // Reset state to unauthenticated defaults
      appState.userId = 'user-default';
      if (nameEl) nameEl.textContent = 'Sign In';
      if (tierPillEl) {
        tierPillEl.textContent = 'Guest';
        tierPillEl.className = 'tier-badge-pill';
      }
      if (quotaBadgeEl) quotaBadgeEl.style.display = 'none';
      if (dropdownNameEl) dropdownNameEl.textContent = 'Guest';
      if (dropdownEmailEl) dropdownEmailEl.textContent = 'Not signed in';
      if (adminMenuItem) adminMenuItem.style.display = 'none';
      if (mainContainer) mainContainer.style.display = 'none';
      return;
    }

    // ── Update global state with authenticated user's identity ──────────────
    const userId = user.userId || user.id;
    if (userId) appState.userId = userId;
    if (user.userTimezone) appState.userTimezone = user.userTimezone;

    if (mainContainer) mainContainer.style.display = 'block';

    const displayName = user.displayName || user.name || (user.email ? user.email.split('@')[0] : 'User');
    if (nameEl) nameEl.textContent = displayName;
    if (dropdownNameEl) dropdownNameEl.textContent = displayName;
    if (dropdownEmailEl) dropdownEmailEl.textContent = user.email || '';

    const tierName = typeof user.tier === 'number'
      ? (user.tier === 3 ? 'SuperAdmin' : user.tier === 2 ? 'Premium' : user.tier === 1 ? 'Basic' : 'Free')
      : (user.tier || 'Free');

    if (tierPillEl) {
      tierPillEl.textContent = tierName === 'SuperAdmin' ? '👑 Super' : tierName === 'Premium' ? '⚡ Premium' : tierName === 'Basic' ? '⭐ Basic' : '🆓 Free';
      tierPillEl.className = `tier-badge-pill tier-${tierName.toLowerCase()}`;
    }

    if (quota) {
      const isUnlimited = quota.dailyLimit < 0;
      const count = quota.remainingCalls;
      const label = isUnlimited
        ? 'Unlimited scans'
        : `${count} scan${count === 1 ? '' : 's'} remaining today`;

      if (quotaBadgeEl) {
        quotaBadgeEl.textContent = label;
        quotaBadgeEl.style.display = 'inline-block';
      }
      if (quotaPreviewEl) {
        quotaPreviewEl.textContent = isUnlimited ? 'Unlimited' : `${count} left today`;
      }
    } else {
      if (quotaBadgeEl) quotaBadgeEl.style.display = 'none';
      if (quotaPreviewEl) quotaPreviewEl.textContent = tierName;
    }

    const isAdmin = featureFlags?.isAdmin ?? (user.role === 'Admin' || user.role === 'SuperAdmin' || user.role === 1 || user.role === 2);
    if (adminMenuItem) {
      adminMenuItem.style.display = isAdmin ? 'flex' : 'none';
    }

    // Keep authService.currentUser synchronized with entitlements for modals & feature gates
    authService.currentUser = {
      id: userId,
      email: user.email,
      name: displayName,
      role: user.role,
      tier: tierName,
      entitlements: {
        allowPhotoCompare: featureFlags?.canComparePhotos ?? (tierName === 'Premium' || tierName === 'SuperAdmin' || isAdmin),
        allowDataExport: featureFlags?.canExportData ?? (tierName === 'Premium' || tierName === 'SuperAdmin' || isAdmin),
        historyLimitDays: featureFlags?.historyDayLimit ?? (tierName === 'Premium' || tierName === 'SuperAdmin' || isAdmin ? 365 : tierName === 'Basic' ? 30 : 7),
        hasAdvancedAnalytics: featureFlags?.hasAdvancedAnalytics ?? (tierName === 'Premium' || tierName === 'SuperAdmin' || isAdmin)
      }
    };
  }

  // User menu dropdown toggle
  const userMenuBtn = document.getElementById('btn-user-menu');
  const userMenuDropdown = document.getElementById('user-menu-dropdown');
  userMenuBtn?.addEventListener('click', (e) => {
    e.stopPropagation();
    if (!authService.currentUser) {
      authGate.show('signin');
      return;
    }
    if (userMenuDropdown) {
      const isVisible = userMenuDropdown.style.display === 'block';
      userMenuDropdown.style.display = isVisible ? 'none' : 'block';
    }
  });

  document.addEventListener('click', (e) => {
    if (userMenuDropdown && !userMenuDropdown.contains(e.target) && e.target !== userMenuBtn) {
      userMenuDropdown.style.display = 'none';
    }
  });

  document.getElementById('menu-open-quota')?.addEventListener('click', () => {
    if (userMenuDropdown) userMenuDropdown.style.display = 'none';
    quotaModal.open();
  });

  document.getElementById('menu-open-admin')?.addEventListener('click', () => {
    if (userMenuDropdown) userMenuDropdown.style.display = 'none';
    adminModal.open();
  });

  document.getElementById('menu-btn-signout')?.addEventListener('click', async () => {
    if (userMenuDropdown) userMenuDropdown.style.display = 'none';
    await authService.logout();
    updateUserUI(null);
    authGate.show('signin');
  });

  // Global window event listeners for auth/quota lifecycle
  window.addEventListener('auth:token_expired', () => {
    container.resolve('toastService')?.warning('Your session has expired. Please sign in again to continue.');
  });

  window.addEventListener('auth:unauthorized', () => {
    updateUserUI(null);
    authGate.show('signin');
  });

  window.addEventListener('quota:exceeded', (e) => {
    container.resolve('toastService')?.error(e.detail?.message || 'Daily AI detection limit reached for your tier.');
    quotaModal.open();
  });

  window.addEventListener('tier:upgrade_required', (e) => {
    container.resolve('toastService')?.warning(e.detail?.message || 'This feature requires a tier upgrade.');
    quotaModal.open();
  });

  /**
   * Synchronously hydrates all dashboard UI components from the Web BFF composite payload.
   * Eliminates 5-6 fragmented requests and prevents Cumulative Layout Shift (CLS).
   * @param {Object} dashboard - WebDashboardCompositeDto
   */
  function hydrateFromDashboard(dashboard) {
    if (!dashboard) return;
    updateUserUI(dashboard.user, dashboard.featureFlags, dashboard.quota);
    dailyHud.refresh(dashboard.todayLedger);
    analyticsChart.refresh(dashboard.projections, dashboard.recentMeals);
    quotaModal.hydrate(dashboard.quota);
    progressModal.refresh(dashboard.featureFlags);
  }

  eventBus.on('auth:success', async (user) => {
    try {
      const period = appState.activePeriod || '7D';
      const dashboard = await apiClient.getWebDashboard(period);
      hydrateFromDashboard(dashboard);
    } catch (err) {
      console.warn('[Main] Error hydrating dashboard after login:', err);
      // Fallback update if composite fails
      updateUserUI(user);
      await Promise.allSettled([
        dailyHud.refresh(),
        analyticsChart.refresh(),
        progressModal.refresh()
      ]);
    }
  });

  // ============================================================================
  // 3. Global Window Facades (for HTML onclick & inline attribute compatibility)
  // ============================================================================
  window.openAuthGate = (tab) => authGate.show(tab);
  window.closeAuthGate = () => authGate.hide();
  window.openAdminModal = () => adminModal.open();
  window.closeAdminModal = () => adminModal.close();
  window.openQuotaModal = () => quotaModal.open();
  window.closeQuotaModal = () => quotaModal.close();
  window.openProgressModal = (tab) => progressModal.open(tab);
  window.closeProgressModal = () => progressModal.close();
  window.openTransparencyModal = () => transparencyModal.open();
  window.closeTransparencyModal = () => transparencyModal.close();
  window.openProfileModal = () => profileModal.open();
  window.closeProfileModal = () => profileModal.close();
  window.switchAnalyticsPeriod = (period) => analyticsChart.switchPeriod(period);
  window.quickAddSubzi = (name, kcal, p, c, f) => reviewModal.quickAddSubzi(name, kcal, p, c, f);
  window.updateItemName = (idx, name) => reviewModal.updateItemName(idx, name);
  window.deleteReviewItem = (idx) => reviewModal.deleteItem(idx);
  window.stepItemQuantity = (idx, delta) => reviewModal.stepItemQuantity(idx, delta);
  window.appendMedication = (med) => profileModal.appendMedication(med);
  window.clearMedications = () => profileModal.clearMedications();

  // ============================================================================
  // 4. Initial Application Load (Single-Roundtrip Web BFF Hydration)
  // ============================================================================
  try {
    const period = appState.activePeriod || '7D';
    const dashboard = await apiClient.getWebDashboard(period);
    hydrateFromDashboard(dashboard);
  } catch (err) {
    if (err.status === 401) {
      updateUserUI(null);
      authGate.show('signin');
    } else {
      console.warn('[Main] Error during initial Web BFF hydration:', err);
      const currentUser = await authService.getCurrentUser().catch(() => null);
      if (!currentUser || !currentUser.isEmailVerified) {
        updateUserUI(null);
        authGate.show('signin');
      } else {
        updateUserUI(currentUser);
        await Promise.allSettled([
          dailyHud.refresh(),
          analyticsChart.refresh(),
          progressModal.refresh()
        ]);
      }
    }
  }
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initApp);
} else {
  initApp();
}

