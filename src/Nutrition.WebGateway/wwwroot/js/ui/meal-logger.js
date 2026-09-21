/**
 * MealLoggerController
 * Handles AI photo scanning, drag/drop dropzone, text/voice queries, and meal type detection.
 */
export class MealLoggerController {
  /**
   * @param {Object} options
   * @param {import('../services/meals-service.js').MealsService} options.mealsService
   * @param {import('../ui/toast.js').ToastNotificationService} options.toastService
   * @param {import('../core/state.js').AppState} options.appState
   * @param {import('../core/event-bus.js').EventBus} options.eventBus
   */
  constructor({ mealsService, toastService, appState, eventBus }) {
    this._meals = mealsService;
    this._toast = toastService;
    this._state = appState;
    this._bus = eventBus;

    this._bindEvents();
    this._initClockTracker();
  }

  get elements() {
    return {
      dropzone: document.getElementById('photo-dropzone'),
      fileInput: document.getElementById('file-input'),
      scanning: document.getElementById('scanning-state'),
      modeCamera: document.getElementById('btn-mode-camera'),
      modeText: document.getElementById('btn-mode-text'),
      textLoggerBox: document.getElementById('text-logger-box'),
      textInput: document.getElementById('text-input'),
      btnSubmitText: document.getElementById('btn-submit-text'),
      btnSampleThali: document.getElementById('btn-sample-thali'),
      clockBadge: document.getElementById('logger-clock-badge'),
      clockIcon: document.getElementById('logger-clock-icon'),
      clockText: document.getElementById('logger-clock-text')
    };
  }

  _bindEvents() {
    const el = this.elements;

    // Mode Switcher
    if (el.modeCamera && el.modeText) {
      el.modeCamera.addEventListener('click', () => {
        el.modeCamera.classList.add('active');
        el.modeText.classList.remove('active');
        if (el.dropzone) el.dropzone.style.display = 'block';
        if (el.textLoggerBox) el.textLoggerBox.style.display = 'none';
      });

      el.modeText.addEventListener('click', () => {
        el.modeText.classList.add('active');
        el.modeCamera.classList.remove('active');
        if (el.dropzone) el.dropzone.style.display = 'none';
        if (el.textLoggerBox) el.textLoggerBox.style.display = 'flex';
      });
    }

    // Dropzone Click & Drag/Drop
    if (el.dropzone && el.fileInput) {
      el.dropzone.addEventListener('click', () => el.fileInput.click());
      el.dropzone.addEventListener('dragover', (e) => {
        e.preventDefault();
        el.dropzone.classList.add('dragover');
      });
      el.dropzone.addEventListener('dragleave', () => el.dropzone.classList.remove('dragover'));
      el.dropzone.addEventListener('drop', (e) => {
        e.preventDefault();
        el.dropzone.classList.remove('dragover');
        if (e.dataTransfer.files.length > 0) {
          this.handleImageUpload(e.dataTransfer.files[0]);
        }
      });
      el.fileInput.addEventListener('change', (e) => {
        if (e.target.files.length > 0) {
          this.handleImageUpload(e.target.files[0]);
        }
      });
    }

    // Sample Indian Lunch Thali Button
    if (el.btnSampleThali) {
      el.btnSampleThali.addEventListener('click', async (e) => {
        e.stopPropagation();
        try {
          const resp = await fetch('/uploads/meals/sample_lunch_thali.jpg');
          const blob = await resp.blob();
          const sampleFile = new File([blob], 'sample_lunch_thali.jpg', { type: 'image/jpeg' });
          await this.handleImageUpload(sampleFile);
        } catch (err) {
          this._toast.show({
            title: 'Sample Photo Error',
            message: 'Could not load sample meal photo.'
          });
        }
      });
    }

    // Text Submission
    if (el.btnSubmitText) {
      el.btnSubmitText.addEventListener('click', () => this.handleTextAnalyze());
    }
    if (el.textInput) {
      el.textInput.addEventListener('keypress', (e) => {
        if (e.key === 'Enter') this.handleTextAnalyze();
      });
    }

    // Suggestion chips
    document.querySelectorAll('.suggest-chip').forEach(chip => {
      chip.addEventListener('click', () => {
        if (el.textInput) {
          el.textInput.value = chip.dataset.query;
          this.handleTextAnalyze();
        }
      });
    });
  }

  /**
   * Initializes the live clock badge tracking and periodic update interval.
   */
  _initClockTracker() {
    this.updateClockBadge();
    // Update badge every 30 seconds
    this._clockInterval = setInterval(() => this.updateClockBadge(), 30000);
    // Re-evaluate on user profile/timezone updates
    this._bus.on('profile:updated', () => this.updateClockBadge());
  }

  /**
   * Determine meal category based on current local clock time in user's configured timezone.
   * - 05:00 - 11:30: Breakfast
   * - 11:30 - 16:00: Lunch
   * - 16:00 - 19:30: Snack
   * - 19:30 - 05:00: Dinner
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
   * Format clock time for badge & review hints.
   * @param {string} [tz] Timezone identifier
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
   * Updates the live clock badge in the instant logger header.
   */
  updateClockBadge() {
    const el = this.elements;
    if (!el.clockBadge || !el.clockText) return;

    const mealType = this.detectMealTypeByTime();
    const timeStr = this.getClockTimeString();
    const typeIcons = { 'Breakfast': '🌅', 'Lunch': '☀️', 'Snack': '☕', 'Dinner': '🌙' };

    if (el.clockIcon) el.clockIcon.textContent = typeIcons[mealType] || '☀️';
    el.clockText.textContent = `Auto: ${mealType} (${timeStr})`;
    el.clockBadge.title = `Auto-detected ${mealType} based on current clock (${timeStr}, ${this._state.userTimezone || 'Asia/Kolkata'})`;
  }

  /**
   * Handle image upload and AI analysis.
   * @param {File} file
   */
  async handleImageUpload(file) {
    const el = this.elements;
    if (el.dropzone) el.dropzone.style.display = 'none';
    if (el.scanning) el.scanning.style.display = 'block';

    try {
      const localPhotoUrl = URL.createObjectURL(file);
      const detectedMealType = this.detectMealTypeByTime();
      const clockTimeStr = this.getClockTimeString();
      const data = await this._meals.uploadMealImage(file, this._state.userId, detectedMealType, 'North Indian');

      if (el.scanning) el.scanning.style.display = 'none';
      if (el.dropzone) el.dropzone.style.display = 'block';

      if (data.requiresRetake) {
        this.showRetakePrompt(data.confidenceScore, data.advice);
        return;
      }

      if (data.analysis) {
        if (!data.analysis.mealType) {
          data.analysis.mealType = detectedMealType;
        }
        data.analysis.clockTimeStr = clockTimeStr;
        data.analysis.isClockAutoDetected = true;
        // Attach photo URL (server URL if available, otherwise local object URL)
        data.analysis.photoUrl = data.photoUrl || data.analysis.photoUri || localPhotoUrl;

        // Notify ReviewModal to open
        this._bus.emit('meal:analyzed', data.analysis);
      }
    } catch (err) {
      if (el.scanning) el.scanning.style.display = 'none';
      if (el.dropzone) el.dropzone.style.display = 'block';
      this._toast.show({
        title: 'Upload Failed',
        message: err.message || 'Could not analyze meal photo.'
      });
    }
  }

  /**
   * Handle natural language text / voice query analysis.
   */
  async handleTextAnalyze() {
    const el = this.elements;
    const query = el.textInput ? el.textInput.value.trim() : '';
    if (!query) return;

    const detectedMealType = this.detectMealTypeByTime();
    const clockTimeStr = this.getClockTimeString();
    if (el.btnSubmitText) {
      el.btnSubmitText.disabled = true;
      el.btnSubmitText.textContent = 'Parsing...';
    }

    try {
      const data = await this._meals.analyzeMealText(query, this._state.userId, detectedMealType);

      if (el.btnSubmitText) {
        el.btnSubmitText.disabled = false;
        el.btnSubmitText.textContent = 'Analyze Meal';
      }

      if (data && data.analysis) {
        if (!data.analysis.mealType) {
          data.analysis.mealType = detectedMealType;
        }
        data.analysis.clockTimeStr = clockTimeStr;
        data.analysis.isClockAutoDetected = true;
        this._bus.emit('meal:analyzed', data.analysis);
      }
    } catch (err) {
      if (el.btnSubmitText) {
        el.btnSubmitText.disabled = false;
        el.btnSubmitText.textContent = 'Analyze Meal';
      }
      this._toast.show({
        title: 'Parse Error',
        message: err.message || 'Failed to parse meal description.'
      });
    }
  }

  showRetakePrompt(score, advice) {
    alert(
      `📸 Low Visual Confidence (${Math.round(score * 100)}% < 70% required threshold)\n\n${advice}\n\nPlease click OK to retake under better lighting or use Voice / Smart Search.`
    );
  }
}
