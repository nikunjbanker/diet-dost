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
   * Fetch daily ledger and update HUD meters and figures.
   */
  async refresh() {
    try {
      const el = this.elements;
      const ledger = await this._analytics.getDailyLedger(this._state.userId);
      if (!ledger) return;

      if (el.consumed) el.consumed.textContent = Math.round(ledger.consumedCalories).toLocaleString();
      if (el.budget) el.budget.textContent = Math.round(ledger.budgetedCalories).toLocaleString();
      if (el.remaining) el.remaining.textContent = Math.round(ledger.pendingCalories).toLocaleString();

      if (el.remaining && el.progressFill) {
        if (ledger.consumedCalories > ledger.budgetedCalories) {
          el.remaining.classList.add('over');
          el.progressFill.className = 'progress-bar-fill over';
        } else if (ledger.pendingCalories <= 200) {
          el.remaining.classList.remove('over');
          el.progressFill.className = 'progress-bar-fill warning';
        } else {
          el.remaining.classList.remove('over');
          el.progressFill.className = 'progress-bar-fill';
        }

        const pct = Math.min(100, Math.round((ledger.consumedCalories / ledger.budgetedCalories) * 100));
        el.progressFill.style.width = `${pct}%`;
      }

      // Macro gauges
      if (el.proteinVal) el.proteinVal.textContent = `${Math.round(ledger.consumedProteinGrams)}g`;
      if (el.proteinTarget) el.proteinTarget.textContent = `/ ${Math.round(ledger.targetProteinGrams)}g`;
      if (el.meterProtein) {
        el.meterProtein.style.width = `${Math.min(100, (ledger.consumedProteinGrams / ledger.targetProteinGrams) * 100)}%`;
      }

      if (el.carbsVal) el.carbsVal.textContent = `${Math.round(ledger.consumedCarbsGrams)}g`;
      if (el.carbsTarget) el.carbsTarget.textContent = `/ ${Math.round(ledger.targetCarbsGrams)}g`;
      if (el.meterCarbs) {
        el.meterCarbs.style.width = `${Math.min(100, (ledger.consumedCarbsGrams / ledger.targetCarbsGrams) * 100)}%`;
      }

      if (el.fatVal) el.fatVal.textContent = `${Math.round(ledger.consumedFatGrams)}g`;
      if (el.fatTarget) el.fatTarget.textContent = `/ ${Math.round(ledger.targetFatGrams)}g`;
      if (el.meterFat) {
        el.meterFat.style.width = `${Math.min(100, (ledger.consumedFatGrams / ledger.targetFatGrams) * 100)}%`;
      }

      if (el.fiberVal) el.fiberVal.textContent = `${Math.round(ledger.consumedFiberGrams)}g`;
      if (el.fiberTarget) el.fiberTarget.textContent = `/ ${Math.round(ledger.targetFiberGrams)}g`;
      if (el.meterFiber) {
        el.meterFiber.style.width = `${Math.min(100, (ledger.consumedFiberGrams / ledger.targetFiberGrams) * 100)}%`;
      }

      // Health Score & Speech
      if (el.scoreCircle) el.scoreCircle.textContent = ledger.healthScore;
      if (el.dostSpeech) el.dostSpeech.textContent = ledger.dostMessage;
      if (el.streakCounter) el.streakCounter.textContent = `🔥 ${ledger.consistencyStreakDays}-Day Consistency Streak`;

      // Earned badges
      if (el.badges && ledger.earnedBadges && ledger.earnedBadges.length > 0) {
        el.badges.innerHTML = ledger.earnedBadges.map(b => `<span class="badge-chip">${b}</span>`).join('');
      }
    } catch (err) {
      console.error('[DailyHudController] Failed to refresh ledger:', err);
    }
  }
}
