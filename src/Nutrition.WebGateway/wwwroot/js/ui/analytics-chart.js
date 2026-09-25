/**
 * AnalyticsChartController
 * Manages periodic deficit projections, bar chart visualization (1D, 7D, 30D, 90D, 365D),
 * and interactive logged meal diary history with Card/Grid layouts and Excel export.
 */
export class AnalyticsChartController {
  /**
   * @param {Object} options
   * @param {import('../services/analytics-service.js').AnalyticsService} options.analyticsService
   * @param {import('../services/meals-service.js').MealsService} [options.mealsService]
   * @param {import('./toast.js').ToastService} [options.toastService]
   * @param {import('../services/auth-service.js').AuthService} [options.authService]
   * @param {import('../core/state.js').AppState} options.appState
   * @param {import('../core/event-bus.js').EventBus} options.eventBus
   */
  constructor({ analyticsService, mealsService, toastService, authService, appState, eventBus }) {
    this._analytics = analyticsService;
    this._meals = mealsService;
    this._toast = toastService;
    this._authService = authService;
    this._state = appState;
    this._bus = eventBus;

    this._currentView = 'cards'; // 'cards' | 'grid'
    this._selectedBarFilter = null; // { label: string, date: string|null, mealType: string|null }
    this._currentMeals = [];

    this._bindEvents();

    // Listen to global changes
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

      // Meal Log Diary Elements
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

  _bindEvents() {
    // Tab switching for periods
    document.querySelectorAll('.tab-btn').forEach(btn => {
      btn.addEventListener('click', () => {
        const period = btn.dataset.period;
        this._selectedBarFilter = null;
        this.switchPeriod(period);
      });
    });

    // View toggle switcher
    const el = this.elements;
    if (el.btnViewCards) {
      el.btnViewCards.addEventListener('click', () => this.setView('cards'));
    }
    if (el.btnViewGrid) {
      el.btnViewGrid.addEventListener('click', () => this.setView('grid'));
    }

    // Clear bar filter
    if (el.mealLogClearFilter) {
      el.mealLogClearFilter.addEventListener('click', () => {
        this._selectedBarFilter = null;
        document.querySelectorAll('.chart-bar.selected').forEach(b => b.classList.remove('selected'));
        const period = this._state.activePeriod || '7D';
        this.loadMeals(period);
      });
    }

    // Export to Excel
    if (el.btnExportExcel) {
      el.btnExportExcel.addEventListener('click', () => this.exportToExcel());
    }
  }

  /**
   * Set active view layout ('cards' | 'grid')
   * @param {'cards'|'grid'} view
   */
  setView(view) {
    this._currentView = view;
    const el = this.elements;

    if (el.btnViewCards) {
      el.btnViewCards.classList.toggle('active', view === 'cards');
    }
    if (el.btnViewGrid) {
      el.btnViewGrid.classList.toggle('active', view === 'grid');
    }

    if (el.cardsContainer && el.gridContainer) {
      if (view === 'cards') {
        el.cardsContainer.classList.remove('hidden');
        el.gridContainer.classList.add('hidden');
      } else {
        el.cardsContainer.classList.add('hidden');
        el.gridContainer.classList.remove('hidden');
      }
    }

    this.renderMealData(this._currentMeals);
  }

  /**
   * Switch the active analytics timeline period.
   * @param {'1D'|'7D'|'30D'|'90D'|'365D'} period
   */
  async switchPeriod(period) {
    document.querySelectorAll('.tab-btn').forEach(b => {
      if (b.dataset.period === period) b.classList.add('active');
      else b.classList.remove('active');
    });

    this._state.activePeriod = period;
    this._selectedBarFilter = null;
    await this.refresh();
  }

  /**
   * Fetch projection data, render bar chart, and load corresponding logged meals.
   */
  async refresh() {
    try {
      const period = this._state.activePeriod || '7D';
      const el = this.elements;

      if (el.periodTitle) {
        const titles = {
          '1D': 'Daily',
          '7D': '7-Day',
          '30D': '30-Day',
          '90D': 'Quarterly (90D)',
          '365D': 'Yearly (1Y)'
        };
        el.periodTitle.textContent = titles[period] || period;
      }

      if (el.mealLogClearPeriod) {
        el.mealLogClearPeriod.textContent = period;
      }

      const data = await this._analytics.getProjections(this._state.userId, period);
      if (data) {
        if (el.statTotalDeficit) {
          el.statTotalDeficit.textContent = `${Math.round(data.totalDeficitKcal).toLocaleString()} kcal`;
        }
        if (el.statProjectedWeight) {
          el.statProjectedWeight.textContent = `${data.projectedWeightLossKg} kg`;
        }
        if (el.statProteinCompliance) {
          el.statProteinCompliance.textContent = `${Math.round(data.proteinCompliancePercent)}%`;
        }

        if (el.barChartContainer && data.dailyTrends && data.dailyTrends.length > 0) {
          const maxKcal = Math.max(
            ...data.dailyTrends.map(d => Math.max(d.consumedCalories, d.budgetCalories)),
            2000
          );

          el.barChartContainer.innerHTML = data.dailyTrends.map((d, idx) => {
            const heightPct = Math.max(4, Math.round((d.consumedCalories / maxKcal) * 100));
            const color = d.consumedCalories > d.budgetCalories
              ? 'var(--status-rose)'
              : d.consumedCalories === 0
              ? 'rgba(255,255,255,0.06)'
              : 'var(--accent-brand)';
            const dateLabel = d.date || d.Date || '';

            return `
              <div class="chart-bar-group" data-index="${idx}" data-label="${dateLabel}">
                <div class="chart-bar" style="height: ${heightPct}%; background: ${color};" 
                     data-tooltip="${dateLabel}: ${Math.round(d.consumedCalories)} / ${Math.round(d.budgetCalories)} kcal"
                     data-label="${dateLabel}"></div>
                <div class="chart-label">${dateLabel}</div>
              </div>
            `;
          }).join('');

          // Wire interactive chart bar clicks
          el.barChartContainer.querySelectorAll('.chart-bar-group').forEach(group => {
            group.addEventListener('click', () => {
              const label = group.dataset.label;
              const bar = group.querySelector('.chart-bar');
              this.handleBarClick(label, bar, period);
            });
          });
        }
      }

      // Load meal history for current period
      await this.loadMeals(period, this._selectedBarFilter);
    } catch (err) {
      console.error('[AnalyticsChartController] Failed to load projections:', err);
    }
  }

  /**
   * Handle click on a bar in the chart to filter logged meals by that specific date/segment.
   * @param {string} label - e.g. "Breakfast", "Sep 15", "Oct 2025"
   * @param {HTMLElement} barEl
   * @param {string} period
   */
  async handleBarClick(label, barEl, period) {
    const isAlreadySelected = barEl.classList.contains('selected');

    // Remove selected state from all bars
    document.querySelectorAll('.chart-bar.selected').forEach(b => b.classList.remove('selected'));

    if (isAlreadySelected) {
      // Toggle off filter
      this._selectedBarFilter = null;
      await this.loadMeals(period, null);
    } else {
      // Toggle on filter
      barEl.classList.add('selected');

      // Determine filter parameters
      let filterDate = null;
      let filterMealType = null;

      if (period === '1D') {
        // Label is MealType (Breakfast, Lunch, Snack, Dinner)
        filterMealType = label;
      } else {
        // Label is date like "Sep 15" or "MMM dd"
        // Try parsing to date if possible, or filter client-side
        filterDate = this._parseLabelToDate(label);
      }

      this._selectedBarFilter = {
        label,
        date: filterDate,
        mealType: filterMealType
      };

      await this.loadMeals(period, this._selectedBarFilter);
    }
  }

  /**
   * Parse a chart label (e.g. "Sep 15") into an approximate ISO date string for filtering.
   * @param {string} label
   * @returns {string|null}
   */
  _parseLabelToDate(label) {
    if (!label) return null;
    const currentYear = new Date().getFullYear();
    const candidate = new Date(`${label}, ${currentYear}`);
    if (!isNaN(candidate.getTime())) {
      return candidate.toLocaleDateString('en-CA'); // 'YYYY-MM-DD'
    }
    return null;
  }

  /**
   * Load logged meals from API and render.
   * @param {string} period
   * @param {Object|null} [filter=null]
   */
  async loadMeals(period = '7D', filter = null) {
    const el = this.elements;
    if (!this._meals) return;

    try {
      const dateParam = filter?.date || null;
      const mealTypeParam = filter?.mealType || null;

      const response = await this._meals.getMealHistory(
        this._state.userId,
        period,
        dateParam,
        mealTypeParam
      );

      let meals = (response && response.meals) ? response.meals : [];

      // If filtering by a label that couldn't be parsed on backend (e.g. "Breakfast" or month string)
      if (filter && filter.label && !dateParam && !mealTypeParam) {
        const norm = filter.label.toLowerCase();
        meals = meals.filter(m => {
          const d = new Date(m.loggedAt || m.LoggedAt);
          const dateStr = d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' }).toLowerCase();
          const monthStr = d.toLocaleDateString('en-US', { month: 'short', year: 'numeric' }).toLowerCase();
          return dateStr.includes(norm) || monthStr.includes(norm);
        });
      }

      this._currentMeals = meals;

      // Update header badge and subtitle
      if (el.mealLogCountBadge) {
        el.mealLogCountBadge.textContent = `${meals.length} meal${meals.length === 1 ? '' : 's'}`;
      }

      if (el.mealLogFilterSubtitle) {
        if (filter) {
          el.mealLogFilterSubtitle.innerHTML = `Filtered by: <strong style="color: #38bdf8;">${filter.label}</strong>`;
        } else {
          const periodLabels = {
            '1D': "Today's",
            '7D': '7-Day',
            '30D': '30-Day',
            '90D': 'Quarterly (90D)',
            '365D': 'Yearly (1Y)'
          };
          el.mealLogFilterSubtitle.textContent = `Showing ${periodLabels[period] || period} meal history`;
        }
      }

      // Show/hide clear filter button
      if (el.mealLogClearFilter) {
        if (filter) el.mealLogClearFilter.classList.remove('hidden');
        else el.mealLogClearFilter.classList.add('hidden');
      }

      this.renderMealData(meals);
    } catch (err) {
      console.error('[AnalyticsChartController] Failed to load meal history:', err);
    }
  }

  /**
   * Render meals into currently active layout.
   * @param {Array<Object>} meals
   */
  renderMealData(meals) {
    const el = this.elements;
    const hasMeals = meals && meals.length > 0;

    if (el.emptyState) {
      if (hasMeals) el.emptyState.classList.add('hidden');
      else el.emptyState.classList.remove('hidden');
    }

    if (!hasMeals) {
      if (el.cardsContainer) el.cardsContainer.innerHTML = '';
      if (el.gridTbody) el.gridTbody.innerHTML = '';
      if (el.gridTfoot) el.gridTfoot.innerHTML = '';
      return;
    }

    if (this._currentView === 'cards') {
      this.renderCards(meals);
    } else {
      this.renderGrid(meals);
    }
  }

  /**
   * Render Obsidian Dark Meal Cards layout.
   * @param {Array<Object>} meals
   */
  renderCards(meals) {
    const el = this.elements;
    if (!el.cardsContainer) return;

    const mealTypeNames = ['Breakfast', 'Lunch', 'Snack', 'Dinner'];
    const mealTypeClasses = ['breakfast', 'lunch', 'snack', 'dinner'];
    const mealTypeIcons = ['🌅', '☀️', '☕', '🌙'];

    el.cardsContainer.innerHTML = meals.map(meal => {
      const typeIndex = typeof meal.mealType === 'number' ? meal.mealType : 1;
      const typeName = mealTypeNames[typeIndex] || meal.mealType || 'Meal';
      const typeClass = mealTypeClasses[typeIndex] || 'lunch';
      const typeIcon = mealTypeIcons[typeIndex] || '🍽️';

      const d = new Date(meal.loggedAt || meal.LoggedAt);
      const dateFormatted = !isNaN(d)
        ? d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
        : '';
      const timeFormatted = !isNaN(d)
        ? d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
        : '';

      const items = meal.items || meal.Items || [];
      const itemsHtml = items.map(item => `
        <span class="meal-item-tag" title="${item.hindiOrRegionalName || ''}">
          <strong>${item.name || item.Name}</strong> 
          <span style="color: var(--text-muted);">(${item.estimatedPortion || item.EstimatedPortion || '1 serving'})</span>
        </span>
      `).join('');

      const cals = Math.round(meal.totalCalories || 0);
      const protein = (meal.totalProteinGrams || 0).toFixed(1);
      const carbs = (meal.totalCarbsGrams || 0).toFixed(1);
      const fat = (meal.totalFatGrams || 0).toFixed(1);
      const fiber = (meal.totalFiberGrams || 0).toFixed(1);
      const sugar = (meal.totalSugarGrams || 0).toFixed(1);
      const sodium = Math.round(meal.totalSodiumMg || 0);

      const advice = meal.dietitianAdvice || meal.DietitianAdvice;
      const adviceHtml = advice ? `
        <div class="meal-card-advice">
          <strong>Dost Insight:</strong> ${advice}
        </div>
      ` : '';

      return `
        <div class="meal-card" data-meal-id="${meal.id}">
          <div class="meal-card-top">
            <span class="meal-card-type-badge ${typeClass}">
              <span>${typeIcon}</span> ${typeName}
            </span>
            <div class="meal-card-top-right">
              <span class="meal-card-time">${dateFormatted} · ${timeFormatted}</span>
              <div class="meal-card-actions">
                <button type="button" class="btn-card-action btn-edit-meal" data-meal-id="${meal.id}" title="Edit Meal">
                  <span class="action-icon">✏️</span> Edit
                </button>
                <button type="button" class="btn-card-action btn-delete-meal" data-meal-id="${meal.id}" title="Delete Meal">
                  <span class="action-icon">🗑️</span> Delete
                </button>
              </div>
            </div>
          </div>

          <div class="meal-card-dish">${meal.dishName || meal.DishName || 'Logged Meal'}</div>

          <div class="meal-card-macros">
            <span class="meal-macro-pill cals" title="Calories">🔥 ${cals} kcal</span>
            <span class="meal-macro-pill protein" title="Protein">💪 ${protein}g</span>
            <span class="meal-macro-pill carbs" title="Carbs">🌾 ${carbs}g</span>
            <span class="meal-macro-pill fat" title="Fat">🥑 ${fat}g</span>
            <span class="meal-macro-pill fiber" title="Dietary Fiber">🥗 ${fiber}g</span>
            <span class="meal-macro-pill sugar" title="Sugars">🍬 ${sugar}g</span>
            <span class="meal-macro-pill sodium" title="Sodium">🧂 ${sodium}mg</span>
          </div>

          <div class="meal-card-items-wrap">
            ${itemsHtml}
          </div>

          ${adviceHtml}
        </div>
      `;
    }).join('');

    this._bindMealActions(el.cardsContainer);
  }

  /**
   * Render High-Density Responsive Data Grid layout with footer totals.
   * @param {Array<Object>} meals
   */
  renderGrid(meals) {
    const el = this.elements;
    if (!el.gridTbody) return;

    const mealTypeNames = ['Breakfast', 'Lunch', 'Snack', 'Dinner'];
    const mealTypeClasses = ['breakfast', 'lunch', 'snack', 'dinner'];

    let sumCals = 0, sumProtein = 0, sumCarbs = 0, sumFat = 0, sumFiber = 0, sumSugar = 0, sumSodium = 0;

    el.gridTbody.innerHTML = meals.map(meal => {
      const typeIndex = typeof meal.mealType === 'number' ? meal.mealType : 1;
      const typeName = mealTypeNames[typeIndex] || meal.mealType || 'Meal';
      const typeClass = mealTypeClasses[typeIndex] || 'lunch';

      const d = new Date(meal.loggedAt || meal.LoggedAt);
      const dateFormatted = !isNaN(d)
        ? d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
        : '';
      const timeFormatted = !isNaN(d)
        ? d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
        : '';

      const items = meal.items || meal.Items || [];
      const itemsSummary = items.map(i => `${i.name || i.Name} (${i.estimatedPortion || '1 serving'})`).join(', ');

      const cals = meal.totalCalories || 0;
      const protein = meal.totalProteinGrams || 0;
      const carbs = meal.totalCarbsGrams || 0;
      const fat = meal.totalFatGrams || 0;
      const fiber = meal.totalFiberGrams || 0;
      const sugar = meal.totalSugarGrams || 0;
      const sodium = meal.totalSodiumMg || 0;

      sumCals += cals;
      sumProtein += protein;
      sumCarbs += carbs;
      sumFat += fat;
      sumFiber += fiber;
      sumSugar += sugar;
      sumSodium += sodium;

      const conf = Math.round((meal.overallConfidenceScore || 0.85) * 100);

      return `
        <tr>
          <td style="white-space: nowrap; font-family: var(--font-mono); font-size: 0.72rem; color: var(--text-muted);">
            <div>${dateFormatted}</div>
            <div>${timeFormatted}</div>
          </td>
          <td>
            <span class="table-meal-badge ${typeClass}">${typeName}</span>
          </td>
          <td>
            <div class="table-dish-name">${meal.dishName || meal.DishName || 'Logged Meal'}</div>
            <div class="table-dish-items">${itemsSummary}</div>
          </td>
          <td class="num-col" style="color: #fb923c; font-weight: 700;">${Math.round(cals)}</td>
          <td class="num-col" style="color: #38bdf8;">${protein.toFixed(1)}</td>
          <td class="num-col" style="color: #facc15;">${carbs.toFixed(1)}</td>
          <td class="num-col" style="color: #f87171;">${fat.toFixed(1)}</td>
          <td class="num-col" style="color: #34d399;">${fiber.toFixed(1)}</td>
          <td class="num-col" style="color: #f472b6;">${sugar.toFixed(1)}</td>
          <td class="num-col" style="color: #94a3b8;">${Math.round(sodium)}</td>
          <td style="font-size: 0.7rem; white-space: nowrap;">
            <span style="color: var(--status-emerald);">✓ Verified</span>
            <span style="color: var(--text-muted);">(${conf}%)</span>
          </td>
          <td class="action-col" style="text-align: center; white-space: nowrap;">
            <div class="table-actions-wrap">
              <button type="button" class="btn-table-action btn-edit-meal" data-meal-id="${meal.id}" title="Edit Meal">✏️</button>
              <button type="button" class="btn-table-action btn-delete-meal" data-meal-id="${meal.id}" title="Delete Meal">🗑️</button>
            </div>
          </td>
        </tr>
      `;
    }).join('');

    // Render summary footer row
    if (el.gridTfoot) {
      el.gridTfoot.innerHTML = `
        <tr>
          <td colspan="3" style="text-align: right; text-transform: uppercase; font-size: 0.72rem; letter-spacing: 0.05em; color: var(--text-secondary);">
            Total Aggregate (${meals.length} Meals):
          </td>
          <td class="num-col" style="color: #fb923c; font-size: 0.85rem;">${Math.round(sumCals)} kcal</td>
          <td class="num-col" style="color: #38bdf8; font-size: 0.85rem;">${sumProtein.toFixed(1)}g</td>
          <td class="num-col" style="color: #facc15; font-size: 0.85rem;">${sumCarbs.toFixed(1)}g</td>
          <td class="num-col" style="color: #f87171; font-size: 0.85rem;">${sumFat.toFixed(1)}g</td>
          <td class="num-col" style="color: #34d399; font-size: 0.85rem;">${sumFiber.toFixed(1)}g</td>
          <td class="num-col" style="color: #f472b6; font-size: 0.85rem;">${sumSugar.toFixed(1)}g</td>
          <td class="num-col" style="color: #94a3b8; font-size: 0.85rem;">${Math.round(sumSodium)} mg</td>
          <td></td>
          <td class="action-col"></td>
        </tr>
      `;
    }

    this._bindMealActions(el.gridTbody);
  }

  /**
   * Bind Edit and Delete click handlers on rendered meal containers.
   * @param {HTMLElement} container
   */
  _bindMealActions(container) {
    if (!container) return;

    // Edit meal handler
    container.querySelectorAll('.btn-edit-meal').forEach(btn => {
      btn.addEventListener('click', (e) => {
        e.stopPropagation();
        const mealId = btn.dataset.mealId;
        const meal = this._currentMeals.find(m => m.id === mealId);
        if (meal) {
          this._bus.emit('meal:edit', meal);
        }
      });
    });

    // Delete meal handler
    container.querySelectorAll('.btn-delete-meal').forEach(btn => {
      btn.addEventListener('click', async (e) => {
        e.stopPropagation();
        const mealId = btn.dataset.mealId;
        const meal = this._currentMeals.find(m => m.id === mealId);
        if (!meal) return;
        const dishName = meal.dishName || meal.DishName || 'this meal';

        const confirmed = await this._showDeleteConfirmModal(meal);
        if (!confirmed) return;

        btn.disabled = true;
        const originalContent = btn.innerHTML;
        btn.innerHTML = '⏳';

        try {
          await this._meals.deleteMeal(mealId);
          if (this._toast) {
            this._toast.show({
              title: 'Meal Deleted 🗑️',
              message: `Deleted "${dishName}". Your daily ledger and charts have been recalculated!`
            });
          }
          // Emit global meal:logged event to synchronize HUD and projections
          this._bus.emit('meal:logged');
          // Refresh current period meals
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
      });
    });
  }

  /**
   * Show custom Obsidian Dark confirmation modal before deleting a meal.
   * @param {Object} meal
   * @returns {Promise<boolean>}
   */
  _showDeleteConfirmModal(meal) {
    return new Promise((resolve) => {
      const modal = document.getElementById('delete-meal-modal');
      if (!modal) {
        // Fallback in case partial failed to load
        const dish = meal?.dishName || meal?.DishName || 'this meal';
        resolve(window.confirm(`Are you sure you want to delete "${dish}"?`));
        return;
      }

      const dishEl = document.getElementById('delete-preview-dish');
      const badgeEl = document.getElementById('delete-preview-badge');
      const timeEl = document.getElementById('delete-preview-time');
      const calsEl = document.getElementById('delete-macro-cals');
      const proteinEl = document.getElementById('delete-macro-protein');
      const carbsEl = document.getElementById('delete-macro-carbs');
      const fatEl = document.getElementById('delete-macro-fat');
      const fiberEl = document.getElementById('delete-macro-fiber');
      const sugarEl = document.getElementById('delete-macro-sugar');

      const btnConfirm = document.getElementById('btn-confirm-delete');
      const btnCancel = document.getElementById('btn-cancel-delete');
      const btnClose = document.getElementById('btn-close-delete-modal');

      const dishName = meal?.dishName || meal?.DishName || 'Logged Meal';
      if (dishEl) dishEl.textContent = dishName;

      // Meal type pills & badges
      const mealTypeNames = ['Breakfast', 'Lunch', 'Snack', 'Dinner'];
      const mealTypeClasses = ['breakfast', 'lunch', 'snack', 'dinner'];
      const mealTypeIcons = ['🌅', '☀️', '☕', '🌙'];
      const typeIndex = typeof meal.mealType === 'number' ? meal.mealType : 1;
      const typeName = mealTypeNames[typeIndex] || meal.mealType || 'Meal';
      const typeClass = mealTypeClasses[typeIndex] || 'lunch';
      const typeIcon = mealTypeIcons[typeIndex] || '🍽️';

      if (badgeEl) {
        badgeEl.className = `delete-preview-type-badge ${typeClass}`;
        badgeEl.innerHTML = `${typeIcon} ${typeName}`;
      }

      // Date / timestamp
      const d = new Date(meal.loggedAt || meal.LoggedAt);
      const dateFormatted = !isNaN(d)
        ? d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
        : '';
      const timeFormatted = !isNaN(d)
        ? d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
        : '';
      if (timeEl) {
        timeEl.textContent = dateFormatted ? `${dateFormatted} · ${timeFormatted}` : timeFormatted;
      }

      // Macro chips
      const cals = Math.round(meal.totalCalories || 0);
      const protein = (meal.totalProteinGrams || 0).toFixed(1);
      const carbs = (meal.totalCarbsGrams || 0).toFixed(1);
      const fat = (meal.totalFatGrams || 0).toFixed(1);
      const fiber = (meal.totalFiberGrams || 0).toFixed(1);
      const sugar = (meal.totalSugarGrams || 0).toFixed(1);

      if (calsEl) calsEl.textContent = `🔥 ${cals} kcal`;
      if (proteinEl) proteinEl.textContent = `💪 ${protein}g P`;
      if (carbsEl) carbsEl.textContent = `🌾 ${carbs}g C`;
      if (fatEl) fatEl.textContent = `🥑 ${fat}g F`;
      if (fiberEl) fiberEl.textContent = `🥗 ${fiber}g Fib`;
      if (sugarEl) sugarEl.textContent = `🍬 ${sugar}g Sug`;

      // Reset button states
      if (btnConfirm) {
        btnConfirm.disabled = false;
        btnConfirm.innerHTML = `<span class="btn-delete-icon">🗑️</span><span class="btn-delete-text">Delete Meal</span>`;
      }

      let isClosed = false;
      const cleanup = () => {
        if (isClosed) return;
        isClosed = true;
        modal.style.display = 'none';
        modal.classList.remove('active');
        document.removeEventListener('keydown', onKeyDown);
        if (btnConfirm) btnConfirm.removeEventListener('click', onConfirm);
        if (btnCancel) btnCancel.removeEventListener('click', onCancel);
        if (btnClose) btnClose.removeEventListener('click', onCancel);
        modal.removeEventListener('click', onBackdropClick);
      };

      const onConfirm = () => {
        cleanup();
        resolve(true);
      };

      const onCancel = () => {
        cleanup();
        resolve(false);
      };

      const onKeyDown = (e) => {
        if (e.key === 'Escape') {
          e.preventDefault();
          onCancel();
        }
      };

      const onBackdropClick = (e) => {
        if (e.target === modal) {
          onCancel();
        }
      };

      if (btnConfirm) btnConfirm.addEventListener('click', onConfirm);
      if (btnCancel) btnCancel.addEventListener('click', onCancel);
      if (btnClose) btnClose.addEventListener('click', onCancel);
      modal.addEventListener('click', onBackdropClick);
      document.addEventListener('keydown', onKeyDown);

      modal.style.display = 'flex';
      modal.classList.add('active');
      if (btnCancel) btnCancel.focus();
    });
  }

  /**
   * Export all loaded meal data to Microsoft Excel (RFC 4180 CSV with UTF-8 BOM).
   */
  exportToExcel() {
    const user = this._authService?.currentUser;
    const isExportAllowed = user?.entitlements?.allowDataExport ?? this._authService?.isAdmin();

    if (!isExportAllowed) {
      if (this._toast) {
        this._toast.show('Exporting meal history (Excel / CSV) is a Premium tier feature. Please upgrade your plan.', 'warning');
      }
      window.dispatchEvent(new CustomEvent('tier:upgrade_required', {
        detail: {
          error: 'FeatureTierUpgradeRequired',
          message: 'Exporting meal history (Excel / CSV) is a Premium tier feature. Please upgrade your plan.'
        }
      }));
      return;
    }

    if (!this._currentMeals || this._currentMeals.length === 0) {
      if (this._toast) this._toast.show('No meal data available to export in this period.', 'warning');
      return;
    }

    const period = this._state.activePeriod || '7D';
    if (this._meals && typeof this._meals.exportMealsClientCsv === 'function') {
      const success = this._meals.exportMealsClientCsv(this._currentMeals, period);
      if (success && this._toast) {
        this._toast.show(`📥 Successfully exported ${this._currentMeals.length} meals to Excel!`, 'success');
      }
    } else {
      // Direct server export fallback
      const url = `/api/meals/export?userId=${encodeURIComponent(this._state.userId)}&period=${encodeURIComponent(period)}&format=csv`;
      window.location.href = url;
    }
  }
}
