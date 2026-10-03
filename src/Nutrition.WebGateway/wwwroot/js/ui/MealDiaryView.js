/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

/**
 * MealDiaryView
 * Single Responsibility: Rendering logged meal history in Card or Grid view,
 * updating meal count badges and filter subtitles, managing empty states,
 * and handling meal deletion confirmation modals.
 */
export class MealDiaryView {
  /**
   * @param {Object} options
   * @param {HTMLElement} options.cardsContainer - Meal cards container
   * @param {HTMLElement} options.gridContainer - Grid table container
   * @param {HTMLElement} options.gridTbody - Grid table body element
   * @param {HTMLElement} options.gridTfoot - Grid table footer element
   * @param {HTMLElement} [options.emptyState] - Empty state placeholder element
   * @param {HTMLElement} [options.countBadge] - Header badge displaying meal count
   * @param {HTMLElement} [options.filterSubtitle] - Subtitle element displaying active filter
   * @param {HTMLElement} [options.clearFilterBtn] - Button to clear active filter
   * @param {Function} [options.onEditMeal] - Callback when Edit is clicked: (meal) => void
   * @param {Function} [options.onDeleteMeal] - Callback when Delete is confirmed: (meal, btn) => Promise<void>
   */
  constructor({
    cardsContainer,
    gridContainer,
    gridTbody,
    gridTfoot,
    emptyState = null,
    countBadge = null,
    filterSubtitle = null,
    clearFilterBtn = null,
    onEditMeal = null,
    onDeleteMeal = null
  }) {
    this._cardsContainer = cardsContainer;
    this._gridContainer = gridContainer;
    this._gridTbody = gridTbody;
    this._gridTfoot = gridTfoot;
    this._emptyState = emptyState;
    this._countBadge = countBadge;
    this._filterSubtitle = filterSubtitle;
    this._clearFilterBtn = clearFilterBtn;
    this._onEditMeal = onEditMeal;
    this._onDeleteMeal = onDeleteMeal;

    this._currentView = 'cards';
    this._currentMeals = [];
  }

  /**
   * Set active presentation view ('cards' | 'grid')
   * @param {'cards'|'grid'} view
   */
  setView(view) {
    this._currentView = view;
    if (this._cardsContainer && this._gridContainer) {
      if (view === 'cards') {
        this._cardsContainer.classList.remove('hidden');
        this._gridContainer.classList.add('hidden');
      } else {
        this._cardsContainer.classList.add('hidden');
        this._gridContainer.classList.remove('hidden');
      }
    }
    this.render(this._currentMeals);
  }

  /**
   * Render meals into the active layout.
   * @param {Array<Object>} meals
   * @param {string} [period='7D']
   * @param {Object|null} [filter=null]
   */
  render(meals = [], period = '7D', filter = null) {
    this._currentMeals = meals || [];
    const hasMeals = this._currentMeals.length > 0;

    // Update empty state
    if (this._emptyState) {
      if (hasMeals) this._emptyState.classList.add('hidden');
      else this._emptyState.classList.remove('hidden');
    }

    // Update badge and subtitle
    this.updateHeaderMeta(this._currentMeals.length, period, filter);

    if (!hasMeals) {
      if (this._cardsContainer) this._cardsContainer.innerHTML = '';
      if (this._gridTbody) this._gridTbody.innerHTML = '';
      if (this._gridTfoot) this._gridTfoot.innerHTML = '';
      return;
    }

    if (this._currentView === 'cards') {
      this.renderCards(this._currentMeals);
    } else {
      this.renderGrid(this._currentMeals);
    }
  }

  /**
   * Update count badge and subtitle text.
   * @param {number} count
   * @param {string} period
   * @param {Object|null} filter
   */
  updateHeaderMeta(count, period = '7D', filter = null) {
    if (this._countBadge) {
      this._countBadge.textContent = `${count} meal${count === 1 ? '' : 's'}`;
    }

    if (this._filterSubtitle) {
      if (filter) {
        this._filterSubtitle.innerHTML = `Filtered by: <strong style="color: #38bdf8;">${filter.label}</strong>`;
      } else {
        const periodLabels = {
          '1D': "Today's",
          '7D': '7-Day',
          '30D': '30-Day',
          '90D': 'Quarterly (90D)',
          '365D': 'Yearly (1Y)'
        };
        this._filterSubtitle.textContent = `Showing ${periodLabels[period] || period} meal history`;
      }
    }

    if (this._clearFilterBtn) {
      if (filter) this._clearFilterBtn.classList.remove('hidden');
      else this._clearFilterBtn.classList.add('hidden');
    }
  }

  /**
   * Render Obsidian Dark Meal Cards.
   * @param {Array<Object>} meals
   */
  renderCards(meals) {
    if (!this._cardsContainer) return;

    const mealTypeNames = ['Breakfast', 'Lunch', 'Snack', 'Dinner'];
    const mealTypeClasses = ['breakfast', 'lunch', 'snack', 'dinner'];
    const mealTypeIcons = ['🌅', '☀️', '☕', '🌙'];

    this._cardsContainer.innerHTML = meals.map(meal => {
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

    this._bindActions(this._cardsContainer);
  }

  /**
   * Render High-Density Responsive Data Grid.
   * @param {Array<Object>} meals
   */
  renderGrid(meals) {
    if (!this._gridTbody) return;

    const mealTypeNames = ['Breakfast', 'Lunch', 'Snack', 'Dinner'];
    const mealTypeClasses = ['breakfast', 'lunch', 'snack', 'dinner'];

    let sumCals = 0, sumProtein = 0, sumCarbs = 0, sumFat = 0, sumFiber = 0, sumSugar = 0, sumSodium = 0;

    this._gridTbody.innerHTML = meals.map(meal => {
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

    // Summary footer aggregate
    if (this._gridTfoot) {
      this._gridTfoot.innerHTML = `
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

    this._bindActions(this._gridTbody);
  }

  /**
   * Bind Edit and Delete event listeners on cards or table rows.
   * @param {HTMLElement} container
   */
  _bindActions(container) {
    if (!container) return;

    // Edit meal
    container.querySelectorAll('.btn-edit-meal').forEach(btn => {
      btn.addEventListener('click', (e) => {
        e.stopPropagation();
        const mealId = btn.dataset.mealId;
        const meal = this._currentMeals.find(m => m.id === mealId);
        if (meal && typeof this._onEditMeal === 'function') {
          this._onEditMeal(meal);
        }
      });
    });

    // Delete meal
    container.querySelectorAll('.btn-delete-meal').forEach(btn => {
      btn.addEventListener('click', async (e) => {
        e.stopPropagation();
        const mealId = btn.dataset.mealId;
        const meal = this._currentMeals.find(m => m.id === mealId);
        if (!meal) return;

        const confirmed = await this.showDeleteConfirmModal(meal);
        if (!confirmed) return;

        if (typeof this._onDeleteMeal === 'function') {
          await this._onDeleteMeal(meal, btn);
        }
      });
    });
  }

  /**
   * Obsidian Dark Delete Confirmation Modal.
   * @param {Object} meal
   * @returns {Promise<boolean>}
   */
  showDeleteConfirmModal(meal) {
    return new Promise((resolve) => {
      const modal = document.getElementById('delete-meal-modal');
      if (!modal) {
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
        document.body.classList.remove('modal-open');
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

      document.body.classList.add('modal-open');
      modal.style.display = 'flex';
      modal.classList.add('active');
      if (btnCancel) btnCancel.focus();
    });
  }
}
