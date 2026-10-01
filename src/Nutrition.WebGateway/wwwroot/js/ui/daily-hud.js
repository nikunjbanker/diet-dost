/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
/**
 * DailyHudController
 * Manages the Daily Calorie Balance, Macro gauges, Health Score, and Companion feedback.
 */
export class DailyHudController {
  /**
   * @param {Object} options
   * @param {import('../services/analytics-service.js').AnalyticsService} options.analyticsService
   * @param {import('../core/state.js').AppState} options.appState
   * @param {import('../core/event-bus.js').EventBus} options.eventBus
   */
  constructor({ analyticsService, appState, eventBus }) {
    this._analytics = analyticsService;
    this._state = appState;
    this._bus = eventBus;

    // Listen to global changes
    this._bus.on('meal:logged', () => this.refresh());
    this._bus.on('profile:updated', () => this.refresh());
  }

  get elements() {
    return {
      consumed: document.getElementById('val-consumed'),
      budget: document.getElementById('val-budget'),
      remaining: document.getElementById('val-remaining'),
      progressFill: document.getElementById('calorie-progress-fill'),
      dostSpeech: document.getElementById('companion-speech'),
      streakCounter: document.getElementById('streak-counter'),
      scoreCircle: document.getElementById('score-circle-val'),
      badges: document.getElementById('badges-container'),
      proteinVal: document.getElementById('macro-protein-val'),
      proteinTarget: document.getElementById('macro-protein-target'),
      meterProtein: document.getElementById('meter-protein'),
      carbsVal: document.getElementById('macro-carbs-val'),
      carbsTarget: document.getElementById('macro-carbs-target'),
      meterCarbs: document.getElementById('meter-carbs'),
      fatVal: document.getElementById('macro-fat-val'),
      fatTarget: document.getElementById('macro-fat-target'),
      meterFat: document.getElementById('meter-fat'),
      fiberVal: document.getElementById('macro-fiber-val'),
      fiberTarget: document.getElementById('macro-fiber-target'),
      meterFiber: document.getElementById('meter-fiber')
    };
  }

  /**
   * Synchronously hydrates HUD elements from preloaded ledger data without network roundtrip.
   * @param {Object} ledger
   */
  hydrate(ledger) {
    if (!ledger) return;
    const el = this.elements;

    if (el.consumed) el.consumed.textContent = Math.round(ledger.consumedCalories || 0).toLocaleString();
    if (el.budget) el.budget.textContent = Math.round(ledger.budgetedCalories || 0).toLocaleString();
    if (el.remaining) el.remaining.textContent = Math.round(ledger.pendingCalories || 0).toLocaleString();

    if (el.remaining && el.progressFill) {
      if ((ledger.consumedCalories || 0) > (ledger.budgetedCalories || 0)) {
        el.remaining.classList.add('over');
        el.progressFill.className = 'progress-bar-fill over';
      } else if ((ledger.pendingCalories || 0) <= 200) {
        el.remaining.classList.remove('over');
        el.progressFill.className = 'progress-bar-fill warning';
      } else {
        el.remaining.classList.remove('over');
        el.progressFill.className = 'progress-bar-fill';
      }

      const budget = Math.max(1, ledger.budgetedCalories || 1600);
      const pct = Math.min(100, Math.round(((ledger.consumedCalories || 0) / budget) * 100));
      el.progressFill.style.width = `${pct}%`;
    }

    // Macro gauges
    if (el.proteinVal) el.proteinVal.textContent = `${Math.round(ledger.consumedProteinGrams || 0)}g`;
    if (el.proteinTarget) el.proteinTarget.textContent = `/ ${Math.round(ledger.targetProteinGrams || 0)}g`;
    if (el.meterProtein) {
      const tgt = Math.max(1, ledger.targetProteinGrams || 1);
      el.meterProtein.style.width = `${Math.min(100, ((ledger.consumedProteinGrams || 0) / tgt) * 100)}%`;
    }

    if (el.carbsVal) el.carbsVal.textContent = `${Math.round(ledger.consumedCarbsGrams || 0)}g`;
    if (el.carbsTarget) el.carbsTarget.textContent = `/ ${Math.round(ledger.targetCarbsGrams || 0)}g`;
    if (el.meterCarbs) {
      const tgt = Math.max(1, ledger.targetCarbsGrams || 1);
      el.meterCarbs.style.width = `${Math.min(100, ((ledger.consumedCarbsGrams || 0) / tgt) * 100)}%`;
    }

    if (el.fatVal) el.fatVal.textContent = `${Math.round(ledger.consumedFatGrams || 0)}g`;
    if (el.fatTarget) el.fatTarget.textContent = `/ ${Math.round(ledger.targetFatGrams || 0)}g`;
    if (el.meterFat) {
      const tgt = Math.max(1, ledger.targetFatGrams || 1);
      el.meterFat.style.width = `${Math.min(100, ((ledger.consumedFatGrams || 0) / tgt) * 100)}%`;
    }

    if (el.fiberVal) el.fiberVal.textContent = `${Math.round(ledger.consumedFiberGrams || 0)}g`;
    if (el.fiberTarget) el.fiberTarget.textContent = `/ ${Math.round(ledger.targetFiberGrams || 0)}g`;
    if (el.meterFiber) {
      const tgt = Math.max(1, ledger.targetFiberGrams || 1);
      el.meterFiber.style.width = `${Math.min(100, ((ledger.consumedFiberGrams || 0) / tgt) * 100)}%`;
    }

    // Health Score & Speech
    if (el.scoreCircle) el.scoreCircle.textContent = ledger.healthScore ?? 85;
    if (el.dostSpeech) el.dostSpeech.textContent = ledger.dostMessage || "Namaste! I am Diet Dost, your AI clinical nutrition companion.";
    if (el.streakCounter) el.streakCounter.textContent = `🔥 ${ledger.consistencyStreakDays || 0}-Day Consistency Streak`;

    // Earned badges
    if (el.badges && ledger.earnedBadges && ledger.earnedBadges.length > 0) {
      el.badges.innerHTML = ledger.earnedBadges.map(b => `<span class="badge-chip">${b}</span>`).join('');
    }
  }

  /**
   * Fetch daily ledger and update HUD meters and figures.
   * If preloadedLedger is provided, hydrates synchronously with 0 network calls.
   * @param {Object} [preloadedLedger]
   */
  async refresh(preloadedLedger = null) {
    if (preloadedLedger) {
      this.hydrate(preloadedLedger);
      return;
    }
    try {
      const ledger = await this._analytics.getDailyLedger(this._state.userId);
      this.hydrate(ledger);
    } catch (err) {
      console.error('[DailyHudController] Failed to refresh ledger:', err);
    }
  }
}
