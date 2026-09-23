/**
 * Main Application Composition Root
 * Bootstraps the Dependency Injection container, instantiates services,
 * initializes UI controllers, and sets up window facades for legacy HTML compatibility.
 */
import { container } from './core/di-container.js';
import { eventBus } from './core/event-bus.js';
import { appState } from './core/state.js';

import { ApiClient, apiClient } from './services/api-client.js?v=1.3.6';
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
  appState: c.resolve('appState'),
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
  const dailyHud = container.resolve('dailyHud');
  const mealLogger = container.resolve('mealLogger');
  const reviewModal = container.resolve('reviewModal');
  const analyticsChart = container.resolve('analyticsChart');
  const profileModal = container.resolve('profileModal');
  const transparencyModal = container.resolve('transparencyModal');
  const progressModal = container.resolve('progressModal');

  // ============================================================================
  // 3. Global Window Facades (for HTML onclick & inline attribute compatibility)
  // ============================================================================
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

  // Initial Data Load
  await Promise.all([
    dailyHud.refresh(),
    analyticsChart.refresh(),
    progressModal.refresh()
  ]);
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initApp);
} else {
  initApp();
}

