import { estimateIndianFoodNutrition, estimateFoodNutritionWithAi, generateDietitianAdvice, scaleNutritionByPortion } from '../services/nutrition-estimator.js?v=1.3.6';

/**
 * ReviewModalController
 * Manages AI Food Detection Review, interactive portion steppers, ghee/tadka adjustments,
 * item renaming/corrections, portion scaling, and continuous model retraining.
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
    this._isEditing = false;
    this._editingMealId = null;

    this._bindEvents();

    // Listen for meal analyzed event from MealLogger
    this._bus.on('meal:analyzed', (analysis) => this.open(analysis));
    this._bus.on('meal:edit', (meal) => this.openForEdit(meal));
  }

  get elements() {
    return {
      modal: document.getElementById('review-modal'),
      mealType: document.getElementById('review-meal-type'),
      dishName: document.getElementById('review-dish-name'),
      dishNameInput: document.getElementById('review-dish-name-input'),
      dishKcalBadge: document.getElementById('review-dish-kcal-badge'),
      logTimeInput: document.getElementById('review-log-time-input'),
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
      // Review Modal Food Search by Text Box
      reviewSearchInput: document.getElementById('review-search-input'),
      btnReviewSearchAi: document.getElementById('btn-review-search-ai'),
      // AI Feedback & Continuous Retraining Elements
      btnFeedbackUp: document.getElementById('btn-feedback-up'),
      btnFeedbackDown: document.getElementById('btn-feedback-down'),
      feedbackRemarksRow: document.getElementById('feedback-remarks-row'),
      feedbackRemarksInput: document.getElementById('feedback-remarks-input'),
      btnSubmitFeedback: document.getElementById('btn-submit-feedback'),
      feedbackRetrainStatus: document.getElementById('feedback-retrain-status'),
      // Top Aggregated Nutrition Bar Elements
      macroSummaryBar: document.getElementById('review-macro-summary-bar'),
      totalKcal: document.getElementById('review-total-kcal'),
      totalProtein: document.getElementById('review-total-protein'),
      totalCarbs: document.getElementById('review-total-carbs'),
      totalFat: document.getElementById('review-total-fat'),
      totalFiber: document.getElementById('review-total-fiber'),
      totalSugar: document.getElementById('review-total-sugar')
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
    if (el.mealPhoto) {
      el.mealPhoto.addEventListener('error', () => {
        el.mealPhoto.src = '/assets/placeholder-meal.svg';
        const caption = el.photoWrapper?.querySelector('.review-photo-caption span');
        if (caption) caption.textContent = 'Plate preview (image unavailable)';
      });
    }
    if (el.lightboxImg) {
      el.lightboxImg.addEventListener('error', () => {
        el.lightboxImg.src = '/assets/placeholder-meal.svg';
      });
    }

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
          const autoHint = document.getElementById('meal-time-auto-hint');
          if (autoHint) {
            autoHint.textContent = 'Manually selected';
          }
          this.recalculateTotals();
        }
      });
    });

    // Editable Dish Name Input Handler
    if (el.dishNameInput) {
      el.dishNameInput.addEventListener('input', () => {
        const val = el.dishNameInput.value.trim();
        this._hasUserRenamedTitle = !!val;
        const fallbackTitle = this.synthesizeMealDishName(this._state.currentMeal?.identifiedItems) || 'Homestyle Indian Meal';
        const finalTitle = val || fallbackTitle;
        if (this._state.currentMeal) {
          this._state.currentMeal.dishName = finalTitle;
        }
        if (el.dishName) {
          el.dishName.textContent = finalTitle;
        }
      });
    }

    // Meal Consumption Date & Time Input Handler
    if (el.logTimeInput) {
      el.logTimeInput.addEventListener('change', () => {
        if (this._state.currentMeal && el.logTimeInput.value) {
          this._state.currentMeal.loggedAt = new Date(el.logTimeInput.value).toISOString();
        }
      });
    }

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
          sugarGrams: 2.0,
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

    // Review Modal Food Search by Text Box (same as Instant Logger Smart Search)
    if (el.btnReviewSearchAi) {
      el.btnReviewSearchAi.addEventListener('click', () => this.handleReviewTextSearch());
    }
    if (el.reviewSearchInput) {
      el.reviewSearchInput.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
          e.preventDefault();
          this.handleReviewTextSearch();
        }
      });
    }

    document.querySelectorAll('.review-search-chip').forEach(chip => {
      chip.addEventListener('click', () => {
        const query = chip.dataset.query;
        if (query) {
          if (el.reviewSearchInput) el.reviewSearchInput.value = query;
          this.handleReviewTextSearch(query);
        }
      });
    });

    // Modal background click
    if (el.modal) {
      el.modal.addEventListener('click', (e) => {
        if (e.target === el.modal) this.close();
      });
    }
  }

  /**
   * Determine meal category based on current local clock time in user's configured timezone.
   * @param {string} [tz] Timezone identifier, e.g. 'Asia/Kolkata'
   * @returns {'Breakfast'|'Lunch'|'Snack'|'Dinner'}
   */
  detectMealTypeByTime(tz = this._state.userTimezone || 'Asia/Kolkata') {
    let hour, minute;
    try {
      const formatter = new Intl.DateTimeFormat('en-US', {
        timeZone: tz,
        hour: 'numeric',
        minute: 'numeric',
        hour12: false
      });
      const parts = formatter.formatToParts(new Date());
      hour = parseInt(parts.find(p => p.type === 'hour')?.value || '0', 10);
      minute = parseInt(parts.find(p => p.type === 'minute')?.value || '0', 10);
      if (hour === 24) hour = 0;
    } catch {
      const now = new Date();
      hour = now.getHours();
      minute = now.getMinutes();
    }

    const decimalTime = hour + (minute / 60.0);
    if (decimalTime >= 5.0 && decimalTime < 11.5) return 'Breakfast';
    if (decimalTime >= 11.5 && decimalTime < 16.0) return 'Lunch';
    if (decimalTime >= 16.0 && decimalTime < 19.5) return 'Snack';
    return 'Dinner';
  }

  /**
   * Format clock time for auto hint.
   * @param {string} [tz]
   * @returns {string} e.g. "01:15 PM"
   */
  getClockTimeString(tz = this._state.userTimezone || 'Asia/Kolkata') {
    try {
      return new Intl.DateTimeFormat('en-US', {
        timeZone: tz,
        hour: '2-digit',
        minute: '2-digit',
        hour12: true
      }).format(new Date());
    } catch {
      return new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    }
  }

  /**
   * Converts Date to local YYYY-MM-DDTHH:mm string for datetime-local input.
   * @param {Date} date
   * @returns {string}
   */
  _toLocalIsoString(date) {
    if (!date || isNaN(date.getTime())) date = new Date();
    const pad = (n) => String(n).padStart(2, '0');
    const year = date.getFullYear();
    const month = pad(date.getMonth() + 1);
    const day = pad(date.getDate());
    const hours = pad(date.getHours());
    const minutes = pad(date.getMinutes());
    return `${year}-${month}-${day}T${hours}:${minutes}`;
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

    const currentMealType = analysis.mealType || this.detectMealTypeByTime();
    this._state.currentMeal.mealType = currentMealType;

    const typeIcons = { 'Breakfast': '🌅', 'Lunch': '☀️', 'Snack': '☕', 'Dinner': '🌙' };
    if (el.mealType) {
      el.mealType.textContent = `${typeIcons[currentMealType] || '☀️'} ${currentMealType} Review & Correction`;
    }
    
    // Fill title from AI detection, or synthesize from food items if missing/generic
    let initialTitle = (analysis.dishName || '').trim();
    if (!initialTitle || initialTitle.toLowerCase() === 'custom indian meal' || initialTitle.toLowerCase() === 'indian meal') {
      initialTitle = this.synthesizeMealDishName(analysis.identifiedItems) || 'Homestyle Indian Meal';
    }
    
    this._state.currentMeal.dishName = initialTitle;
    this._hasUserRenamedTitle = false;

    if (el.dishNameInput) {
      el.dishNameInput.value = initialTitle;
    }
    if (el.dishName) {
      el.dishName.textContent = initialTitle;
    }

    // Set meal consumption date & time (defaults to current date & time)
    const initialLogDate = analysis.loggedAt ? new Date(analysis.loggedAt) : new Date();
    if (el.logTimeInput) {
      el.logTimeInput.value = this._toLocalIsoString(initialLogDate);
    }
    this._state.currentMeal.loggedAt = initialLogDate.toISOString();

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
      const timeStr = analysis.clockTimeStr || this.getClockTimeString();
      autoHint.textContent = `Auto-detected (${timeStr})`;
    }

    // Retain original detection name on each item to track user training, and ensure fiber/sugar exist
    if (analysis.identifiedItems) {
      analysis.identifiedItems.forEach(i => {
        if (!i.originalDetection) i.originalDetection = i.name;
        if (i.fiberGrams === undefined || i.fiberGrams === null || i.fiberGrams === 0) {
          const est = estimateIndianFoodNutrition(i.name, i.estimatedPortion);
          i.fiberGrams = est?.fiberGrams ?? 2.0;
        }
        if (i.sugarGrams === undefined || i.sugarGrams === null || i.sugarGrams === 0) {
          const est = estimateIndianFoodNutrition(i.name, i.estimatedPortion);
          i.sugarGrams = est?.sugarGrams ?? 1.5;
        }
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
    this.recalculateTotals();

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
    this._isEditing = false;
    this._editingMealId = null;
    if (el.btnConfirm) {
      el.btnConfirm.disabled = false;
      el.btnConfirm.textContent = 'Looks Great! Log Meal 🎉';
    }
    if (el.modal) el.modal.style.display = 'none';
    this._state.currentMeal = null;
  }

  /**
   * Open review modal to edit an existing logged meal.
   * @param {Object} meal
   */
  openForEdit(meal) {
    if (!meal) return;
    this._isEditing = true;
    this._editingMealId = meal.id;

    const mealTypeNames = ['Breakfast', 'Lunch', 'Snack', 'Dinner'];
    const typeIndex = typeof meal.mealType === 'number' ? meal.mealType : 1;
    const currentMealType = mealTypeNames[typeIndex] || meal.mealType || 'Lunch';

    this._state.addedGhee = meal.addedGheeKcal || 0;
    this._state.addedTadka = meal.addedTadkaKcal || 0;

    const el = this.elements;
    if (el.chipGhee) el.chipGhee.classList.toggle('active', this._state.addedGhee > 0);
    if (el.chipTadka) el.chipTadka.classList.toggle('active', this._state.addedTadka > 0);
    if (el.chipOilfree) el.chipOilfree.classList.remove('active');

    const rawItems = meal.items || meal.Items || [];
    const identifiedItems = rawItems.map(i => ({
      id: i.id || i.Id,
      mealLogId: i.mealLogId || i.MealLogId || meal.id,
      name: i.name || i.Name,
      originalDetection: i.originalDetection || i.OriginalDetection || i.name || i.Name,
      hindiOrRegionalName: i.hindiOrRegionalName || i.HindiOrRegionalName || i.name || i.Name,
      estimatedPortion: i.estimatedPortion || i.EstimatedPortion || '1 Portion',
      quantity: i.quantity || i.Quantity || 1,
      grams: i.grams || i.Grams || 100,
      calories: i.calories !== undefined ? i.calories : (i.Calories || 0),
      proteinGrams: i.proteinGrams !== undefined ? i.proteinGrams : (i.ProteinGrams || 0),
      carbsGrams: i.carbsGrams !== undefined ? i.carbsGrams : (i.CarbsGrams || 0),
      fatGrams: i.fatGrams !== undefined ? i.fatGrams : (i.FatGrams || 0),
      fiberGrams: i.fiberGrams !== undefined ? i.fiberGrams : (i.FiberGrams !== undefined ? i.FiberGrams : 2.0),
      sugarGrams: i.sugarGrams !== undefined ? i.sugarGrams : (i.SugarGrams !== undefined ? i.SugarGrams : 1.5),
      sodiumMg: i.sodiumMg !== undefined ? i.sodiumMg : (i.SodiumMg !== undefined ? i.SodiumMg : 100),
      cookingMediumEstimate: i.cookingMediumEstimate || i.CookingMediumEstimate || 'Standard Home Cooking'
    }));

    let existingTitle = meal.dishName || meal.DishName || '';
    if (!existingTitle || existingTitle.toLowerCase() === 'custom indian meal' || existingTitle.toLowerCase() === 'indian meal') {
      existingTitle = this.synthesizeMealDishName(identifiedItems) || 'Homestyle Indian Meal';
    }
    this._hasUserRenamedTitle = true;
    this._state.currentMeal = {
      id: meal.id,
      userId: meal.userId || this._state.userId,
      dishName: existingTitle,
      mealType: currentMealType,
      loggedAt: meal.loggedAt || meal.LoggedAt || new Date().toISOString(),
      photoUrl: meal.photoUri || meal.PhotoUri || null,
      overallConfidenceScore: meal.overallConfidenceScore || meal.OverallConfidenceScore || 0.88,
      identifiedItems,
      whoComplianceFlags: meal.whoComplianceFlags || meal.WhoComplianceFlags || [],
      medicationWarnings: meal.medicationWarnings || meal.MedicationWarnings || [],
      dietitianAdvice: meal.dietitianAdvice || meal.DietitianAdvice || 'Wholesome homestyle preparation adhering to ICMR-NIN guidelines.'
    };

    const d = new Date(this._state.currentMeal.loggedAt);
    const dateFormatted = !isNaN(d)
      ? d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })
      : '';

    if (el.mealType) {
      el.mealType.textContent = `✏️ Edit ${currentMealType} (${dateFormatted})`;
    }
    if (el.dishNameInput) {
      el.dishNameInput.value = existingTitle;
    }
    if (el.dishName) {
      el.dishName.textContent = existingTitle;
    }

    // Set meal consumption time for edit (populated by default from existing loggedAt)
    const editMealDate = this._state.currentMeal.loggedAt ? new Date(this._state.currentMeal.loggedAt) : new Date();
    if (el.logTimeInput) {
      el.logTimeInput.value = this._toLocalIsoString(editMealDate);
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
      autoHint.textContent = `Editing · ${dateFormatted}`;
    }

    const confPct = Math.round((this._state.currentMeal.overallConfidenceScore || 0.88) * 100);
    if (el.confidenceBadge) {
      el.confidenceBadge.textContent = `✓ ${confPct}% Confidence`;
      el.confidenceBadge.className = 'confidence-badge confidence-pass';
    }

    if (el.modelBadge && el.modelName) {
      el.modelName.textContent = 'Diet Dost Clinical Engine';
      el.modelBadge.style.display = 'inline-flex';
    }

    const photoUrl = this._state.currentMeal.photoUrl;
    if (photoUrl) {
      if (el.mealPhoto) el.mealPhoto.src = photoUrl;
      if (el.photoContainer) el.photoContainer.style.display = 'block';
      if (el.bodyLayout) el.bodyLayout.classList.remove('no-photo');
    } else {
      if (el.photoContainer) el.photoContainer.style.display = 'none';
      if (el.mealPhoto) el.mealPhoto.src = '';
      if (el.bodyLayout) el.bodyLayout.classList.add('no-photo');
    }

    // Reset AI feedback
    this._feedbackRating = meal.aiFeedbackRating || null;
    this._feedbackSubmitted = false;
    if (el.feedbackRemarksRow) el.feedbackRemarksRow.style.display = 'none';
    if (el.feedbackRemarksInput) el.feedbackRemarksInput.value = meal.aiFeedbackRemarks || '';
    if (el.feedbackRetrainStatus) el.feedbackRetrainStatus.style.display = 'none';

    // Change button text to indicate update mode
    if (el.btnConfirm) {
      el.btnConfirm.disabled = false;
      el.btnConfirm.textContent = '💾 Save Changes ✨';
    }

    this.renderItems();
    this.renderFlags();
    this.recalculateTotals();

    if (el.dietitianAdvice) {
      el.dietitianAdvice.textContent = this._state.currentMeal.dietitianAdvice;
    }

    if (el.modal) el.modal.style.display = 'flex';
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
      sugarGrams: i.sugarGrams || 1.5,
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
          targetItem.sugarGrams = est.sugarGrams;
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

  /**
   * Performs textual food AI search via the review modal text box.
   * Leverages the same AI text analysis agent as the instant meal logger text box.
   * @param {string|null} [customQuery=null]
   */
  async handleReviewTextSearch(customQuery = null) {
    const el = this.elements;
    const query = (customQuery !== null ? customQuery : (el.reviewSearchInput ? el.reviewSearchInput.value : '')).trim();
    if (!query) return;

    if (el.btnReviewSearchAi) {
      el.btnReviewSearchAi.disabled = true;
      el.btnReviewSearchAi.textContent = '⚡ Searching...';
    }

    try {
      const mealType = this._state.currentMeal?.mealType || this.detectMealTypeByTime();
      const userId = this._state.userId || 'user-default';

      // Call textual meal AI search (same as meal search by text box option)
      const data = await this._meals.analyzeMealText(query, userId, mealType);
      const analysis = data?.analysis;

      if (analysis && analysis.identifiedItems && analysis.identifiedItems.length > 0) {
        if (!this._state.currentMeal) {
          this._state.currentMeal = { identifiedItems: [] };
        }
        if (!this._state.currentMeal.identifiedItems) {
          this._state.currentMeal.identifiedItems = [];
        }

        analysis.identifiedItems.forEach(item => {
          this._state.currentMeal.identifiedItems.push({
            name: item.name,
            originalDetection: item.originalDetection || item.name,
            hindiOrRegionalName: item.hindiOrRegionalName || item.name,
            estimatedPortion: item.estimatedPortion || '1 Portion',
            quantity: item.quantity || 1,
            grams: item.grams || 100,
            calories: Math.round(item.calories || 0),
            proteinGrams: Number((item.proteinGrams || 0).toFixed(1)),
            carbsGrams: Number((item.carbsGrams || 0).toFixed(1)),
            fatGrams: Number((item.fatGrams || 0).toFixed(1)),
            fiberGrams: Number((item.fiberGrams ?? 2.0).toFixed(1)),
            sugarGrams: Number((item.sugarGrams ?? 1.5).toFixed(1)),
            sodiumMg: Math.round(item.sodiumMg || 0),
            cookingMediumEstimate: item.cookingMediumEstimate || 'Standard Home Cooking',
            isAiEstimated: true
          });
        });

        if (el.reviewSearchInput) el.reviewSearchInput.value = '';

        this.renderItems();
        this.recalculateTotals();

        const names = analysis.identifiedItems.map(i => i.name).join(', ');
        this._toast.show({
          title: '🤖 AI Food Added!',
          message: `Added ${names} with verified clinical nutrition.`
        });
      } else {
        // Fallback to single item estimation
        const fallback = await estimateFoodNutritionWithAi(query, null, {
          forceRefresh: true,
          userId,
          mealType
        });
        if (fallback) {
          if (!this._state.currentMeal) this._state.currentMeal = { identifiedItems: [] };
          if (!this._state.currentMeal.identifiedItems) this._state.currentMeal.identifiedItems = [];

          this._state.currentMeal.identifiedItems.push({
            name: fallback.name || query,
            originalDetection: query,
            hindiOrRegionalName: fallback.hindiName || query,
            estimatedPortion: fallback.portion || '1 Portion',
            quantity: 1,
            grams: fallback.grams || 100,
            calories: Math.round(fallback.calories || 100),
            proteinGrams: fallback.proteinGrams || 3,
            carbsGrams: fallback.carbsGrams || 15,
            fatGrams: fallback.fatGrams || 4,
            fiberGrams: fallback.fiberGrams || 2,
            sugarGrams: fallback.sugarGrams || 1.5,
            sodiumMg: fallback.sodiumMg || 120,
            cookingMediumEstimate: fallback.cookingMedium || 'Home cooking',
            isAiEstimated: true
          });

          if (el.reviewSearchInput) el.reviewSearchInput.value = '';
          this.renderItems();
          this.recalculateTotals();

          this._toast.show({
            title: '🤖 Food Item Added!',
            message: `Added '${fallback.name || query}' (${Math.round(fallback.calories)} kcal).`
          });
        }
      }
    } catch (err) {
      console.error('Error searching food item with AI:', err);
      this._toast.show({
        title: 'Search Error',
        message: err.message || 'Could not perform AI food search.'
      });
    } finally {
      if (el.btnReviewSearchAi) {
        el.btnReviewSearchAi.disabled = false;
        el.btnReviewSearchAi.textContent = '⚡ AI Search';
      }
    }
  }

  /**
   * Explicitly triggers textual food AI search for a single food item row.
   * @param {number} idx
   */
  async searchItemWithAi(idx) {
    if (!this._state.currentMeal || !this._state.currentMeal.identifiedItems[idx]) return;
    const item = this._state.currentMeal.identifiedItems[idx];
    const name = (item.name || '').trim();
    if (!name) return;

    item.isAiSearching = true;
    this.renderItems();

    try {
      const aiResult = await estimateFoodNutritionWithAi(name, item.estimatedPortion, {
        forceRefresh: true,
        userId: this._state.userId || 'user-default',
        mealType: this._state.currentMeal?.mealType
      });

      if (aiResult) {
        item.calories = aiResult.calories;
        item.proteinGrams = aiResult.proteinGrams;
        item.carbsGrams = aiResult.carbsGrams;
        item.fatGrams = aiResult.fatGrams;
        item.fiberGrams = aiResult.fiberGrams;
        item.sugarGrams = aiResult.sugarGrams;
        item.sodiumMg = aiResult.sodiumMg;
        if (aiResult.grams) item.grams = aiResult.grams;
        if (aiResult.portion) item.estimatedPortion = aiResult.portion;
        if (aiResult.hindiName) item.hindiOrRegionalName = aiResult.hindiName;
        if (aiResult.cookingMedium) item.cookingMediumEstimate = aiResult.cookingMedium;
        item.isAiEstimated = true;

        this._toast.show({
          title: '🤖 AI Nutrition Updated!',
          message: `${aiResult.name}: ${Math.round(item.calories)} kcal, ${item.proteinGrams}g Protein.`
        });
      }
    } catch (e) {
      console.error('Error in searchItemWithAi:', e);
    } finally {
      item.isAiSearching = false;
      this.renderItems();
      this.recalculateTotals();
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
      const totalItemFiber = Math.round((item.fiberGrams || 2.5) * qty * 10) / 10;
      const totalItemSugar = Math.round((item.sugarGrams || 1.5) * qty * 10) / 10;

      return `
        <div class="item-row-editable">
          <div class="item-edit-left">
            <div class="item-inputs-row">
              <input type="text" class="item-name-input" value="${item.name}" 
                     placeholder="e.g. Bhindi Masala, Palak Paneer, Moong Dal"
                     title="Click to edit or rename this dish. Nutrition will auto-update with AI."
                     data-idx="${idx}" />
              <div class="portion-input-wrap">
                <span class="portion-icon" title="Quantity / Portion">📏</span>
                <input type="text" class="item-portion-input" value="${item.estimatedPortion || '1 Katori'}" 
                       placeholder="Portion (e.g. 1.5 Cup, 1 Katori, 5-6 Slices)"
                       title="Update quantity detection. Nutrition will auto-update with AI."
                       data-idx="${idx}" />
              </div>
              <button type="button" class="btn-item-ai-search" data-idx="${idx}" title="⚡ Search AI for updated nutrition of this dish">
                ⚡ AI
              </button>
            </div>
            <div style="display: flex; gap: 8px; align-items: center; font-size: 0.72rem; color: var(--text-muted); flex-wrap: wrap; margin-top: 3px;">
              <span class="tabular" style="font-weight: 600; color: var(--text-primary);">${totalItemKcal} kcal</span>
              <span>·</span>
              <span class="tabular" style="color: #38bdf8; font-weight: 600;">${totalItemProtein}g Protein</span>
              <span>·</span>
              <span class="tabular" style="color: #fbbf24;">${totalItemCarbs}g Carbs</span>
              <span>·</span>
              <span class="tabular" style="color: #f87171;">${totalItemFat}g Fat</span>
              <span>·</span>
              <span class="tabular" style="color: #34d399;">${totalItemFiber}g Fiber</span>
              <span>·</span>
              <span class="tabular" style="color: #f472b6;">${totalItemSugar}g Sugar</span>
              ${item.isAiSearching ? `
                <span class="item-ai-searching-badge">
                  <span class="spinner-mini">⏳</span> 🤖 AI Searching...
                </span>
              ` : (item.isAiEstimated ? `
                <span class="item-ai-verified-badge" title="Nutrition computed and verified via Google AI Gemini">
                  ✓ AI-Verified
                </span>
              ` : '')}
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

    // Attach listeners to items: Name input
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

    // Attach listeners to items: Portion quantity input
    el.itemsList.querySelectorAll('.item-portion-input').forEach(input => {
      input.addEventListener('change', (e) => {
        const idx = parseInt(e.target.dataset.idx);
        this.updateItemPortion(idx, e.target.value);
      });
      input.addEventListener('blur', (e) => {
        const idx = parseInt(e.target.dataset.idx);
        if (this._state.currentMeal && this._state.currentMeal.identifiedItems[idx]) {
          const currentPortion = (this._state.currentMeal.identifiedItems[idx].estimatedPortion || '').trim().toLowerCase();
          if (currentPortion !== e.target.value.trim().toLowerCase()) {
            this.updateItemPortion(idx, e.target.value);
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

    // Attach listeners to per-item AI Search buttons
    el.itemsList.querySelectorAll('.btn-item-ai-search').forEach(btn => {
      btn.addEventListener('click', (e) => {
        const idx = parseInt(e.currentTarget.dataset.idx);
        this.searchItemWithAi(idx);
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
      item.sugarGrams = baseline.sugarGrams;
      item.sodiumMg = baseline.sodiumMg;
      item.isAiEstimated = false;
      if (!item.estimatedPortion && baseline.portion) item.estimatedPortion = baseline.portion;
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

    // 3. Mark loading & recalculate baseline
    item.isAiSearching = true;
    this.renderItems();
    this.recalculateTotals();

    // 4. Asynchronously query AI agent (Google Gemini / Clinical NLP) with textual meal search
    try {
      const aiResult = await estimateFoodNutritionWithAi(trimmed, item.estimatedPortion, {
        forceRefresh: true,
        userId: this._state.userId || 'user-default',
        mealType: this._state.currentMeal?.mealType
      });
      if (aiResult) {
        item.calories = aiResult.calories;
        item.proteinGrams = aiResult.proteinGrams;
        item.carbsGrams = aiResult.carbsGrams;
        item.fatGrams = aiResult.fatGrams;
        item.fiberGrams = aiResult.fiberGrams;
        item.sugarGrams = aiResult.sugarGrams;
        item.sodiumMg = aiResult.sodiumMg;
        if (aiResult.grams) item.grams = aiResult.grams;
        if (aiResult.hindiName) item.hindiOrRegionalName = aiResult.hindiName;
        if (aiResult.cookingMedium) item.cookingMediumEstimate = aiResult.cookingMedium;
        item.isAiEstimated = true;

        this._toast.show({
          title: '🤖 AI Nutrition Refined!',
          message: `${aiResult.name}: ${Math.round(item.calories)} kcal, ${item.proteinGrams}g Protein.`
        });
      }
    } catch (_) {
      // Hardcoded ICMR-NIN baseline already applied
    } finally {
      item.isAiSearching = false;
      this.renderItems();
      this.recalculateTotals();
    }
  }

  async updateItemPortion(idx, newPortion) {
    if (!this._state.currentMeal || !this._state.currentMeal.identifiedItems[idx]) return;
    const trimmed = (newPortion || '').trim();
    if (!trimmed) return;

    const item = this._state.currentMeal.identifiedItems[idx];
    item.estimatedPortion = trimmed;

    // 1. Instant client-side portion scaling (Zero Latency)
    const scaled = scaleNutritionByPortion(item, trimmed);
    item.grams = scaled.grams;
    item.calories = scaled.calories;
    item.proteinGrams = scaled.proteinGrams;
    item.carbsGrams = scaled.carbsGrams;
    item.fatGrams = scaled.fatGrams;
    item.fiberGrams = scaled.fiberGrams;
    item.sugarGrams = scaled.sugarGrams;
    item.sodiumMg = scaled.sodiumMg;

    item.isAiSearching = true;
    this.renderItems();
    this.recalculateTotals();

    // 2. Query AI agent to refine exact nutrition metrics for the updated portion
    try {
      const aiResult = await estimateFoodNutritionWithAi(item.name, trimmed, {
        forceRefresh: true,
        userId: this._state.userId || 'user-default',
        mealType: this._state.currentMeal?.mealType
      });
      if (aiResult) {
        item.calories = aiResult.calories;
        item.proteinGrams = aiResult.proteinGrams;
        item.carbsGrams = aiResult.carbsGrams;
        item.fatGrams = aiResult.fatGrams;
        item.fiberGrams = aiResult.fiberGrams;
        item.sugarGrams = aiResult.sugarGrams;
        item.sodiumMg = aiResult.sodiumMg;
        if (aiResult.grams) item.grams = aiResult.grams;
        item.isAiEstimated = true;
      }
    } catch (_) {
      // Local scaled ICMR-NIN baseline already active
    } finally {
      item.isAiSearching = false;
      this.renderItems();
      this.recalculateTotals();
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
      sugarGrams: 2.0,
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
    if (!this._state.currentMeal) return;

    const items = this._state.currentMeal.identifiedItems || [];
    let baseKcal = 0;
    let baseProtein = 0;
    let baseCarbs = 0;
    let baseFat = 0;
    let baseFiber = 0;
    let baseSugar = 0;
    let baseSodium = 0;

    items.forEach(i => {
      const qty = i.quantity || 1;
      baseKcal += (i.calories || 0) * qty;
      baseProtein += (i.proteinGrams || 0) * qty;
      baseCarbs += (i.carbsGrams || 0) * qty;
      baseFat += (i.fatGrams || 0) * qty;
      baseFiber += (i.fiberGrams || 0) * qty;
      baseSugar += (i.sugarGrams || 0) * qty;
      baseSodium += (i.sodiumMg || 0) * qty;
    });

    const addedGheeKcal = this._state.addedGhee || 0;
    const addedTadkaKcal = this._state.addedTadka || 0;
    const totalKcal = baseKcal + addedGheeKcal + addedTadkaKcal;
    const totalFat = baseFat + (addedGheeKcal + addedTadkaKcal) / 9;
    const totalProtein = baseProtein;
    const totalCarbs = baseCarbs;
    const totalFiber = baseFiber;
    const totalSugar = baseSugar;
    const totalSodium = Math.round(baseSodium);

    if (el.dishKcalBadge) {
      el.dishKcalBadge.textContent = `~${Math.round(totalKcal)} kcal`;
    }

    const fallbackTitle = this.synthesizeMealDishName(this._state.currentMeal?.identifiedItems) || 'Homestyle Indian Meal';
    const currentTitle = (el.dishNameInput && el.dishNameInput.value.trim()) || this._state.currentMeal.dishName || fallbackTitle;
    if (el.dishNameInput && !el.dishNameInput.value.trim()) {
      el.dishNameInput.value = currentTitle;
    }
    if (el.dishName) {
      el.dishName.textContent = `${currentTitle} (~${Math.round(totalKcal)} kcal)`;
    }

    // Update Top Aggregated Nutrition Values Summary Bar
    if (el.totalKcal) {
      el.totalKcal.textContent = `${Math.round(totalKcal)} kcal`;
      this._pulseElement(el.totalKcal);
    }
    if (el.totalProtein) {
      el.totalProtein.textContent = `${totalProtein.toFixed(1)}g`;
      this._pulseElement(el.totalProtein);
    }
    if (el.totalCarbs) {
      el.totalCarbs.textContent = `${totalCarbs.toFixed(1)}g`;
      this._pulseElement(el.totalCarbs);
    }
    if (el.totalFat) {
      el.totalFat.textContent = `${totalFat.toFixed(1)}g`;
      this._pulseElement(el.totalFat);
    }
    if (el.totalFiber) {
      el.totalFiber.textContent = `${totalFiber.toFixed(1)}g`;
      this._pulseElement(el.totalFiber);
    }
    if (el.totalSugar) {
      el.totalSugar.textContent = `${totalSugar.toFixed(1)}g`;
      this._pulseElement(el.totalSugar);
    }

    // Persist in state
    this._state.currentMeal.totalCalories = Math.round(totalKcal);
    this._state.currentMeal.totalProteinGrams = Math.round(totalProtein * 10) / 10;
    this._state.currentMeal.totalCarbsGrams = Math.round(totalCarbs * 10) / 10;
    this._state.currentMeal.totalFatGrams = Math.round(totalFat * 10) / 10;
    this._state.currentMeal.totalFiberGrams = Math.round(totalFiber * 10) / 10;
    this._state.currentMeal.totalSugarGrams = Math.round(totalSugar * 10) / 10;
    this._state.currentMeal.totalSodiumMg = totalSodium;

    // Dynamically evaluate WHO Compliance Flags based on updated nutrition totals
    const dynamicFlags = [];
    if (totalSodium > 800) {
      dynamicFlags.push(`High sodium alert (>800mg in meal: ${totalSodium}mg). ICMR recommends <2,000mg/day.`);
    }
    if (totalSugar > 15) {
      dynamicFlags.push(`Elevated free sugar (>15g in meal: ${totalSugar.toFixed(1)}g). Limit sweet items to manage insulin.`);
    }
    if (totalFat > 35) {
      dynamicFlags.push(`High fat content (${totalFat.toFixed(1)}g). Ensure visible cooking fats are moderated.`);
    }

    // Preserve existing non-metric clinical flags while updating dynamic thresholds
    const existingFlags = (this._state.currentMeal.whoComplianceFlags || []).filter(f =>
      !f.toLowerCase().includes('sodium') &&
      !f.toLowerCase().includes('sugar') &&
      !f.toLowerCase().includes('high fat')
    );
    this._state.currentMeal.whoComplianceFlags = [...existingFlags, ...dynamicFlags];
    this.renderFlags();

    // Dynamically update clinical dietitian advice in real-time
    this.updateDietitianAdvice();
  }

  _pulseElement(elem) {
    if (!elem) return;
    elem.classList.remove('macro-val-pulse');
    void elem.offsetWidth;
    elem.classList.add('macro-val-pulse');
    setTimeout(() => elem.classList.remove('macro-val-pulse'), 350);
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

    // Capture latest header and timing inputs
    if (el.dishNameInput && el.dishNameInput.value.trim()) {
      this._state.currentMeal.dishName = el.dishNameInput.value.trim();
    }
    if (el.logTimeInput && el.logTimeInput.value) {
      this._state.currentMeal.loggedAt = new Date(el.logTimeInput.value).toISOString();
    }

    if (this._isEditing) {
      if (el.btnConfirm) {
        el.btnConfirm.disabled = true;
        el.btnConfirm.textContent = 'Saving Changes...';
      }

      try {
        const mealTypeMap = { 'Breakfast': 0, 'Lunch': 1, 'Snack': 2, 'Dinner': 3 };
        const numericMealType = mealTypeMap[this._state.currentMeal.mealType] !== undefined
          ? mealTypeMap[this._state.currentMeal.mealType]
          : 1;

        const payload = {
          id: this._editingMealId,
          userId: this._state.userId,
          mealType: numericMealType,
          dishName: this._state.currentMeal.dishName,
          loggedAt: this._state.currentMeal.loggedAt || new Date().toISOString(),
          photoUri: this._state.currentMeal.photoUrl || null,
          overallConfidenceScore: this._state.currentMeal.overallConfidenceScore || 0.88,
          addedGheeKcal: this._state.addedGhee || 0,
          addedTadkaKcal: this._state.addedTadka || 0,
          items: this._state.currentMeal.identifiedItems.map(i => ({
            id: i.id || null,
            mealLogId: this._editingMealId,
            name: i.name,
            originalDetection: i.originalDetection || i.name,
            hindiOrRegionalName: i.hindiOrRegionalName || i.name,
            estimatedPortion: i.estimatedPortion || '1 Portion',
            quantity: i.quantity || 1,
            grams: i.grams || 100,
            calories: i.calories || 0,
            proteinGrams: i.proteinGrams || 0,
            carbsGrams: i.carbsGrams || 0,
            fatGrams: i.fatGrams || 0,
            fiberGrams: i.fiberGrams || 0,
            sugarGrams: i.sugarGrams || 0,
            sodiumMg: i.sodiumMg || 0,
            cookingMediumEstimate: i.cookingMediumEstimate || 'Standard Home Cooking'
          })),
          whoComplianceFlags: this._state.currentMeal.whoComplianceFlags || [],
          medicationWarnings: this._state.currentMeal.medicationWarnings || [],
          dietitianAdvice: this._state.currentMeal.dietitianAdvice,
          aiFeedbackRating: this._feedbackRating || null,
          aiFeedbackRemarks: el.feedbackRemarksInput ? (el.feedbackRemarksInput.value.trim() || null) : null
        };

        const data = await this._meals.updateMeal(this._editingMealId, payload);

        if (el.btnConfirm) {
          el.btnConfirm.disabled = false;
          el.btnConfirm.textContent = 'Looks Great! Log Meal 🎉';
        }

        this.close();
        this._confetti.burst();

        // Emit global meal:logged event to trigger Daily HUD & Analytics refresh
        this._bus.emit('meal:logged', data);

        this._toast.show({
          title: 'Meal Updated! ✏️',
          message: `Updated ${this._state.currentMeal?.dishName || 'meal'}! Daily calorie ledger synchronized.`
        });
      } catch (err) {
        if (el.btnConfirm) {
          el.btnConfirm.disabled = false;
          el.btnConfirm.textContent = '💾 Save Changes ✨';
        }
        this._toast.show({
          title: 'Error Updating Meal',
          message: err.message || 'Failed to update meal.'
        });
      }
      return;
    }

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
        loggedAt: this._state.currentMeal.loggedAt || new Date().toISOString(),
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
          sugarGrams: i.sugarGrams || 1.5,
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

  /**
   * Synthesizes a natural, descriptive meal title from the identified food items.
   * @param {Array<Object>} items
   * @returns {string}
   */
  synthesizeMealDishName(items) {
    if (!items || items.length === 0) return 'Homestyle Indian Meal';

    const cleanName = (raw) => {
      if (!raw) return 'Meal';
      let c = raw.replace(/^\d+(?:\.\d+)?\s*(?:Cups?|Portions?|Pieces?|Plates?|Bowls?|Katoris?|Rotis?|Phulkas?|Tbsp|Tsp|g|gms|ml)?\s*/i, '');
      c = c.replace(/\s*\([^)]*\)/g, '').trim();
      return c || raw.trim();
    };

    if (items.length === 1) {
      return cleanName(items[0].name);
    }

    const hasItem = (query) => items.some(i => i.name && i.name.toLowerCase().includes(query.toLowerCase()));

    if (hasItem('tea') || hasItem('chai')) {
      if (items.length === 1) return cleanName(items[0].name);
    }

    if ((hasItem('nuts') || hasItem('almond') || hasItem('walnut') || hasItem('cashew')) &&
        (hasItem('anjeer') || hasItem('fig') || hasItem('date') || hasItem('raisin'))) {
      const nut = cleanName(items.find(i => i.name && /nuts|almond|walnut|cashew/i.test(i.name))?.name);
      const fruit = cleanName(items.find(i => i.name && /anjeer|fig|date|raisin/i.test(i.name))?.name);
      return `${nut} with ${fruit}`;
    }

    if (hasItem('dosa') && hasItem('sambar')) {
      return 'Masala Dosa with Sambar & Chutney';
    }
    if (hasItem('idli') && hasItem('sambar')) {
      return 'Steamed Idlis with Sambar & Chutney';
    }
    if (hasItem('khichdi') && (hasItem('curd') || hasItem('dahi'))) {
      return 'Moong Dal Khichdi with Fresh Curd';
    }
    if (hasItem('poha') && (hasItem('chai') || hasItem('tea'))) {
      return 'Kanda Poha with Masala Chai';
    }
    if (hasItem('rajma') && (hasItem('rice') || hasItem('chawal'))) {
      return 'Rajma Chawal Feast';
    }
    if (hasItem('phulka') || hasItem('roti') || hasItem('chapati')) {
      const subziOrDal = items.find(i => !/phulka|roti|chapati|salad/i.test(i.name));
      if (subziOrDal) {
        return `North Indian Thali (Phulkas & ${cleanName(subziOrDal.name)})`;
      }
      return 'North Indian Phulka Meal';
    }

    const primaryDishes = items.filter(i => !/salad|chutney/i.test(i.name));
    const targetList = primaryDishes.length > 0 ? primaryDishes : items;

    if (targetList.length === 1) {
      return cleanName(targetList[0].name);
    }

    const names = targetList.slice(0, 2).map(i => cleanName(i.name));
    let composite = names.join(' with ');
    if (targetList.length > 2) composite += ' & sides';
    return composite;
  }
}
