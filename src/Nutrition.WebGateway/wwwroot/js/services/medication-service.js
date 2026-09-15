/**
 * MedicationService
 * Provides condition-to-medication mappings, typical Indian clinical dosages, and suggestion rules.
 */
export class MedicationService {
  constructor() {
    this.conditionMap = {
      'Type 2 Diabetes': ['Metformin 500mg', 'Glimepiride 1mg'],
      'Pre-Diabetes': ['Metformin 500mg'],
      'Hypertension': ['Telmisartan 40mg', 'Amlodipine 5mg'],
      'Hypothyroidism': ['Thyronorm 50mcg', 'Eltroxin 50mcg'],
      'High Cholesterol': ['Atorvastatin 10mg', 'Rosuvastatin 10mg'],
      'PCOS': ['Metformin 500mg', 'Myo-Inositol 2g'],
      'Gout': ['Febuxostat 40mg', 'Allopurinol 100mg'],
      'Fatty Liver': ['Vitamin E 400 IU', 'Saroglitazar 4mg']
    };
  }

  /**
   * Get suggestions for a list of active conditions.
   * @param {string[]} conditions
   * @returns {string[]}
   */
  getSuggestions(conditions) {
    const list = [];
    for (const cond of conditions) {
      if (this.conditionMap[cond]) {
        list.push(...this.conditionMap[cond]);
      }
    }
    return [...new Set(list)];
  }

  /**
   * Get first-line standard defaults for a list of conditions.
   * @param {string[]} conditions
   * @returns {string[]}
   */
  getDefaults(conditions) {
    const list = conditions
      .map(c => (this.conditionMap[c] ? this.conditionMap[c][0] : null))
      .filter(Boolean);
    return [...new Set(list)];
  }
}
