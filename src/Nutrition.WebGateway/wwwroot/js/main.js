/**
 * Main Application Composition Root
 * Bootstraps the Dependency Injection container, instantiates services,
 * initializes UI controllers, and sets up window facades for legacy HTML compatibility.
 */
import { container } from './core/di-container.js';
import { eventBus } from './core/event-bus.js';
import { appState } from './core/state.js';

import { ApiClient, apiClient } from './services/api-client.js?v=1.3.6';
import { AuthService } from './services/auth-service.js?v=1.3.6';
import { AdminService } from './services/admin-service.js?v=1.3.6';
import { MealsService } from './services/meals-service.js?v=1.3.6';
import { ProfileService } from './services/profile-service.js?v=1.3.6';
import { AnalyticsService } from './services/analytics-service.js?v=1.3.6';
import { ProgressPhotosService } from './services/progress-service.js?v=1.3.6';
import { MedicationService } from './services/medication-service.js?v=1.3.6';

import { toastService } from './ui/toast.js?v=1.3.6';
import { confettiService } from './ui/confetti.js?v=1.3.6';
import { DailyHudController } from './ui/daily-hud.js?v=1.3.6';
import { MealLoggerController } from './ui/meal-logger.js?v=1.3.6';
import { ReviewModalController } from './ui/review-modal.js?v=1.3.6';
import { AnalyticsChartController } from './ui/analytics-chart.js?v=1.3.6';
import { ProfileModalController } from './ui/profile-modal.js?v=1.3.6';
import { TransparencyModalController } from './ui/transparency-modal.js?v=1.3.6';
import { ProgressModalController } from './ui/progress-modal.js?v=1.3.6';
import { AuthGateController } from './ui/auth-gate.js?v=1.3.6';
import { AdminModalController } from './ui/admin-modal.js?v=1.3.6';
import { QuotaModalController } from './ui/quota-modal.js?v=1.3.6';

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

container.register('analyticsChart', (c) => new AnalyticsChartController({
  analyticsService: c.resolve('analyticsService'),
  mealsService: c.resolve('mealsService'),
  toastService: c.resolve('toastService'),
  authService: c.resolve('authService'),
  appState: c.resolve('appState'),
  eventBus: c.resolve('eventBus')
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
  function updateUserUI(user) {
    const avatarEl = document.getElementById('header-user-avatar');
    const nameEl = document.getElementById('header-user-name');
    const tierPillEl = document.getElementById('header-tier-pill');
    const dropdownNameEl = document.getElementById('dropdown-user-fullname');
    const dropdownEmailEl = document.getElementById('dropdown-user-email');
    const adminMenuItem = document.getElementById('menu-open-admin');
    const quotaPreviewEl = document.getElementById('menu-quota-preview');
    const mainContainer = document.querySelector('main.container');

    if (!user) {
      if (nameEl) nameEl.textContent = 'Sign In';
      if (tierPillEl) {
        tierPillEl.textContent = 'Guest';
        tierPillEl.className = 'tier-badge-pill';
      }
      if (dropdownNameEl) dropdownNameEl.textContent = 'Guest';
      if (dropdownEmailEl) dropdownEmailEl.textContent = 'Not signed in';
      if (adminMenuItem) adminMenuItem.style.display = 'none';
      if (mainContainer) mainContainer.style.display = 'none';
      return;
    }

    if (mainContainer) mainContainer.style.display = 'block';

    const displayName = user.name || (user.email ? user.email.split('@')[0] : 'User');
    if (nameEl) nameEl.textContent = displayName;
    if (dropdownNameEl) dropdownNameEl.textContent = user.name || displayName;
    if (dropdownEmailEl) dropdownEmailEl.textContent = user.email || '';

    const tierName = typeof user.tier === 'number'
      ? (user.tier === 3 ? 'SuperAdmin' : user.tier === 2 ? 'Premium' : user.tier === 1 ? 'Basic' : 'Free')
      : (user.tier || 'Free');

    if (tierPillEl) {
      tierPillEl.textContent = tierName === 'SuperAdmin' ? '👑 Super' : tierName === 'Premium' ? '⚡ Premium' : tierName === 'Basic' ? '⭐ Basic' : '🆓 Free';
      tierPillEl.className = `tier-badge-pill tier-${tierName.toLowerCase()}`;
    }

    if (quotaPreviewEl) {
      quotaPreviewEl.textContent = tierName;
    }

    const isAdmin = user.role === 'Admin' || user.role === 'SuperAdmin' || user.role === 1 || user.role === 2;
    if (adminMenuItem) {
      adminMenuItem.style.display = isAdmin ? 'flex' : 'none';
    }
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

  eventBus.on('auth:success', async (user) => {
    updateUserUI(user);
    await Promise.all([
      dailyHud.refresh(),
      analyticsChart.refresh(),
      progressModal.refresh()
    ]);
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

  // Initial Auth Check & Data Load
  const currentUser = await authService.getCurrentUser();
  if (!currentUser || !currentUser.isEmailVerified) {
    updateUserUI(null);
    authGate.show('signin');
  } else {
    updateUserUI(currentUser);
    await Promise.all([
      dailyHud.refresh(),
      analyticsChart.refresh(),
      progressModal.refresh()
    ]);
  }
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initApp);
} else {
  initApp();
}

