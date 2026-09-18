import { estimateIndianFoodNutrition, estimateFoodNutritionWithAi, generateDietitianAdvice } from '../services/nutrition-estimator.js?v=1.2.6';

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

    this._feedbackRating = null;
    this._feedbackSubmitted = false;

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
      modelBadge: document.getElementById('review-model-badge'),
      modelName: document.getElementById('review-model-name'),
      itemsList: document.getElementById('review-items-list'),
      flagsContainer: document.getElementById('review-flags-container'),
      dietitianAdvice: document.getElementById('review-dietitian-advice'),
      btnCancel: document.getElementById('btn-cancel-review'),
      btnConfirm: document.getElementById('btn-confirm-meal'),
      photoContainer: document.getElementById('review-photo-container'),
      photoWrapper: document.getElementById('review-photo-wrapper'),
      mealPhoto: document.getElementById('review-meal-photo'),
      btnZoomPhoto: document.getElementById('btn-zoom-meal-photo'),
      photoZoomLabel: document.getElementById('photo-zoom-label'),
      bodyLayout: document.getElementById('review-body-layout'),
      lightbox: document.getElementById('review-lightbox'),
      lightboxImg: document.getElementById('review-lightbox-img'),
      lightboxTitle: document.getElementById('review-lightbox-title'),
      btnCloseLightbox: document.getElementById('btn-close-lightbox'),
      chipGhee: document.getElementById('chip-ghee'),
      chipTadka: document.getElementById('chip-tadka'),
      chipOilfree: document.getElementById('chip-oilfree'),
      btnAddItem: document.getElementById('btn-add-review-item'),
      btnResetMemory: document.getElementById('btn-reset-training-memory'),
      // AI Feedback & Continuous Retraining Elements
      btnFeedbackUp: document.getElementById('btn-feedback-up'),
      btnFeedbackDown: document.getElementById('btn-feedback-down'),
      feedbackRemarksRow: document.getElementById('feedback-remarks-row'),
      feedbackRemarksInput: document.getElementById('feedback-remarks-input'),
      btnSubmitFeedback: document.getElementById('btn-submit-feedback'),
      feedbackRetrainStatus: document.getElementById('feedback-retrain-status')
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

    // Photo Lightbox Inspection
    const openLightbox = () => {
      const currentPhoto = (this._state.currentMeal && (this._state.currentMeal.photoUrl || this._state.currentMeal.photoUri)) || (el.mealPhoto ? el.mealPhoto.src : '');
      if (!currentPhoto || !el.lightbox || !el.lightboxImg) return;
      el.lightboxImg.src = currentPhoto;
      if (el.lightboxTitle && this._state.currentMeal) {
        el.lightboxTitle.textContent = `📸 ${this._state.currentMeal.dishName || 'Meal Plate Inspection'}`;
      }
      el.lightbox.style.display = 'flex';
    };

    const closeLightbox = () => {
      if (el.lightbox) el.lightbox.style.display = 'none';
      if (el.lightboxImg) el.lightboxImg.src = '';
    };

    if (el.btnZoomPhoto) {
      el.btnZoomPhoto.addEventListener('click', (e) => {
        e.stopPropagation();
        openLightbox();
      });
    }
    if (el.photoWrapper) {
      el.photoWrapper.addEventListener('click', openLightbox);
    }
    if (el.btnCloseLightbox) {
      el.btnCloseLightbox.addEventListener('click', closeLightbox);
    }
    if (el.lightbox) {
      el.lightbox.addEventListener('click', (e) => {
        if (e.target === el.lightbox || e.target.classList.contains('review-lightbox-frame')) {
          closeLightbox();
        }
      });
    }
    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape' && el.lightbox && el.lightbox.style.display === 'flex') {
        closeLightbox();
      }
    });

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
          this.recalculateTotals();
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

    if (el.btnResetMemory) {
      el.btnResetMemory.addEventListener('click', async () => {
        if (!confirm('Reset all trained dish memory for this profile? AI will revert to clean visual identification.')) return;
        try {
          const userId = this._state.userId || 'user-default';
          const res = await fetch(`/api/meals/corrections/reset?userId=${encodeURIComponent(userId)}`, {
            method: 'DELETE'
          });
          if (res.ok) {
            window.showToast?.('🧠 Trained memory cleared. Ready for clean scans!', 'success');
            if (this._state.currentMeal?.identifiedItems) {
              this._state.currentMeal.identifiedItems.forEach(item => {
                item.originalDetection = item.name;
              });
              this.renderItems();
            }
          } else {
            window.showToast?.('Failed to reset trained memory.', 'error');
          }
        } catch (e) {
          console.error(e);
          window.showToast?.('Network error while resetting memory.', 'error');
        }
      });
    }

    // AI Accuracy Feedback & Continuous Retraining
    if (el.btnFeedbackUp) {
      el.btnFeedbackUp.addEventListener('click', () => {
        this._feedbackRating = 'thumbs_up';
        el.btnFeedbackUp.classList.add('active-up');
        if (el.btnFeedbackDown) el.btnFeedbackDown.classList.remove('active-down');

        // Reveal remarks row for optional positive remarks
        if (el.feedbackRemarksRow) {
          el.feedbackRemarksRow.style.display = 'flex';
          if (el.feedbackRemarksInput) {
            el.feedbackRemarksInput.placeholder = "Optional remarks (e.g. 'Crispy bhindi, perfectly balanced')...";
          }
          if (el.btnSubmitFeedback) {
            el.btnSubmitFeedback.textContent = '👍 Save Feedback';
          }
        }

        // Auto-submit positive signal
        this.submitAiFeedback('thumbs_up', false);
      });
    }

    if (el.btnFeedbackDown) {
      el.btnFeedbackDown.addEventListener('click', () => {
        this._feedbackRating = 'thumbs_down';
        el.btnFeedbackDown.classList.add('active-down');
        if (el.btnFeedbackUp) el.btnFeedbackUp.classList.remove('active-up');

        // Reveal remarks row for correction input
        if (el.feedbackRemarksRow) {
          el.feedbackRemarksRow.style.display = 'flex';
          if (el.feedbackRemarksInput) {
            el.feedbackRemarksInput.placeholder = "Add remarks to retrain model (e.g. 'Dal was Toor Dal', 'Subzi was Lauki')...";
            el.feedbackRemarksInput.focus();
          }
          if (el.btnSubmitFeedback) {
            el.btnSubmitFeedback.textContent = '⚡ Retrain AI';
          }
        }

        if (el.feedbackRetrainStatus && !this._feedbackSubmitted) {
          el.feedbackRetrainStatus.style.display = 'block';
          el.feedbackRetrainStatus.className = 'feedback-retrain-status retrain-info';
          el.feedbackRetrainStatus.innerHTML = '<span>ℹ️</span> <span>Type your correction above and click <strong>⚡ Retrain AI</strong> to update the model.</span>';
        }
      });
    }

    if (el.btnSubmitFeedback) {
      el.btnSubmitFeedback.addEventListener('click', () => {
        const rating = this._feedbackRating || 'thumbs_down';
        this.submitAiFeedback(rating, true);
      });
    }

    if (el.feedbackRemarksInput) {
      el.feedbackRemarksInput.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
          e.preventDefault();
          const rating = this._feedbackRating || 'thumbs_down';
          this.submitAiFeedback(rating, true);
        }
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

    if (el.modelBadge && el.modelName) {
      if (analysis.detectedByModel) {
        el.modelName.textContent = analysis.detectedByModel;
        el.modelBadge.style.display = 'inline-flex';
      } else {
        el.modelBadge.style.display = 'none';
      }
    }

    // Render uploaded meal photo evidence if available
    const photoUrl = analysis.photoUrl || analysis.photoUri;
    if (photoUrl) {
      if (el.mealPhoto) {
        el.mealPhoto.src = photoUrl;
      }
      if (el.photoContainer) {
        el.photoContainer.style.display = 'block';
      }
      if (el.bodyLayout) {
        el.bodyLayout.classList.remove('no-photo');
      }
    } else {
      if (el.photoContainer) {
        el.photoContainer.style.display = 'none';
      }
      if (el.mealPhoto) {
        el.mealPhoto.src = '';
      }
      if (el.bodyLayout) {
        el.bodyLayout.classList.add('no-photo');
      }
    }

    // Reset AI feedback UI state
    this._feedbackRating = null;
    this._feedbackSubmitted = false;
    if (el.btnFeedbackUp) el.btnFeedbackUp.classList.remove('active-up');
    if (el.btnFeedbackDown) el.btnFeedbackDown.classList.remove('active-down');
    if (el.feedbackRemarksRow) el.feedbackRemarksRow.style.display = 'none';
    if (el.feedbackRemarksInput) el.feedbackRemarksInput.value = '';
    if (el.feedbackRetrainStatus) {
      el.feedbackRetrainStatus.style.display = 'none';
      el.feedbackRetrainStatus.innerHTML = '';
      el.feedbackRetrainStatus.className = 'feedback-retrain-status';
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
    if (el.lightbox) el.lightbox.style.display = 'none';
    if (el.lightboxImg) el.lightboxImg.src = '';
    if (el.btnFeedbackUp) el.btnFeedbackUp.classList.remove('active-up');
    if (el.btnFeedbackDown) el.btnFeedbackDown.classList.remove('active-down');
    if (el.feedbackRemarksRow) el.feedbackRemarksRow.style.display = 'none';
    if (el.feedbackRetrainStatus) el.feedbackRetrainStatus.style.display = 'none';
    this._feedbackRating = null;
    this._feedbackSubmitted = false;
    if (el.modal) el.modal.style.display = 'none';
    this._state.currentMeal = null;
  }

  /**
   * Submit AI detection feedback (thumbs up / thumbs down + remarks) for model evaluation and continuous retraining.
   * @param {string} rating - 'thumbs_up' or 'thumbs_down'
   * @param {boolean} explicitSubmit - true if triggered by Retrain/Submit button
   */
  async submitAiFeedback(rating, explicitSubmit = false) {
    const el = this.elements;
    if (!this._state.currentMeal) return;

    const remarks = el.feedbackRemarksInput ? el.feedbackRemarksInput.value.trim() : '';
    const mealLogId = this._state.currentMeal.id || null;
    const dishName = this._state.currentMeal.dishName || 'Indian Meal';
    const detectedByModel = this._state.currentMeal.detectedByModel || 'gemini-2.5-flash';
    const confidenceScore = this._state.currentMeal.overallConfidenceScore || 0.88;
    const items = (this._state.currentMeal.identifiedItems || []).map(i => ({
      name: i.name,
      hindiOrRegionalName: i.hindiOrRegionalName || i.name,
      estimatedPortion: i.estimatedPortion || '1 Katori',
      grams: i.grams || 100,
      calories: i.calories || 100,
      proteinGrams: i.proteinGrams || 5,
      carbsGrams: i.carbsGrams || 15,
      fatGrams: i.fatGrams || 3,
      fiberGrams: i.fiberGrams || 2,
      sodiumMg: i.sodiumMg || 100,
      cookingMediumEstimate: i.cookingMediumEstimate || 'Home cooking',
      confidenceScore: i.confidenceScore || 0.85
    }));

    if (el.btnSubmitFeedback && explicitSubmit) {
      el.btnSubmitFeedback.disabled = true;
      el.btnSubmitFeedback.textContent = '⚡ Retraining...';
    }

    try {
      const payload = {
        userId: this._state.userId || 'user-default',
        mealLogId,
        dishName,
        detectedByModel,
        confidenceScore,
        rating,
        remarks: remarks || null,
        items
      };

      const data = await this._meals.submitAiFeedback(payload);
      this._feedbackSubmitted = true;

      // Update feedback retrain status banner
      if (el.feedbackRetrainStatus) {
        el.feedbackRetrainStatus.style.display = 'block';
        el.feedbackRetrainStatus.className = data.retrained
          ? 'feedback-retrain-status retrain-success'
          : 'feedback-retrain-status retrain-info';
        el.feedbackRetrainStatus.innerHTML = `
          <span>${data.retrained ? '🧠' : 'ℹ️'}</span>
          <span>${data.message || (rating === 'thumbs_up' ? 'Thank you! AI accuracy affirmed.' : 'Feedback recorded.')}</span>
        `;
      }

      // If model retrained an item live, update the item list!
      if (data.retrained && data.updatedItemEstimate && this._state.currentMeal.identifiedItems) {
        const origTarget = (data.originalDetectedDish || '').toLowerCase();
        let targetItem = null;
        if (origTarget) {
          targetItem = this._state.currentMeal.identifiedItems.find(i =>
            i.name.toLowerCase().includes(origTarget) ||
            (i.originalDetection && i.originalDetection.toLowerCase().includes(origTarget))
          );
        }
        if (!targetItem && this._state.currentMeal.identifiedItems.length > 0) {
          // If no specific item matched by name, check if any item is not roti/rice
          targetItem = this._state.currentMeal.identifiedItems.find(i => 
            !i.name.toLowerCase().includes('roti') && 
            !i.name.toLowerCase().includes('chapati') && 
            !i.name.toLowerCase().includes('phulka') &&
            !i.name.toLowerCase().includes('rice')
          ) || this._state.currentMeal.identifiedItems[0];
        }

        if (targetItem) {
          const est = data.updatedItemEstimate;
          const oldItemName = targetItem.name;
          targetItem.name = est.name || data.correctedDish;
          targetItem.hindiOrRegionalName = est.hindiOrRegionalName || est.name;
          targetItem.estimatedPortion = est.estimatedPortion || targetItem.estimatedPortion;
          targetItem.calories = est.calories;
          targetItem.proteinGrams = est.proteinGrams;
          targetItem.carbsGrams = est.carbsGrams;
          targetItem.fatGrams = est.fatGrams;
          targetItem.fiberGrams = est.fiberGrams;
          targetItem.sodiumMg = est.sodiumMg;
          targetItem.isAiEstimated = true;

          // Update dish title if matching
          if (this._state.currentMeal.dishName && oldItemName && targetItem.name) {
            let cleanDish = this._state.currentMeal.dishName.replace(/\s*\([~≈]?\d+\s*kcal\)/gi, '').trim();
            const escapedOld = oldItemName.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
            const regex = new RegExp(escapedOld, 'i');
            if (regex.test(cleanDish)) {
              cleanDish = cleanDish.replace(regex, targetItem.name);
            }
            this._state.currentMeal.dishName = cleanDish;
          }

          this.renderItems();
          this.recalculateTotals();
        }
      }

      this._toast.show({
        title: data.retrained ? '🧠 Model Retrained!' : (rating === 'thumbs_up' ? '👍 Feedback Saved' : 'Feedback Received'),
        message: data.message || 'Continuous learning record updated.'
      });

    } catch (err) {
      console.error('Error submitting AI feedback:', err);
      if (explicitSubmit) {
        this._toast.show({
          title: 'Feedback Notice',
          message: 'Could not record feedback right now.'
        });
      }
    } finally {
      if (el.btnSubmitFeedback && explicitSubmit) {
        el.btnSubmitFeedback.disabled = false;
        el.btnSubmitFeedback.textContent = rating === 'thumbs_up' ? '👍 Save Feedback' : '⚡ Retrain AI';
      }
    }
  }

  renderItems() {
    const el = this.elements;
    if (!this._state.currentMeal || !this._state.currentMeal.identifiedItems || !el.itemsList) return;

    el.itemsList.innerHTML = this._state.currentMeal.identifiedItems.map((item, idx) => {
      const isCorrected = item.originalDetection &&
        !item.originalDetection.toLowerCase().includes('added by') &&
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

    // Dynamically update clinical dietitian advice in real-time
    this.updateDietitianAdvice();
  }

  updateDietitianAdvice() {
    const el = this.elements;
    if (!this._state.currentMeal || !el.dietitianAdvice) return;

    const items = this._state.currentMeal.identifiedItems || [];
    const advice = generateDietitianAdvice(items, {
      addedGhee: this._state.addedGhee || 0,
      addedTadka: this._state.addedTadka || 0,
      mealType: this._state.currentMeal.mealType || 'Lunch'
    });

    this._state.currentMeal.dietitianAdvice = advice;
    el.dietitianAdvice.textContent = advice;

    // Trigger subtle visual pulse on container
    const box = el.dietitianAdvice.closest('.review-dietitian-box');
    if (box) {
      box.classList.remove('advice-updated');
      void box.offsetWidth;
      box.classList.add('advice-updated');
      setTimeout(() => box.classList.remove('advice-updated'), 700);
    }
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
        photoUri: this._state.currentMeal.photoUrl || this._state.currentMeal.photoUri || null,
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
        dietitianAdvice: this._state.currentMeal.dietitianAdvice,
        aiFeedbackRating: this._feedbackRating || null,
        aiFeedbackRemarks: el.feedbackRemarksInput ? (el.feedbackRemarksInput.value.trim() || null) : null
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
