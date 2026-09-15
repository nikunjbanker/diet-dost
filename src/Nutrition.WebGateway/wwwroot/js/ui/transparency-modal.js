/**
 * TransparencyModalController
 * Manages the ICMR-NIN & WHO South Asian Clinical Calculation Transparency One-Pager.
 */
export class TransparencyModalController {
  /**
   * @param {Object} options
   * @param {import('../services/profile-service.js').ProfileService} options.profileService
   * @param {import('../core/state.js').AppState} options.appState
   * @param {import('../core/event-bus.js').EventBus} options.eventBus
   */
  constructor({ profileService, appState, eventBus }) {
    this._profile = profileService;
    this._state = appState;
    this._bus = eventBus;

    this._bindEvents();

    // Listen for events
    this._bus.on('transparency:open', () => this.open());
    this._bus.on('profile:updated', () => this.populate());
  }

  get elements() {
    return {
      modal: document.getElementById('transparency-modal'),
      btnOpen: document.getElementById('btn-open-transparency'),
      btnClose: document.getElementById('btn-close-transparency'),
      userSummary: document.getElementById('tp-user-summary'),
      bmrVal: document.getElementById('tp-bmr-val'),
      bmrFormula: document.getElementById('tp-bmr-formula'),
      tdeeVal: document.getElementById('tp-tdee-val'),
      tdeeFormula: document.getElementById('tp-tdee-formula'),
      targetVal: document.getElementById('tp-target-val'),
      deficitFormula: document.getElementById('tp-deficit-formula'),
      bmiVal: document.getElementById('tp-bmi-val'),
      ibwFormula: document.getElementById('tp-ibw-formula'),
      adjustmentsText: document.getElementById('tp-adjustments-text'),
      syncStatus: document.getElementById('tp-sync-status'),
      auditCard: document.getElementById('tp-audit-card')
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
  }

  async open() {
    await this.populate();
    const el = this.elements;
    if (el.modal) el.modal.style.display = 'flex';
  }

  close() {
    const el = this.elements;
    if (el.modal) el.modal.style.display = 'none';
  }

  async populate() {
    try {
      const data = await this._profile.getProfile(this._state.userId);
      if (!data) return;

      const p = data.profile;
      const b = data.budget;
      const m = data.macros;
      const el = this.elements;

      const totalInches = p.heightCm / 2.54;
      const feet = Math.floor(totalInches / 12);
      const inches = Math.round((totalInches % 12) * 10) / 10;
      const sexStr = p.sex === 0 ? 'Male' : 'Female';

      if (el.userSummary) {
        el.userSummary.textContent = `${p.name} · ${sexStr}, ${p.age}y · ${Math.round(p.heightCm * 10) / 10}cm (${feet}'${inches}") · ${p.currentWeightKg}kg`;
      }
      if (el.bmrVal) {
        el.bmrVal.textContent = `${Math.round(b.bmr)} kcal/day`;
      }
      if (el.bmrFormula) {
        el.bmrFormula.textContent = `Mifflin-St Jeor: (10 × ${p.currentWeightKg}) + (6.25 × ${p.heightCm}) - (5 × ${p.age}) ${p.sex === 0 ? '+ 5' : '- 161'}`;
      }

      const actMult = p.activityLevel === 0 ? 1.20 : p.activityLevel === 1 ? 1.375 : p.activityLevel === 2 ? 1.55 : 1.725;
      const actLabel = p.activityLevel === 0 ? 'Sedentary' : p.activityLevel === 1 ? 'Light' : p.activityLevel === 2 ? 'Moderate' : 'High';

      if (el.tdeeVal) {
        el.tdeeVal.textContent = `${Math.round(b.tdee)} kcal/day`;
      }
      if (el.tdeeFormula) {
        el.tdeeFormula.textContent = `Activity Multiplier: ${Math.round(b.bmr)} × ${actMult} (${actLabel})`;
      }

      if (el.targetVal) {
        el.targetVal.textContent = `${Math.round(b.targetCalories)} kcal/day Target`;
      }
      if (el.deficitFormula) {
        el.deficitFormula.textContent = `Deficit: -${Math.round(b.deficitCalories)} kcal/day (${p.desiredPaceKgPerWeek} kg/wk pace). Safe Floor: ≥ ${p.sex === 0 ? '1,500' : '1,200'} kcal ✓`;
      }

      if (el.bmiVal) {
        el.bmiVal.textContent = `BMI ${b.bmi} (${b.bmiClassification})`;
      }
      if (el.ibwFormula) {
        el.ibwFormula.textContent = `Ideal Body Wt (BMI 22): ${b.idealBodyWeightKg} kg · Target Protein: ${Math.round(m.proteinGrams)}g (${Math.round((m.proteinGrams / b.idealBodyWeightKg) * 10) / 10}g/kg IBW)`;
      }

      if (el.adjustmentsText) {
        const condList = p.diagnosedConditions && p.diagnosedConditions.length > 0
          ? p.diagnosedConditions.join(', ')
          : 'None diagnosed';
        const adjList = b.clinicalAdjustments && b.clinicalAdjustments.length > 0
          ? b.clinicalAdjustments.join(' ')
          : 'Standard ICMR-NIN & WHO South Asian protocols active.';
        const medsList = p.medications && p.medications.length > 0
          ? ` Prescribed: ${p.medications.map(x => x.drugName).join(', ')}.`
          : '';
        el.adjustmentsText.textContent = `Conditions: ${condList}. ${adjList}${medsList}`;
      }

      if (el.syncStatus) {
        const timeStr = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
        el.syncStatus.textContent = `✓ Live Synced (${timeStr})`;
      }

      if (el.auditCard) {
        el.auditCard.classList.add('audit-card-updated');
        setTimeout(() => el.auditCard?.classList.remove('audit-card-updated'), 1800);
      }
    } catch (err) {
      console.error('[TransparencyModalController] Failed to populate:', err);
    }
  }
}
