/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

import { ChartRenderer } from './ChartRenderer.js';
import { MealDiaryView } from './MealDiaryView.js';
import { ExcelExportService } from '../services/ExcelExportService.js';

/**
 * AnalyticsChartController
 * Coordinating UI controller / facade adhering strictly to the Single Responsibility Principle (SRP).
 * Delegates rendering and export to focused sub-services:
 * - ChartRenderer: SVG bar chart & timeline coordinate math
 * - MealDiaryView: Card / Grid history rendering & Obsidian Dark delete modal
 * - ExcelExportService: CSV / XLSX RFC 4180 serialization & tier entitlement gating
 */
export class AnalyticsChartController {
  /**
   * @param {Object} options
   * @param {import('../services/analytics-service.js').AnalyticsService} options.analyticsService
   * @param {import('../services/meals-service.js').MealsService} [options.mealsService]
   * @param {import('../services/api-client.js').ApiClient} [options.apiClient]
   * @param {import('./toast.js').ToastService} [options.toastService]
   * @param {import('../services/auth-service.js').AuthService} [options.authService]
   * @param {import('../core/state.js').AppState} options.appState
   * @param {import('../core/event-bus.js').EventBus} options.eventBus
   * @param {ExcelExportService} [options.excelExportService]
   */
  constructor({ analyticsService, mealsService, apiClient, toastService, authService, appState, eventBus, excelExportService }) {
    this._analytics = analyticsService;
    this._meals = mealsService;
    this._apiClient = apiClient;
    this._toast = toastService;
    this._authService = authService;
    this._state = appState;
    this._bus = eventBus;

    this._exportService = excelExportService || new ExcelExportService({
      authService,
      toastService,
      mealsService
    });

    this._currentMeals = [];
    this._selectedBarFilter = null;

    this._initSubComponents();
    this._bindEvents();

    // Event bus subscriptions
    this._bus.on('meal:logged', () => this.refresh());
    this._bus.on('profile:updated', () => this.refresh());
    this._bus.on('analytics:switch-period', (period) => this.switchPeriod(period));
  }

  get elements() {
    return {
      barChartContainer: document.getElementById('bar-chart-container'),
      statTotalDeficit: document.getElementById('stat-total-deficit'),
      statProjectedWeight: document.getElementById('stat-projected-weight'),
      statProteinCompliance: document.getElementById('stat-protein-compliance'),
      periodTitle: document.getElementById('stat-period-title'),
      mealLogSection: document.getElementById('meal-log-section'),
      mealLogCountBadge: document.getElementById('meal-log-count-badge'),
      mealLogFilterSubtitle: document.getElementById('meal-log-filter-subtitle'),
      mealLogClearFilter: document.getElementById('meal-log-clear-filter'),
      mealLogClearPeriod: document.getElementById('meal-log-clear-period'),
      btnViewCards: document.getElementById('btn-view-cards'),
      btnViewGrid: document.getElementById('btn-view-grid'),
      btnExportExcel: document.getElementById('btn-export-excel'),
      cardsContainer: document.getElementById('meal-log-cards-container'),
      gridContainer: document.getElementById('meal-log-grid-container'),
      gridTbody: document.getElementById('meal-grid-tbody'),
      gridTfoot: document.getElementById('meal-grid-tfoot'),
      emptyState: document.getElementById('meal-log-empty-state')
    };
  }

  _initSubComponents() {
    const el = this.elements;

    this._chartRenderer = new ChartRenderer({
      container: el.barChartContainer,
      onBarClick: (label, period) => this._onChartBarClicked(label, period)
    });

    this._diaryView = new MealDiaryView({
      cardsContainer: el.cardsContainer,
      gridContainer: el.gridContainer,
      gridTbody: el.gridTbody,
      gridTfoot: el.gridTfoot,
      emptyState: el.emptyState,
      countBadge: el.mealLogCountBadge,
      filterSubtitle: el.mealLogFilterSubtitle,
      clearFilterBtn: el.mealLogClearFilter,
      onEditMeal: (meal) => this._bus.emit('meal:edit', meal),
      onDeleteMeal: (meal, btn) => this._handleDeleteMeal(meal, btn)
    });
  }

  _bindEvents() {
    // Tab switching for periods
    document.querySelectorAll('.tab-btn').forEach(btn => {
      btn.addEventListener('click', () => {
        const period = btn.dataset.period;
        this._selectedBarFilter = null;
        this.switchPeriod(period);
      });
    });

    const el = this.elements;
    if (el.btnViewCards) el.btnViewCards.addEventListener('click', () => this.setView('cards'));
    if (el.btnViewGrid) el.btnViewGrid.addEventListener('click', () => this.setView('grid'));

    if (el.mealLogClearFilter) {
      el.mealLogClearFilter.addEventListener('click', () => {
        this._selectedBarFilter = null;
        this._chartRenderer.clearSelection();
        const period = this._state.activePeriod || '7D';
        this.loadMeals(period);
      });
    }

    if (el.btnExportExcel) {
      el.btnExportExcel.addEventListener('click', () => this.exportToExcel());
    }
  }

  setView(view) {
    const el = this.elements;
    if (el.btnViewCards) el.btnViewCards.classList.toggle('active', view === 'cards');
    if (el.btnViewGrid) el.btnViewGrid.classList.toggle('active', view === 'grid');
    this._diaryView.setView(view);
  }

  async switchPeriod(period) {
    document.querySelectorAll('.tab-btn').forEach(b => {
      if (b.dataset.period === period) b.classList.add('active');
      else b.classList.remove('active');
    });

    this._state.activePeriod = period;
    this._selectedBarFilter = null;

    if (this._apiClient) {
      try {
        const dashboard = await this._apiClient.getWebDashboard(period);
        if (dashboard && dashboard.projections) {
          this.hydrate(dashboard.projections, dashboard.recentMeals, period);
          return;
        }
      } catch (err) {
        console.warn('[AnalyticsChartController] Single-roundtrip period switch fallback:', err);
      }
    }

    await this.refresh();
  }

  renderProjections(data, period = '7D') {
    const el = this.elements;
    if (el.periodTitle) {
      const titles = { '1D': 'Daily', '7D': '7-Day', '30D': '30-Day', '90D': 'Quarterly (90D)', '365D': 'Yearly (1Y)' };
      el.periodTitle.textContent = titles[period] || period;
    }
    if (el.mealLogClearPeriod) el.mealLogClearPeriod.textContent = period;

    if (!data) return;

    if (el.statTotalDeficit) el.statTotalDeficit.textContent = `${Math.round(data.totalDeficitKcal || 0).toLocaleString()} kcal`;
    if (el.statProjectedWeight) el.statProjectedWeight.textContent = `${data.projectedWeightLossKg || 0} kg`;
    if (el.statProteinCompliance) el.statProteinCompliance.textContent = `${Math.round(data.proteinCompliancePercent || 0)}%`;

    this._chartRenderer.render(data.dailyTrends || [], period);
  }

  hydrate(projections, recentMeals = null, period = '7D') {
    this.renderProjections(projections, period);
    if (recentMeals) {
      this._currentMeals = recentMeals;
      this._diaryView.render(recentMeals, period);
    }
    this._updateExportButtonState();
  }

  async refresh(preloadedProjections = null, preloadedMeals = null) {
    const period = this._state.activePeriod || '7D';
    if (preloadedProjections) {
      this.hydrate(preloadedProjections, preloadedMeals, period);
      return;
    }
    try {
      const data = await this._analytics.getProjections(this._state.userId, period);
      this.renderProjections(data, period);
      await this.loadMeals(period, this._selectedBarFilter);
    } catch (err) {
      console.error('[AnalyticsChartController] Failed to load projections:', err);
    }
  }

  async _onChartBarClicked(label, period) {
    if (!label) {
      this._selectedBarFilter = null;
      await this.loadMeals(period, null);
      return;
    }

    const filterDate = period === '1D' ? null : ChartRenderer.parseLabelToDate(label);
    const filterMealType = period === '1D' ? label : null;

    this._selectedBarFilter = { label, date: filterDate, mealType: filterMealType };
    await this.loadMeals(period, this._selectedBarFilter);
  }

  async handleBarClick(label, barEl, period) {
    this._chartRenderer._handleBarClick(label, barEl, period);
  }

  async loadMeals(period = '7D', filter = null) {
    if (!this._meals) return;
    try {
      const response = await this._meals.getMealHistory(
        this._state.userId,
        period,
        filter?.date || null,
        filter?.mealType || null
      );

      let meals = (response && response.meals) ? response.meals : [];

      if (filter && filter.label && !filter.date && !filter.mealType) {
        const norm = filter.label.toLowerCase();
        meals = meals.filter(m => {
          const d = new Date(m.loggedAt || m.LoggedAt);
          const dateStr = d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' }).toLowerCase();
          const monthStr = d.toLocaleDateString('en-US', { month: 'short', year: 'numeric' }).toLowerCase();
          return dateStr.includes(norm) || monthStr.includes(norm);
        });
      }

      this._currentMeals = meals;
      this._diaryView.render(meals, period, filter);
      this._updateExportButtonState();
    } catch (err) {
      console.error('[AnalyticsChartController] Failed to load meal history:', err);
    }
  }

  renderMealData(meals) {
    const period = this._state.activePeriod || '7D';
    this._diaryView.render(meals, period, this._selectedBarFilter);
    this._updateExportButtonState();
  }

  renderCards(meals) {
    this._diaryView.renderCards(meals);
  }

  renderGrid(meals) {
    this._diaryView.renderGrid(meals);
  }

  async _handleDeleteMeal(meal, btn) {
    const dishName = meal.dishName || meal.DishName || 'this meal';
    btn.disabled = true;
    const originalContent = btn.innerHTML;
    btn.innerHTML = '⏳';

    try {
      await this._meals.deleteMeal(meal.id);
      if (this._toast) {
        this._toast.show({
          title: 'Meal Deleted 🗑️',
          message: `Deleted "${dishName}". Your daily ledger and charts have been recalculated!`
        });
      }
      this._bus.emit('meal:logged');
      const period = this._state.activePeriod || '7D';
      await this.loadMeals(period, this._selectedBarFilter);
    } catch (err) {
      btn.disabled = false;
      btn.innerHTML = originalContent;
      if (this._toast) {
        this._toast.show({
          title: 'Error Deleting Meal',
          message: err.message || 'Failed to delete meal.'
        });
      }
    }
  }

  _updateExportButtonState() {
    const el = this.elements;
    if (!el.btnExportExcel) return;
    const isAllowed = this._exportService.isExportAllowed();

    if (!isAllowed) {
      el.btnExportExcel.disabled = true;
      el.btnExportExcel.classList.add('disabled');
      el.btnExportExcel.style.opacity = '0.5';
      el.btnExportExcel.style.cursor = 'not-allowed';
      el.btnExportExcel.title = 'Exporting meal history (Excel / CSV) is a Premium tier feature. Please upgrade your plan.';
    } else {
      el.btnExportExcel.disabled = false;
      el.btnExportExcel.classList.remove('disabled');
      el.btnExportExcel.style.opacity = '';
      el.btnExportExcel.style.cursor = '';
      el.btnExportExcel.title = 'Export all meal & nutrition data to Microsoft Excel';
    }
  }

  exportToExcel() {
    const period = this._state.activePeriod || '7D';
    this._exportService.export(this._currentMeals, period, this._state.userId);
  }
}
