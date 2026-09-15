import { estimateIndianFoodNutrition, estimateFoodNutritionWithAi } from '../services/nutrition-estimator.js';

/**
 * ReviewModalController
 * Manages AI Food Detection Review, interactive portion steppers, ghee/tadka adjustments,
 * item renaming/corrections, and continuous model retraining.
 */
export class ReviewModalController {
  /**
   * @param {Object} options
   * @param {import('../services/meals-service.js').MealsService} options.mealsService
   * @param {import('../ui/toast.js').ToastNotificationService} options.toastService
   * @param {import('../ui/confetti.js').ConfettiService} options.confettiService
   * @param {import('../core/state.js').AppState} options.appState
   * @param {import('../core/event-bus.js').EventBus} options.eventBus
   */
  constructor({ mealsService, toastService, confettiService, appState, eventBus }) {
    this._meals = mealsService;
    this._toast = toastService;
    this._confetti = confettiService;
    this._state = appState;
    this._bus = eventBus;

    this._bindEvents();

    // Listen for meal analyzed event from MealLogger
    this._bus.on('meal:analyzed', (analysis) => this.open(analysis));
  }

  get elements() {
    return {
      modal: document.getElementById('review-modal'),
      mealType: document.getElementById('review-meal-type'),
      dishName: document.getElementById('review-dish-name'),
      confidenceBadge: document.getElementById('review-confidence-badge'),
      itemsList: document.getElementById('review-items-list'),
      flagsContainer: document.getElementById('review-flags-container'),
      dietitianAdvice: document.getElementById('review-dietitian-advice'),
      btnCancel: document.getElementById('btn-cancel-review'),
      btnConfirm: document.getElementById('btn-confirm-meal'),
      chipGhee: document.getElementById('chip-ghee'),
      chipTadka: document.getElementById('chip-tadka'),
      chipOilfree: document.getElementById('chip-oilfree'),
      btnAddItem: document.getElementById('btn-add-review-item')
    };
  }

  _bindEvents() {
    const el = this.elements;

    // Cooking fat toggles
    if (el.chipGhee) {
      el.chipGhee.addEventListener('click', () => {
        el.chipGhee.classList.toggle('active');
        if (el.chipOilfree) el.chipOilfree.classList.remove('active');
        this._state.addedGhee = el.chipGhee.classList.contains('active') ? 45 : 0;
        this.recalculateTotals();
      });
    }

    if (el.chipTadka) {
      el.chipTadka.addEventListener('click', () => {
        el.chipTadka.classList.toggle('active');
        if (el.chipOilfree) el.chipOilfree.classList.remove('active');
        this._state.addedTadka = el.chipTadka.classList.contains('active') ? 60 : 0;
        this.recalculateTotals();
      });
    }

    if (el.chipOilfree) {
      el.chipOilfree.addEventListener('click', () => {
        el.chipOilfree.classList.toggle('active');
        if (el.chipGhee) el.chipGhee.classList.remove('active');
        if (el.chipTadka) el.chipTadka.classList.remove('active');
        this._state.addedGhee = 0;
        this._state.addedTadka = 0;
        this.recalculateTotals();
      });
    }

    if (el.btnCancel) {
      el.btnCancel.addEventListener('click', () => this.close());
    }

    if (el.btnConfirm) {
      el.btnConfirm.addEventListener('click', () => this.handleConfirmMeal());
    }

    // Meal Timing Selector Pills
    document.querySelectorAll('#review-meal-type-pills .meal-pill').forEach(pill => {
      pill.addEventListener('click', () => {
        document.querySelectorAll('#review-meal-type-pills .meal-pill').forEach(p => p.classList.remove('active'));
        pill.classList.add('active');
        if (this._state.currentMeal) {
          this._state.currentMeal.mealType = pill.dataset.type;
          const typeIcons = { 'Breakfast': '🌅', 'Lunch': '☀️', 'Snack': '☕', 'Dinner': '🌙' };
          if (el.mealType) {
            el.mealType.textContent = `${typeIcons[pill.dataset.type] || ''} ${pill.dataset.type} Review & Correction`;
          }
        }
      });
    });

    if (el.btnAddItem) {
      el.btnAddItem.addEventListener('click', () => {
        if (!this._state.currentMeal) return;
        if (!this._state.currentMeal.identifiedItems) this._state.currentMeal.identifiedItems = [];
        this._state.currentMeal.identifiedItems.push({
          name: 'Cooked Indian Subzi',
          originalDetection: 'Added by User',
          hindiOrRegionalName: 'Subzi',
          estimatedPortion: '1 Katori (120g)',
          quantity: 1,
          grams: 120,
          calories: 120,
          proteinGrams: 3.0,
          carbsGrams: 10.0,
          fatGrams: 6.0,
          fiberGrams: 3.5,
          sodiumMg: 180.0
        });
        this.renderItems();
        this.recalculateTotals();
      });
    }

    // Modal background click
    if (el.modal) {
      el.modal.addEventListener('click', (e) => {
        if (e.target === el.modal) this.close();
      });
    }
  }

  /**
   * Open review modal with analysis payload.
   * @param {Object} analysis
   */
  open(analysis) {
    this._state.currentMeal = analysis;
    this._state.addedGhee = 0;
    this._state.addedTadka = 0;

    const el = this.elements;
    if (el.chipGhee) el.chipGhee.classList.remove('active');
    if (el.chipTadka) el.chipTadka.classList.remove('active');
    if (el.chipOilfree) el.chipOilfree.classList.remove('active');

    const currentMealType = analysis.mealType || 'Lunch';
    this._state.currentMeal.mealType = currentMealType;

    const typeIcons = { 'Breakfast': '🌅', 'Lunch': '☀️', 'Snack': '☕', 'Dinner': '🌙' };
    if (el.mealType) {
      el.mealType.textContent = `${typeIcons[currentMealType] || '☀️'} ${currentMealType} Review & Correction`;
    }
    if (el.dishName) {
      el.dishName.textContent = analysis.dishName;
    }

    // Update active pill
    document.querySelectorAll('#review-meal-type-pills .meal-pill').forEach(pill => {
      if (pill.dataset.type.toLowerCase() === currentMealType.toLowerCase()) {
        pill.classList.add('active');
      } else {
        pill.classList.remove('active');
      }
    });

    const autoHint = document.getElementById('meal-time-auto-hint');
    if (autoHint) {
      autoHint.textContent = `Auto-detected from clock (${new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })})`;
    }

    // Retain original detection name on each item to track user training
    if (analysis.identifiedItems) {
      analysis.identifiedItems.forEach(i => {
        if (!i.originalDetection) i.originalDetection = i.name;
      });
    }

    const confPct = Math.round((analysis.overallConfidenceScore || 0.88) * 100);
    if (el.confidenceBadge) {
      el.confidenceBadge.textContent = `✓ ${confPct}% Confidence`;
      el.confidenceBadge.className = confPct >= 70 ? 'confidence-badge confidence-pass' : 'confidence-badge confidence-fail';
    }

    this.renderItems();
    this.renderFlags();

    if (el.dietitianAdvice) {
      el.dietitianAdvice.textContent = analysis.dietitianAdvice || 'Wholesome homestyle preparation adhering to ICMR-NIN guidelines.';
    }

    if (el.modal) el.modal.style.display = 'flex';
  }

  close() {
    const el = this.elements;
    if (el.modal) el.modal.style.display = 'none';
    this._state.currentMeal = null;
  }

  renderItems() {
    const el = this.elements;
    if (!this._state.currentMeal || !this._state.currentMeal.identifiedItems || !el.itemsList) return;

    el.itemsList.innerHTML = this._state.currentMeal.identifiedItems.map((item, idx) => {
      const isCorrected = item.originalDetection &&
        item.originalDetection.trim().toLowerCase() !== item.name.trim().toLowerCase();

      const qty = item.quantity || 1;
      const totalItemKcal = Math.round(item.calories * qty);
      const totalItemProtein = Math.round(item.proteinGrams * qty * 10) / 10;
      const totalItemCarbs = Math.round((item.carbsGrams || 15) * qty);
      const totalItemFat = Math.round((item.fatGrams || 5) * qty);

      return `
        <div class="item-row-editable">
          <div class="item-edit-left">
            <input type="text" class="item-name-input" value="${item.name}" 
                   placeholder="e.g. Bhindi Masala, Palak Paneer, Moong Dal"
                   title="Click to edit or rename this dish. Nutrition will auto-update."
                   data-idx="${idx}" />
            <div style="display: flex; gap: 8px; align-items: center; font-size: 0.72rem; color: var(--text-muted); flex-wrap: wrap; margin-top: 3px;">
              <span>${item.estimatedPortion || '1 Katori'}</span>
              <span>·</span>
              <span class="tabular" style="font-weight: 600; color: var(--text-primary);">${totalItemKcal} kcal</span>
              <span>·</span>
              <span class="tabular" style="color: #38bdf8; font-weight: 600;">${totalItemProtein}g Protein</span>
              <span>·</span>
              <span class="tabular" style="color: #fbbf24;">${totalItemCarbs}g Carbs</span>
              <span>·</span>
              <span class="tabular" style="color: #f87171;">${totalItemFat}g Fat</span>
            </div>
            ${isCorrected ? `
              <div class="item-correction-indicator" style="color: var(--status-emerald); font-size: 0.72rem; margin-top: 4px; display: flex; align-items: center; gap: 4px; flex-wrap: wrap;">
                <span>🧠 Trained: '${item.originalDetection}' ➔ '${item.name}'</span>
                <span style="color: ${item.isAiEstimated ? '#93c5fd' : 'var(--text-secondary)'}; background: ${item.isAiEstimated ? 'rgba(59, 130, 246, 0.15)' : 'rgba(39, 195, 128, 0.1)'}; padding: 1px 6px; border-radius: 4px; border: 1px solid ${item.isAiEstimated ? 'rgba(59, 130, 246, 0.35)' : 'rgba(39, 195, 128, 0.3)'};">
                  ${item.isAiEstimated ? '🤖 AI-Refined' : '⚡ Auto-recalculated'}: ${Math.round(item.calories)} kcal · ${item.proteinGrams}g Protein
                </span>
              </div>
            ` : ''}
          </div>
          <div class="item-edit-right">
            <div class="portion-stepper">
              <button type="button" class="step-btn" data-step="-0.5" data-idx="${idx}">−</button>
              <span class="step-val tabular">${item.quantity || 1}</span>
              <button type="button" class="step-btn" data-step="0.5" data-idx="${idx}">+</button>
            </div>
            <button type="button" class="btn-delete-item" title="Remove item" data-idx="${idx}">🗑️</button>
          </div>
        </div>
      `;
    }).join('');

    // Attach listeners to items
    el.itemsList.querySelectorAll('.item-name-input').forEach(input => {
      input.addEventListener('change', (e) => {
        const idx = parseInt(e.target.dataset.idx);
        this.updateItemName(idx, e.target.value);
      });
      input.addEventListener('blur', (e) => {
        const idx = parseInt(e.target.dataset.idx);
        if (this._state.currentMeal && this._state.currentMeal.identifiedItems[idx]) {
          if (this._state.currentMeal.identifiedItems[idx].name.trim().toLowerCase() !== e.target.value.trim().toLowerCase()) {
            this.updateItemName(idx, e.target.value);
          }
        }
      });
      input.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
          e.preventDefault();
          e.target.blur();
        }
      });
    });

    el.itemsList.querySelectorAll('.step-btn').forEach(btn => {
      btn.addEventListener('click', (e) => {
        const idx = parseInt(e.target.dataset.idx);
        const delta = parseFloat(e.target.dataset.step);
        this.stepItemQuantity(idx, delta);
      });
    });

    el.itemsList.querySelectorAll('.btn-delete-item').forEach(btn => {
      btn.addEventListener('click', (e) => {
        const idx = parseInt(e.target.dataset.idx);
        this.deleteItem(idx);
      });
    });
  }

  async updateItemName(idx, newName) {
    if (!this._state.currentMeal || !this._state.currentMeal.identifiedItems[idx]) return;
    const trimmed = newName.trim();
    if (!trimmed) return;

    const item = this._state.currentMeal.identifiedItems[idx];
    const oldName = item.name;
    item.name = trimmed;

    // 1. Instant client-side nutrition estimation (Zero Latency - Hardcoded ICMR-NIN baseline)
    const baseline = estimateIndianFoodNutrition(trimmed, item.estimatedPortion);
    if (baseline) {
      item.calories = baseline.calories;
      item.proteinGrams = baseline.proteinGrams;
      item.carbsGrams = baseline.carbsGrams;
      item.fatGrams = baseline.fatGrams;
      item.fiberGrams = baseline.fiberGrams;
      item.sodiumMg = baseline.sodiumMg;
      item.isAiEstimated = false;
      if (baseline.portion) item.estimatedPortion = baseline.portion;
      if (baseline.hindiName) item.hindiOrRegionalName = baseline.hindiName;
    }

    // 2. Synchronize meal dishName title
    if (this._state.currentMeal.dishName && oldName && oldName.toLowerCase() !== trimmed.toLowerCase()) {
      let cleanDish = this._state.currentMeal.dishName.replace(/\s*\([~≈]?\d+\s*kcal\)/gi, '').trim();
      const escapedOld = oldName.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
      const regex = new RegExp(escapedOld, 'i');
      if (regex.test(cleanDish)) {
        cleanDish = cleanDish.replace(regex, trimmed);
      }
      this._state.currentMeal.dishName = cleanDish;
    }

    this.renderItems();
    this.recalculateTotals();

    // 3. User feedback for instant baseline
    this._toast.show({
      title: '⚡ Nutrition Recalculated!',
      message: `Updated to ${Math.round(item.calories)} kcal, ${item.proteinGrams}g Protein for '${trimmed}'. Refining with AI...`
    });

    // 4. Asynchronously query AI agent (Gemini 3.8 / Clinical NLP) to refine estimation
    try {
      const aiResult = await estimateFoodNutritionWithAi(trimmed, item.estimatedPortion);
      if (aiResult && (aiResult.isAiEstimated || aiResult.source?.includes('AI'))) {
        item.calories = aiResult.calories;
        item.proteinGrams = aiResult.proteinGrams;
        item.carbsGrams = aiResult.carbsGrams;
        item.fatGrams = aiResult.fatGrams;
        item.fiberGrams = aiResult.fiberGrams;
        item.sodiumMg = aiResult.sodiumMg;
        item.isAiEstimated = true;
        if (aiResult.portion) item.estimatedPortion = aiResult.portion;
        if (aiResult.hindiName) item.hindiOrRegionalName = aiResult.hindiName;

        this.renderItems();
        this.recalculateTotals();

        this._toast.show({
          title: '🤖 AI Nutrition Refined!',
          message: `${aiResult.name}: ${Math.round(item.calories)} kcal, ${item.proteinGrams}g Protein.`
        });
      }
    } catch (_) {
      // Hardcoded ICMR-NIN baseline already applied
    }
  }

  stepItemQuantity(idx, delta) {
    const item = this._state.currentMeal.identifiedItems[idx];
    item.quantity = Math.max(0.5, (item.quantity || 1) + delta);
    this.renderItems();
    this.recalculateTotals();
  }

  deleteItem(idx) {
    if (!this._state.currentMeal || !this._state.currentMeal.identifiedItems) return;
    this._state.currentMeal.identifiedItems.splice(idx, 1);
    this.renderItems();
    this.recalculateTotals();
  }

  quickAddSubzi(name, kcal, protein, carbs, fat) {
    if (!this._state.currentMeal) return;
    if (!this._state.currentMeal.identifiedItems) this._state.currentMeal.identifiedItems = [];
    this._state.currentMeal.identifiedItems.push({
      name,
      originalDetection: 'Added by User',
      hindiOrRegionalName: name,
      estimatedPortion: '1 Katori (120g)',
      quantity: 1,
      grams: 120,
      calories: kcal,
      proteinGrams: protein,
      carbsGrams: carbs,
      fatGrams: fat,
      fiberGrams: 3.5,
      sodiumMg: 190.0
    });
    this.renderItems();
    this.recalculateTotals();
  }

  renderFlags() {
    const el = this.elements;
    if (!this._state.currentMeal || !el.flagsContainer) return;

    const flags = this._state.currentMeal.whoComplianceFlags || [];
    const medWarnings = this._state.currentMeal.medicationWarnings || [];

    let html = '';
    flags.forEach(f => {
      html += `<div class="flag-alert flag-warning">⚠️ <span>${f}</span></div>`;
    });
    medWarnings.forEach(m => {
      html += `<div class="flag-alert flag-medication">💊 <span>${m}</span></div>`;
    });
    el.flagsContainer.innerHTML = html;
  }

  recalculateTotals() {
    const el = this.elements;
    if (!this._state.currentMeal || !el.dishName) return;

    const baseKcal = this._state.currentMeal.identifiedItems.reduce(
      (acc, i) => acc + (i.calories || 0) * (i.quantity || 1),
      0
    );
    const totalKcal = baseKcal + (this._state.addedGhee || 0) + (this._state.addedTadka || 0);

    const cleanDish = (this._state.currentMeal.dishName || 'Meal')
      .replace(/\s*\([~≈]?\d+\s*kcal\)/gi, '')
      .trim();

    el.dishName.textContent = `${cleanDish} (~${Math.round(totalKcal)} kcal)`;
  }

  async handleConfirmMeal() {
    if (!this._state.currentMeal) return;
    const el = this.elements;

    if (el.btnConfirm) {
      el.btnConfirm.disabled = true;
      el.btnConfirm.textContent = 'Logging & Training Model...';
    }

    try {
      const mealTypeMap = { 'Breakfast': 0, 'Lunch': 1, 'Snack': 2, 'Dinner': 3 };
      const numericMealType = mealTypeMap[this._state.currentMeal.mealType] !== undefined
        ? mealTypeMap[this._state.currentMeal.mealType]
        : 1;

      const payload = {
        userId: this._state.userId,
        mealType: numericMealType,
        dishName: this._state.currentMeal.dishName,
        overallConfidenceScore: this._state.currentMeal.overallConfidenceScore || 0.88,
        addedGheeKcal: this._state.addedGhee,
        addedTadkaKcal: this._state.addedTadka,
        items: this._state.currentMeal.identifiedItems.map(i => ({
          name: i.name,
          originalDetection: i.originalDetection || i.name,
          hindiOrRegionalName: i.hindiOrRegionalName || i.name,
          estimatedPortion: i.estimatedPortion || '1 Portion',
          quantity: i.quantity || 1,
          grams: i.grams || 100,
          calories: i.calories || 100,
          proteinGrams: i.proteinGrams || 5,
          carbsGrams: i.carbsGrams || 15,
          fatGrams: i.fatGrams || 3,
          fiberGrams: i.fiberGrams || 2,
          sodiumMg: i.sodiumMg || 100,
          cookingMediumEstimate: i.cookingMediumEstimate || 'Standard Home Cooking'
        })),
        whoComplianceFlags: this._state.currentMeal.whoComplianceFlags || [],
        medicationWarnings: this._state.currentMeal.medicationWarnings || [],
        dietitianAdvice: this._state.currentMeal.dietitianAdvice
      };

      const data = await this._meals.confirmMeal(payload);

      if (el.btnConfirm) {
        el.btnConfirm.disabled = false;
        el.btnConfirm.textContent = 'Looks Great! Log Meal 🎉';
      }

      this.close();
      this._confetti.burst();

      // Emit global meal:logged event
      this._bus.emit('meal:logged', data);

      if (data.learnedMessage) {
        this._toast.show({
          title: '🧠 AI Vision Re-Trained! 🎉',
          message: `${data.learnedMessage}. Diet Dost memorized your household preference for future scans!`,
          actionText: '📊 View Daily Graph',
          onAction: () => this._bus.emit('analytics:switch-period', '1D')
        });
      } else {
        const mealTypeName = this._state.currentMeal?.mealType || 'Meal';
        this._toast.show({
          title: `${mealTypeName} Logged! 🎉`,
          message: `Logged ${Math.round(data.totalCalories)} kcal and ${Math.round(data.totalProtein)}g Protein. Compliance score updated!`,
          actionText: '📊 View Daily Graph',
          onAction: () => this._bus.emit('analytics:switch-period', '1D')
        });
      }
    } catch (err) {
      if (el.btnConfirm) {
        el.btnConfirm.disabled = false;
        el.btnConfirm.textContent = 'Looks Great! Log Meal 🎉';
      }
      this._toast.show({
        title: 'Error Logging Meal',
        message: err.message || 'Failed to save meal.'
      });
    }
  }
}
