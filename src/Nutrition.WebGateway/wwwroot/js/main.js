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

import { apiClient } from './services/api.js?v=1.9.3';
import { AuthService } from './services/auth-service.js?v=1.9.3';
import { AdminService } from './services/admin-service.js?v=1.9.3';
import { MealsService } from './services/meals-service.js?v=1.9.3';
import { ProfileService } from './services/profile-service.js?v=1.9.3';
import { AnalyticsService } from './services/analytics-service.js?v=1.9.3';
import { ProgressPhotosService } from './services/progress-service.js?v=1.9.3';
import { MedicationService } from './services/medication-service.js?v=1.9.3';

import { toastService } from './ui/toast.js?v=1.9.3';
import { confettiService } from './ui/confetti.js?v=1.9.3';
import { DailyHudController } from './ui/daily-hud.js?v=1.9.3';
import { MealLoggerController } from './ui/meal-logger.js?v=1.9.3';
import { ReviewModalController } from './ui/review-modal.js?v=1.9.3';
import { AnalyticsChartController } from './ui/analytics-chart.js?v=1.9.3';
import { ExcelExportService } from './services/ExcelExportService.js?v=1.9.3';
import { ProfileModalController } from './ui/profile-modal.js?v=1.9.3';
import { TransparencyModalController } from './ui/transparency-modal.js?v=1.9.3';
import { ProgressModalController } from './ui/progress-modal.js?v=1.9.3';
import { AuthGateController } from './ui/auth-gate.js?v=1.9.3';
import { AdminModalController } from './ui/admin-modal.js?v=1.9.3';
import { QuotaModalController } from './ui/quota-modal.js?v=1.9.3';
import { CameraService } from './services/camera-service.js?v=1.9.3';
import { CameraModalController } from './ui/camera-modal.js?v=1.9.3';

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
  cameraModal: c.resolve('cameraModal'),
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
  cameraModal: c.resolve('cameraModal'),
  eventBus: c.resolve('eventBus')
}));

container.register('authGate', (c) => new AuthGateController({
  authService: c.resolve('authService'),
  toastService: c.resolve('toastService'),
  eventBus: c.resolve('eventBus')
}));

container.register('adminModal', (c) => new AdminModalController({
  adminService: c.resolve('adminService'),
  authService: c.resolve('authService'),
  toastService: c.resolve('toastService'),
  eventBus: c.resolve('eventBus')
}));

container.register('quotaModal', (c) => new QuotaModalController({
  apiClient: c.resolve('apiClient'),
  toastService: c.resolve('toastService'),
  eventBus: c.resolve('eventBus')
}));

container.register('cameraService', () => new CameraService());
container.register('cameraModal', (c) => new CameraModalController({
  cameraService: c.resolve('cameraService'),
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
// 2. Diwali (Deepavali) Festival Celebration Carousel, Panch-Deep & Calibrator
// ============================================================================
function setupDiwaliBanner(toastService, confettiService) {
  const banner = document.getElementById('diwali-celebration-banner');
  if (!banner) return;

  const tickerBar = document.getElementById('diwali-ticker-bar');
  const carouselWrapper = document.getElementById('diwali-carousel-wrapper');
  const btnMinimize = document.getElementById('btn-diwali-minimize');
  const btnExpand = document.getElementById('btn-diwali-expand');
  const tabBtns = banner.querySelectorAll('.diwali-tab-btn');
  const slides = banner.querySelectorAll('.diwali-slide');
  const dotBtns = banner.querySelectorAll('.diwali-dot');
  const prevBtn = document.getElementById('btn-diwali-prev');
  const nextBtn = document.getElementById('btn-diwali-next');
  const pledgeBtn = document.getElementById('btn-diwali-pledge');

  let currentSlide = 0;
  const totalSlides = slides.length;
  let autoTimer = null;
  let isHovered = false;

  // Restore saved minimize preference
  const isMinimized = localStorage.getItem('diet_dost_diwali_minimized') === 'true';
  if (isMinimized && tickerBar && carouselWrapper) {
    carouselWrapper.style.display = 'none';
    tickerBar.style.display = 'flex';
  }

  function goToSlide(index) {
    if (totalSlides === 0) return;
    if (index < 0) index = totalSlides - 1;
    if (index >= totalSlides) index = 0;
    currentSlide = index;

    slides.forEach((s, i) => s.classList.toggle('active', i === currentSlide));
    tabBtns.forEach((t, i) => {
      const active = i === currentSlide;
      t.classList.toggle('active', active);
      t.setAttribute('aria-selected', active ? 'true' : 'false');
    });
    dotBtns.forEach((d, i) => d.classList.toggle('active', i === currentSlide));
  }

  tabBtns.forEach((tab) => {
    tab.addEventListener('click', () => {
      const idx = parseInt(tab.getAttribute('data-slide') || '0', 10);
      goToSlide(idx);
    });
  });

  dotBtns.forEach((dot) => {
    dot.addEventListener('click', () => {
      const idx = parseInt(dot.getAttribute('data-slide') || '0', 10);
      goToSlide(idx);
    });
  });

  if (prevBtn) prevBtn.addEventListener('click', () => goToSlide(currentSlide - 1));
  if (nextBtn) nextBtn.addEventListener('click', () => goToSlide(currentSlide + 1));

  if (btnMinimize && tickerBar && carouselWrapper) {
    btnMinimize.addEventListener('click', () => {
      carouselWrapper.style.display = 'none';
      tickerBar.style.display = 'flex';
      localStorage.setItem('diet_dost_diwali_minimized', 'true');
    });
  }

  if (btnExpand && tickerBar && carouselWrapper) {
    btnExpand.addEventListener('click', () => {
      tickerBar.style.display = 'none';
      carouselWrapper.style.display = 'block';
      localStorage.setItem('diet_dost_diwali_minimized', 'false');
    });
  }

  // Auto-advance every 7.5 seconds when visible and not hovered
  function startAutoAdvance() {
    if (autoTimer) clearInterval(autoTimer);
    autoTimer = setInterval(() => {
      if (!isHovered && carouselWrapper && carouselWrapper.style.display !== 'none') {
        goToSlide(currentSlide + 1);
      }
    }, 7500);
  }

  banner.addEventListener('mouseenter', () => { isHovered = true; });
  banner.addEventListener('mouseleave', () => { isHovered = false; });
  startAutoAdvance();

  // --------------------------------------------------------------------------
  // 2.1 Interactive Panch-Deep 5-Day Diya Lighting Controller
  // --------------------------------------------------------------------------
  const diyaWisdom = [
    { name: "Dhanteras", toast: "✨ Dhanteras / Dhanvantari Jayanti: Invest in the eternal wealth of ICMR-NIN 2024 balanced nutrition!" },
    { name: "Kali Chaudas", toast: "🛡️ Naraka Chaturdashi / Kali Chaudas: Eliminate metabolic fatigue and sluggishness with joyful, clean foods!" },
    { name: "Deepavali", toast: "🪔 Deepavali Lakshmi Puja: May the divine light of Deepavali illuminate your home with health and joy!" },
    { name: "Bestu Varas", toast: "🌅 સાલ મુબારક & Bestu Varas: Fresh health resolutions and Annakut balance for Nutan Varsh!" },
    { name: "Bhai Dooj", toast: "🍯 Bhai Dooj: Cherish family bonds and savor festive sweet moments with mindful portion balance!" }
  ];

  let litDiyas = [];
  try {
    const saved = localStorage.getItem('diet_dost_diwali_diyas_lit');
    if (saved) litDiyas = JSON.parse(saved);
    if (!Array.isArray(litDiyas)) litDiyas = [];
  } catch (e) {
    litDiyas = [];
  }

  const diyaBtns = banner.querySelectorAll('.diwali-diya-btn');
  const statusEl = document.getElementById('panch-deep-status');

  function updateDiyaUI() {
    diyaBtns.forEach((btn) => {
      const idx = parseInt(btn.getAttribute('data-diya') || '0', 10);
      const isLit = litDiyas.includes(idx);
      btn.classList.toggle('lit', isLit);
      btn.setAttribute('aria-pressed', isLit ? 'true' : 'false');
    });

    if (statusEl) {
      if (litDiyas.length === 5) {
        statusEl.textContent = '🌟 All 5 Deepotsav Lamps Lit — Arogya Jyoti Activated!';
        statusEl.style.color = '#fef08a';
      } else {
        statusEl.textContent = `${litDiyas.length} of 5 Diyas Lit`;
        statusEl.style.color = '#cbd5e1';
      }
    }
  }

  updateDiyaUI();

  diyaBtns.forEach((btn) => {
    btn.addEventListener('click', () => {
      const idx = parseInt(btn.getAttribute('data-diya') || '0', 10);
      const alreadyLit = litDiyas.includes(idx);

      if (!alreadyLit) {
        litDiyas.push(idx);
        localStorage.setItem('diet_dost_diwali_diyas_lit', JSON.stringify(litDiyas));
        updateDiyaUI();
        confettiService?.burst?.();

        const info = diyaWisdom[idx] || { name: 'Diwali', toast: 'Happy Deepavali from Diet-Dost!' };
        toastService?.show?.({
          type: 'success',
          title: `🪔 ${info.name} Jyoti Lit!`,
          message: info.toast,
          icon: '✨'
        });

        if (litDiyas.length === 5) {
          setTimeout(() => {
            confettiService?.burst?.();
            toastService?.show?.({
              type: 'success',
              title: '🌟 Deepotsav Panch-Deep Complete!',
              message: 'All 5 auspicious lamps are illuminated! May your year shine with lifelong health, vitality, and happiness.',
              icon: '🪔'
            });
          }, 350);
        }
      } else {
        // Toggle off if already lit to allow playful interaction
        litDiyas = litDiyas.filter((d) => d !== idx);
        localStorage.setItem('diet_dost_diwali_diyas_lit', JSON.stringify(litDiyas));
        updateDiyaUI();
      }
    });
  });

  // --------------------------------------------------------------------------
  // 2.2 Smart Mithai & Farsan Calibrator Controller
  // --------------------------------------------------------------------------
  const mithaiData = {
    'kaju-katli': {
      badge: '✨ Kaju Katli Balance Tip',
      text: 'Rich in cashew heart-healthy fats and plant protein (~55 kcal/pc). Pair with warm spiced green tea and enjoy a brisk 12-minute post-puja stroll. Zero guilt, pure celebration!',
      prompt: '1 pc Kaju Katli'
    },
    'besan-laddoo': {
      badge: '🟡 Besan Laddoo Balance Tip',
      text: 'Gram flour provides fiber and protein (~180 kcal/pc). Savor slowly with family; a 20-minute celebratory walk or light evening meal balances daily energy perfectly.',
      prompt: '1 pc Besan Laddoo'
    },
    'crispy-chakli': {
      badge: '🌀 Crispy Chakli Balance Tip',
      text: 'Crunchy festive savory treat (~150 kcal for 2 pcs). Balance with a bowl of sprouted moong salad and hydration for optimal electrolyte and sodium harmony.',
      prompt: '2 pcs Crispy Chakli'
    },
    'ghughra-mathiya': {
      badge: '🥟 Ghughra / Mathiya Balance Tip',
      text: 'Traditional Gujarati festive delicacy (~140 kcal/pc). Enjoy mindfully as an afternoon treat alongside cardamom herbal tea. 100% joyful, zero crash diets!',
      prompt: '1 pc Ghughra'
    }
  };

  let activeMithaiKey = 'kaju-katli';
  const mithaiChips = banner.querySelectorAll('.mithai-chip');
  const mithaiBadge = document.getElementById('mithai-card-badge');
  const mithaiText = document.getElementById('mithai-card-text');
  const btnLogFestiveTreat = document.getElementById('btn-log-festive-treat');

  mithaiChips.forEach((chip) => {
    chip.addEventListener('click', () => {
      const sweetKey = chip.getAttribute('data-sweet');
      if (!sweetKey || !mithaiData[sweetKey]) return;

      activeMithaiKey = sweetKey;
      mithaiChips.forEach((c) => c.classList.toggle('active', c === chip));

      const entry = mithaiData[sweetKey];
      if (mithaiBadge) mithaiBadge.textContent = entry.badge;
      if (mithaiText) mithaiText.textContent = entry.text;
    });
  });

  if (btnLogFestiveTreat) {
    btnLogFestiveTreat.addEventListener('click', () => {
      const entry = mithaiData[activeMithaiKey] || mithaiData['kaju-katli'];
      const mealPrompt = document.getElementById('meal-input-prompt');
      if (mealPrompt) {
        mealPrompt.value = entry.prompt;
        mealPrompt.focus();
      } else {
        document.getElementById('btn-nav-log-meal')?.click();
      }

      toastService?.show?.({
        type: 'info',
        title: '⚡ Festive Treat Selected',
        message: `Ready to log "${entry.prompt}". Adjust serving or add companions!`,
        icon: '🍬'
      });
    });
  }

  // --------------------------------------------------------------------------
  // 2.3 Gujarati New Year (Bestu Varas / Saal Mubarak) Health Pledge
  // --------------------------------------------------------------------------
  if (pledgeBtn) {
    pledgeBtn.addEventListener('click', () => {
      confettiService?.burst?.();
      toastService?.show?.({
        type: 'success',
        title: '🌅 સાલ મુબારક • Nutan Varsh Sankalp!',
        message: 'Happy Diwali & Saal Mubarak from Nikunj Banker & Diet-Dost! May Vikram Samvat bring boundless vitality, health, and joyful nutrition.',
        icon: '🌟'
      });
    });
  }
}

// ============================================================================
// 2.1 Mobile Viewport Experience Advisory Strip Controller
// ============================================================================
function setupMobileViewportNotice() {
  const notice = document.getElementById('mobile-viewport-notice');
  if (!notice) return;

  const checkViewport = () => {
    const isDismissedNow = sessionStorage.getItem('diet_dost_mobile_notice_dismissed') === 'true';
    if (!isDismissedNow && window.innerWidth < 768) {
      notice.classList.add('is-visible');
      notice.style.display = 'flex';
    } else {
      notice.classList.remove('is-visible');
      notice.style.display = 'none';
    }
  };

  checkViewport();
  window.addEventListener('resize', checkViewport, { passive: true });

  const btnDismiss = document.getElementById('btn-dismiss-mobile-notice');
  btnDismiss?.addEventListener('click', () => {
    notice.classList.remove('is-visible');
    notice.style.display = 'none';
    sessionStorage.setItem('diet_dost_mobile_notice_dismissed', 'true');
  });
}

// ============================================================================
// 3. Application Bootstrap
// ============================================================================
async function initApp() {
  const splashStartTime = performance.now();
  const MIN_SPLASH_DURATION_MS = 750; // Smooth brand pulse & loading transition guarantee

  // Fire early database warm-up probe asynchronously in background
  // Wakes cold-paused serverless cloud databases (Azure SQL Free Tier) while HTML partials hydrate
  apiClient.warmupDatabase();

  // Load modular HTML partials into the DOM before initializing controllers
  await loadPartials();

  // Resolve and initialize all controllers (DOM is now guaranteed to be populated)
  const authService = container.resolve('authService');
  const authGate = container.resolve('authGate');
  const adminModal = container.resolve('adminModal');
  const quotaModal = container.resolve('quotaModal');

  const dailyHud = container.resolve('dailyHud');
  container.resolve('mealLogger');
  const reviewModal = container.resolve('reviewModal');
  const analyticsChart = container.resolve('analyticsChart');
  const profileModal = container.resolve('profileModal');
  const transparencyModal = container.resolve('transparencyModal');
  const progressModal = container.resolve('progressModal');
  const cameraModal = container.resolve('cameraModal');
  const toastService = container.resolve('toastService');
  const confettiService = container.resolve('confettiService');

  // Initialize Diwali festival celebration banner carousel, Panch-Deep & ticker
  setupDiwaliBanner(toastService, confettiService);

  // Initialize mobile viewport laptop recommendation advisory strip
  setupMobileViewportNotice();

  // Helper: Synchronize user header badges and visibility
  function updateUserUI(user, featureFlags = null, quota = null) {
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
      const profileSessionEmail = document.getElementById('profile-user-session-email');
      if (profileSessionEmail) profileSessionEmail.textContent = 'Not signed in';
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
    const profileSessionEmail = document.getElementById('profile-user-session-email');
    if (profileSessionEmail) profileSessionEmail.textContent = user.email || 'Signed in';

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
    if (userMenuDropdown && !userMenuDropdown.contains(e.target) && !userMenuBtn?.contains(e.target)) {
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

  const performSignOut = async () => {
    if (userMenuDropdown) userMenuDropdown.style.display = 'none';
    if (window.closeProfileModal) window.closeProfileModal();
    await authService.logout();
    updateUserUI(null);
    authGate.show('signin');
  };

  document.getElementById('menu-btn-signout')?.addEventListener('click', performSignOut);
  document.getElementById('btn-profile-signout')?.addEventListener('click', performSignOut);

  // Mobile Thumb-Zone Bottom Navigation Wiring
  const btnNavOverview = document.getElementById('btn-nav-overview');
  const btnNavLogMeal = document.getElementById('btn-nav-log-meal');
  const btnNavHistory = document.getElementById('btn-nav-history');
  const btnNavProfile = document.getElementById('btn-nav-profile');

  function setBottomNavActive(activeBtn) {
    document.querySelectorAll('.bottom-nav-item').forEach(b => b.classList.remove('active'));
    if (activeBtn) {
      activeBtn.classList.add('active');
      if (typeof navigator !== 'undefined' && typeof navigator.vibrate === 'function') {
        try { navigator.vibrate(10); } catch (_) { /* Ignore unsupported/blocked vibration */ }
      }
    }
  }

  btnNavOverview?.addEventListener('click', () => {
    setBottomNavActive(btnNavOverview);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  });

  btnNavLogMeal?.addEventListener('click', () => {
    setBottomNavActive(btnNavLogMeal);
    const loggerCard = document.querySelector('.logging-card');
    if (loggerCard) {
      loggerCard.scrollIntoView({ behavior: 'smooth', block: 'center' });
      const textInput = document.getElementById('text-input');
      const textLoggerBox = document.getElementById('text-logger-box');
      if (textLoggerBox && window.getComputedStyle(textLoggerBox).display !== 'none' && textInput) {
        textInput.focus();
      }
    }
  });

  btnNavHistory?.addEventListener('click', () => {
    setBottomNavActive(btnNavHistory);
    const historySection = document.getElementById('meal-log-section') || document.querySelector('.analytics-card');
    if (historySection) {
      historySection.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  });

  btnNavProfile?.addEventListener('click', () => {
    setBottomNavActive(btnNavProfile);
    profileModal.open();
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
  window.openCameraModal = (options) => cameraModal.open(options);
  window.closeCameraModal = () => cameraModal.close();
  window.openCropModal = (options) => cameraModal.openCrop(options);
  window.switchAnalyticsPeriod = (period) => analyticsChart.switchPeriod(period);
  window.quickAddSubzi = (name, kcal, p, c, f) => reviewModal.quickAddSubzi(name, kcal, p, c, f);
  window.updateItemName = (idx, name) => reviewModal.updateItemName(idx, name);
  window.deleteReviewItem = (idx) => reviewModal.deleteItem(idx);
  window.stepItemQuantity = (idx, delta) => reviewModal.stepItemQuantity(idx, delta);
  window.appendMedication = (med) => profileModal.appendMedication(med);
  window.clearMedications = () => profileModal.clearMedications();

  // Dismiss splash screen smoothly once the DOM and controllers are initialized
  async function dismissSplashScreen() {
    const splash = document.getElementById('app-loading-screen');
    if (!splash) return;
    const elapsed = performance.now() - splashStartTime;
    const remaining = Math.max(0, MIN_SPLASH_DURATION_MS - elapsed);
    if (remaining > 0) {
      await new Promise((resolve) => setTimeout(resolve, remaining));
    }
    splash.classList.add('splash-fade-out');
    setTimeout(() => {
      splash.remove();
    }, 550);
  }

  // Background Keep-Alive for Serverless Cloud Database (every 45m while tab is visible)
  let lastWarmupTime = Date.now();
  setInterval(() => {
    if (document.visibilityState === 'visible' && Date.now() - lastWarmupTime >= 45 * 60 * 1000) {
      lastWarmupTime = Date.now();
      apiClient.warmupDatabase();
    }
  }, 5 * 60 * 1000);

  // Interaction-triggered pre-warming: on auth gate engagement or active user touch
  const triggerInteractionWarmup = () => {
    if (Date.now() - lastWarmupTime >= 15 * 60 * 1000) {
      lastWarmupTime = Date.now();
      apiClient.warmupDatabase();
    }
  };
  document.addEventListener('pointerdown', triggerInteractionWarmup, { passive: true });
  document.addEventListener('keydown', triggerInteractionWarmup, { passive: true });

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
  } finally {
    await dismissSplashScreen();
  }
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initApp);
} else {
  initApp();
}

