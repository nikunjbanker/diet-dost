/**
 * MealsService
 * Handles AI vision photo uploads, text/voice meal parsing, meal confirmation, and correction retraining.
 */
export class MealsService {
  /**
   * @param {import('./api-client.js').ApiClient} apiClient
   */
  constructor(apiClient) {
    this._api = apiClient;
  }

  /**
   * Upload meal image for AI vision analysis.
   * @param {File} file
   * @param {string} userId
   * @param {string} mealType
   * @param {string} [regionalContext='North Indian']
   */
  async uploadMealImage(file, userId, mealType, regionalContext = 'North Indian') {
    const formData = new FormData();
    formData.append('image', file);
    formData.append('userId', userId);
    formData.append('mealType', mealType);
    formData.append('regionalContext', regionalContext);

    return this._api.postForm('/api/meals/upload', formData);
  }

  /**
   * Parse meal from text / voice description.
   * @param {string} description
   * @param {string} userId
   * @param {string} mealType
   */
  async analyzeMealText(description, userId, mealType) {
    return this._api.postJson('/api/meals/analyze-text', {
      userId,
      description,
      mealType
    });
  }

  /**
   * Confirm and log meal into daily clinical ledger.
   * @param {Object} mealData
   */
  async confirmMeal(mealData) {
    return this._api.postJson('/api/meals/confirm', mealData);
  }

  /**
   * Submit food correction for fine-tuning/retraining context.
   * @param {Object} correctionData
   */
  async retrainCorrection(correctionData) {
    return this._api.postJson('/api/meals/retrain-correction', correctionData);
  }

  /**
   * Estimate calories, protein, carbs, fat for an Indian food item by name.
   * Leverages both hardcoded ICMR-NIN baseline and AI agent estimation.
   * @param {string} name
   * @param {string} [portion]
   * @param {boolean} [useAi=true]
   */
  async estimateFoodItem(name, portion = null, useAi = true) {
    return this._api.postJson('/api/meals/estimate-item', { name, portion, useAi });
  }

  /**
   * Submit AI detection feedback (thumbs up / thumbs down + remarks) for model evaluation and continuous retraining.
   * @param {Object} feedbackData
   */
  async submitAiFeedback(feedbackData) {
    return this._api.postJson('/api/meals/ai-feedback', feedbackData);
  }
}

