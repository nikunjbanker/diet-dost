/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
/**
 * ProfileModalController
 * Manages Zero Assumption Clinical Intake, dual-unit height synchronization,
 * dynamic medication chips, and recalculation of clinical targets.
 */
export class ProfileModalController {
  /**
   * @param {Object} options
   * @param {import('../services/profile-service.js').ProfileService} options.profileService
   * @param {import('../services/medication-service.js').MedicationService} options.medicationService
   * @param {import('../ui/toast.js').ToastNotificationService} options.toastService
   * @param {import('../ui/confetti.js').ConfettiService} options.confettiService
   * @param {import('../core/state.js').AppState} options.appState
   * @param {import('../core/event-bus.js').EventBus} options.eventBus
   */
  constructor({ profileService, medicationService, toastService, confettiService, appState, eventBus }) {
    this._profile = profileService;
    this._meds = medicationService;
    this._toast = toastService;
    this._confetti = confettiService;
    this._state = appState;
    this._bus = eventBus;

    this._bindEvents();
  }

  get elements() {
    return {
      modal: document.getElementById('profile-modal'),
      btnOpen: document.getElementById('btn-open-profile'),
      btnClose: document.getElementById('btn-close-profile'),
      form: document.getElementById('profile-form'),
      btnUnitCm: document.getElementById('btn-unit-cm'),
      btnUnitFt: document.getElementById('btn-unit-ft'),
      heightCmWrapper: document.getElementById('height-cm-wrapper'),
      heightFtWrapper: document.getElementById('height-ft-wrapper'),
      inpHeight: document.getElementById('inp-height'),
      inpHeightFt: document.getElementById('inp-height-ft'),
      inpHeightIn: document.getElementById('inp-height-in'),
      heightHint: document.getElementById('height-conversion-hint'),
      inpName: document.getElementById('inp-name'),
      inpSex: document.getElementById('inp-sex'),
      inpAge: document.getElementById('inp-age'),
      inpWeight: document.getElementById('inp-weight'),
      inpTargetWeight: document.getElementById('inp-target-weight'),
      inpPace: document.getElementById('inp-pace'),
      inpActivity: document.getElementById('inp-activity'),
      inpDietary: document.getElementById('inp-dietary'),
      inpCuisine: document.getElementById('inp-cuisine'),
      inpTimezone: document.getElementById('inp-timezone'),
      timezoneAutoHint: document.getElementById('timezone-auto-hint'),
      inpMedications: document.getElementById('inp-medications'),
      medSuggestions: document.getElementById('med-quick-suggestions'),
      medPromptNote: document.getElementById('med-prompt-note'),
      medPlaceholderHint: document.getElementById('med-placeholder-hint'),
      btnProfileOpenProgress: document.getElementById('btn-profile-open-progress')
    };
  }

  _bindEvents() {
    const el = this.elements;

    if (el.btnOpen) {
      el.btnOpen.addEventListener('click', () => this.open());
    }

    if (el.btnClose) {
      el.btnClose.addEventListener('click', () => this.close());
    }

    if (el.modal) {
      el.modal.addEventListener('click', (e) => {
        if (e.target === el.modal) this.close();
      });
    }

    // Dual Unit Height Controls
    if (el.btnUnitCm && el.btnUnitFt) {
      el.btnUnitCm.addEventListener('click', () => {
        el.btnUnitCm.classList.add('active');
        el.btnUnitFt.classList.remove('active');
        if (el.heightCmWrapper) el.heightCmWrapper.style.display = 'block';
        if (el.heightFtWrapper) el.heightFtWrapper.style.display = 'none';
        this.syncFromCm(parseFloat(el.inpHeight?.value));
      });

      el.btnUnitFt.addEventListener('click', () => {
        el.btnUnitFt.classList.add('active');
        el.btnUnitCm.classList.remove('active');
        if (el.heightCmWrapper) el.heightCmWrapper.style.display = 'none';
        if (el.heightFtWrapper) el.heightFtWrapper.style.display = 'grid';
        this.syncFromCm(parseFloat(el.inpHeight?.value));
      });
    }

    if (el.inpHeight) {
      el.inpHeight.addEventListener('input', () => {
        this.syncFromCm(parseFloat(el.inpHeight.value));
      });
    }

    if (el.inpHeightFt && el.inpHeightIn) {
      const syncFtIn = () => this.syncFromFtIn(el.inpHeightFt.value, el.inpHeightIn.value);
      el.inpHeightFt.addEventListener('input', syncFtIn);
      el.inpHeightIn.addEventListener('input', syncFtIn);
    }

    // Condition Checkboxes for Medications
    document.querySelectorAll('#conditions-list input[type="checkbox"]').forEach(cb => {
      cb.addEventListener('change', () => {
        this.updateMedicationSuggestions(true);
      });
    });

    // Form Submit
    if (el.form) {
      el.form.addEventListener('submit', (e) => this.handleSubmit(e));
    }

    // View Photos Shortcut
    if (el.btnProfileOpenProgress) {
      el.btnProfileOpenProgress.addEventListener('click', () => {
        this.close();
        this._bus.emit('progress:open', 'pane-face-progress');
      });
    }
  }

  async open() {
    const el = this.elements;
    this.syncFromCm(parseFloat(el.inpHeight?.value) || 175);
    this.updateMedicationSuggestions(false);
    this._autoDetectTimezone();

    if (el.modal) el.modal.style.display = 'flex';

    try {
      const data = await this._profile.getProfile(this._state.userId);
      if (data && data.profile) {
        this.populateProfile(data.profile);
      }
    } catch (err) {
      console.warn('[ProfileModal] Could not fetch profile from server, using local defaults:', err);
    }
  }

  close() {
    const el = this.elements;
    if (el.modal) el.modal.style.display = 'none';
  }

  populateProfile(p) {
    const el = this.elements;
    if (p.name && el.inpName) el.inpName.value = p.name;
    if (p.sex !== undefined && el.inpSex) el.inpSex.value = p.sex;
    if (p.age && el.inpAge) el.inpAge.value = p.age;
    if (p.heightCm && el.inpHeight) {
      el.inpHeight.value = p.heightCm;
      this.syncFromCm(p.heightCm);
    }
    if (p.currentWeightKg && el.inpWeight) el.inpWeight.value = p.currentWeightKg;
    if (p.targetWeightKg && el.inpTargetWeight) el.inpTargetWeight.value = p.targetWeightKg;
    if (p.desiredPaceKgPerWeek && el.inpPace) el.inpPace.value = p.desiredPaceKgPerWeek.toFixed(2);
    if (p.activityLevel !== undefined && el.inpActivity) el.inpActivity.value = p.activityLevel;
    if (p.dietaryPreference !== undefined && el.inpDietary) el.inpDietary.value = p.dietaryPreference;
    if (p.regionalCuisine && el.inpCuisine) el.inpCuisine.value = p.regionalCuisine;
    
    if (p.timezone && el.inpTimezone) {
      let match = Array.from(el.inpTimezone.options).find(o => o.value === p.timezone);
      if (!match) {
        const opt = document.createElement('option');
        opt.value = p.timezone;
        opt.textContent = `${p.timezone} (Profile)`;
        el.inpTimezone.appendChild(opt);
      }
      el.inpTimezone.value = p.timezone;
      this._state.userTimezone = p.timezone;
      if (el.timezoneAutoHint) {
        el.timezoneAutoHint.textContent = p.timezone;
      }
    }

    if (Array.isArray(p.diagnosedConditions)) {
      document.querySelectorAll('#conditions-list input[type="checkbox"]').forEach(cb => {
        cb.checked = p.diagnosedConditions.includes(cb.value);
      });
      this.updateMedicationSuggestions(false);
    }

    if (Array.isArray(p.medications) && p.medications.length > 0 && el.inpMedications) {
      el.inpMedications.value = p.medications.map(m => m.drugName || m).join(', ');
    }
  }

  _autoDetectTimezone() {
    const el = this.elements;
    if (!el.inpTimezone) return;
    try {
      const detected = Intl.DateTimeFormat().resolvedOptions().timeZone;
      if (detected) {
        let match = Array.from(el.inpTimezone.options).find(o => o.value === detected);
        if (!match) {
          const opt = document.createElement('option');
          opt.value = detected;
          opt.textContent = `${detected} (Auto-detected)`;
          el.inpTimezone.appendChild(opt);
        }
        if (!el.inpTimezone.dataset.userModified) {
          el.inpTimezone.value = detected;
          this._state.userTimezone = detected;
          if (el.timezoneAutoHint) {
            el.timezoneAutoHint.textContent = `Auto-detected: ${detected}`;
          }
        }
      }
    } catch {
      // Ignore if Intl unavailable
    }
  }

  syncFromCm(cm) {
    const el = this.elements;
    if (!cm || isNaN(cm) || cm <= 0) return;

    const totalInches = cm / 2.54;
    const feet = Math.floor(totalInches / 12);
    const inches = Math.round((totalInches % 12) * 10) / 10;

    if (el.inpHeightFt) el.inpHeightFt.value = feet;
    if (el.inpHeightIn) el.inpHeightIn.value = inches;
    if (el.heightHint) el.heightHint.textContent = `≈ ${feet} ft ${inches} in (${Math.round(cm * 10) / 10} cm)`;
  }

  syncFromFtIn(feet, inches) {
    const el = this.elements;
    const f = parseInt(feet) || 0;
    const i = parseFloat(inches) || 0;
    const totalInches = f * 12 + i;
    const cm = Math.round(totalInches * 2.54 * 10) / 10;

    if (el.inpHeight) el.inpHeight.value = cm;
    if (el.heightHint) el.heightHint.textContent = `≈ ${cm} cm (${f} ft ${i} in)`;
  }

  updateMedicationSuggestions(autoFill = false) {
    const el = this.elements;
    const checkedBoxes = Array.from(document.querySelectorAll('#conditions-list input:checked'));
    const conditions = checkedBoxes.map(cb => cb.value);

    // Sync active chip styling
    document.querySelectorAll('#conditions-list label').forEach(lbl => {
      const cb = lbl.querySelector('input');
      if (cb && cb.checked) {
        lbl.classList.add('active');
      } else {
        lbl.classList.remove('active');
      }
    });

    if (!el.medSuggestions || !el.inpMedications) return;

    const uniqueMeds = this._meds.getSuggestions(conditions);

    if (uniqueMeds.length > 0) {
      el.inpMedications.placeholder = `e.g. ${uniqueMeds.join(', ')}`;
      if (el.medPlaceholderHint) {
        el.medPlaceholderHint.textContent = `Auto-suggested for: ${conditions.join(', ')}`;
      }

      el.medSuggestions.innerHTML = uniqueMeds.map(m => `
        <button type="button" class="btn btn-sm med-chip-btn" style="font-size: 0.72rem; padding: 2px 8px; background: rgba(94, 106, 210, 0.12); border-color: rgba(94, 106, 210, 0.35); color: var(--text-primary); cursor: pointer;" data-med="${m}">
          + ${m}
        </button>
      `).join('') + `
        <button type="button" class="btn btn-sm btn-clear-meds" style="font-size: 0.72rem; padding: 2px 8px; cursor: pointer;">Clear / None</button>
      `;

      el.medSuggestions.querySelectorAll('.med-chip-btn').forEach(btn => {
        btn.addEventListener('click', () => this.appendMedication(btn.dataset.med));
      });

      el.medSuggestions.querySelector('.btn-clear-meds')?.addEventListener('click', () => this.clearMedications());

      if (autoFill || !el.inpMedications.value.trim()) {
        const defaults = this._meds.getDefaults(conditions);
        el.inpMedications.value = defaults.join(', ');
        if (el.medPromptNote) el.medPromptNote.style.display = 'block';
      }
    } else {
      el.inpMedications.placeholder = "e.g. None or enter your active medications & dosages";
      if (el.medPlaceholderHint) el.medPlaceholderHint.textContent = "No specific medication indicated";
      el.medSuggestions.innerHTML = `
        <span style="font-size: 0.7rem; color: var(--text-muted);">No medications suggested for 'None' condition.</span>
        <button type="button" class="btn btn-sm btn-clear-meds" style="font-size: 0.72rem; padding: 2px 8px; cursor: pointer;">None</button>
      `;
      el.medSuggestions.querySelector('.btn-clear-meds')?.addEventListener('click', () => this.clearMedications());
    }
  }

  appendMedication(med) {
    const el = this.elements;
    if (!el.inpMedications) return;
    const current = el.inpMedications.value.trim();
    if (!current || current.toLowerCase() === 'none') {
      el.inpMedications.value = med;
    } else {
      const items = current.split(',').map(s => s.trim()).filter(Boolean);
      if (!items.includes(med)) items.push(med);
      el.inpMedications.value = items.join(', ');
    }
    el.inpMedications.focus();
  }

  clearMedications() {
    const el = this.elements;
    if (el.inpMedications) {
      el.inpMedications.value = 'None';
      el.inpMedications.focus();
    }
  }

  async handleSubmit(e) {
    e.preventDefault();
    const el = this.elements;
    const saveBtn = document.getElementById('btn-profile-save');
    const saveBtnText = document.getElementById('btn-profile-save-text');
    const origText = saveBtnText ? saveBtnText.innerHTML : '';

    if (saveBtn) {
      saveBtn.disabled = true;
      if (saveBtnText) saveBtnText.innerHTML = '⏳ Recalculating...';
    }

    const selectedConditions = Array.from(document.querySelectorAll('#conditions-list input:checked')).map(c => c.value);
    const medsText = el.inpMedications ? el.inpMedications.value.trim() : '';
    const meds = medsText ? medsText.split(',').map(m => ({ drugName: m.trim() })) : [];

    const selectedTimezone = el.inpTimezone ? el.inpTimezone.value : (this._state.userTimezone || 'Asia/Kolkata');

    const profile = {
      id: this._state.userId,
      name: el.inpName ? el.inpName.value.trim() : 'Patient',
      sex: parseInt(el.inpSex ? el.inpSex.value : '0'),
      age: parseInt(el.inpAge ? el.inpAge.value : '32'),
      heightCm: parseFloat(el.inpHeight ? el.inpHeight.value : '175'),
      currentWeightKg: parseFloat(el.inpWeight ? el.inpWeight.value : '82'),
      targetWeightKg: parseFloat(el.inpTargetWeight ? el.inpTargetWeight.value : '72'),
      desiredPaceKgPerWeek: parseFloat(el.inpPace ? el.inpPace.value : '0.5'),
      activityLevel: parseInt(el.inpActivity ? el.inpActivity.value : '1'),
      dietaryPreference: parseInt(el.inpDietary ? el.inpDietary.value : '0'),
      regionalCuisine: el.inpCuisine ? el.inpCuisine.value : 'North Indian',
      timezone: selectedTimezone,
      diagnosedConditions: selectedConditions,
      medications: meds
    };

    try {
      const data = await this._profile.saveProfile(profile);
      this._state.userTimezone = profile.timezone;

      if (saveBtn) {
        saveBtn.disabled = false;
        if (saveBtnText) saveBtnText.innerHTML = origText;
      }

      this.close();
      this._confetti.burst();

      // Emit global profile:updated event
      this._bus.emit('profile:updated', data);

      this._toast.show({
        title: 'Clinical Targets Recalculated! 🎉',
        message: `New Daily Target: <strong>${Math.round(data.budget.targetCalories)} kcal</strong> · BMR: <strong>${Math.round(data.budget.bmr)} kcal</strong> · TDEE: <strong>${Math.round(data.budget.tdee)} kcal</strong> · BMI: <strong>${data.budget.bmi}</strong> (${data.budget.bmiClassification}). All transparency formulas are updated.`,
        actionText: '📐 View Calculation Transparency',
        onAction: () => this._bus.emit('transparency:open')
      });
    } catch (err) {
      if (saveBtn) {
        saveBtn.disabled = false;
        if (saveBtnText) saveBtnText.innerHTML = origText;
      }
      this._toast.show({
        title: 'Validation Error',
        message: err.message || 'Failed to save clinical profile.'
      });
    }
  }
}
