/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
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
   * Fetch single meal log by ID.
   * @param {string} id
   */
  async getMeal(id) {
    return this._api.get(`/api/meals/${encodeURIComponent(id)}`);
  }

  /**
   * Update an existing logged meal.
   * @param {string} id
   * @param {Object} mealData
   */
  async updateMeal(id, mealData) {
    return this._api.putJson(`/api/meals/${encodeURIComponent(id)}`, mealData);
  }

  /**
   * Delete an existing logged meal.
   * @param {string} id
   */
  async deleteMeal(id) {
    return this._api.delete(`/api/meals/${encodeURIComponent(id)}`);
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
   * @param {string} [userId=null]
   * @param {string} [mealType=null]
   */
  async estimateFoodItem(name, portion = null, useAi = true, userId = null, mealType = null) {
    return this._api.postJson('/api/meals/estimate-item', { name, portion, useAi, userId, mealType });
  }

  /**
   * Submit AI detection feedback (thumbs up / thumbs down + remarks) for model evaluation and continuous retraining.
   * @param {Object} feedbackData
   */
  async submitAiFeedback(feedbackData) {
    return this._api.postJson('/api/meals/ai-feedback', feedbackData);
  }

  /**
   * Retrieve logged meal history with child items for a user and period/date.
   * @param {string} userId
   * @param {string} [period='7D']
   * @param {string|null} [date=null]
   * @param {string|null} [mealType=null]
   */
  async getMealHistory(userId, period = '7D', date = null, mealType = null) {
    const params = { userId, period };
    if (date) params.date = date;
    if (mealType) params.mealType = mealType;
    return this._api.get('/api/meals/history', params);
  }

  /**
   * Get server-side export download URL for Excel / CSV.
   * @param {string} userId
   * @param {string} [period='7D']
   * @param {string|null} [date=null]
   * @param {string|null} [mealType=null]
   */
  exportMealsUrl(userId, period = '7D', date = null, mealType = null) {
    let url = `/api/meals/export?userId=${encodeURIComponent(userId)}&period=${encodeURIComponent(period)}&format=csv`;
    if (date) url += `&date=${encodeURIComponent(date)}`;
    if (mealType) url += `&mealType=${encodeURIComponent(mealType)}`;
    return url;
  }

  /**
   * Client-side instant RFC 4180 Excel CSV export with UTF-8 BOM.
   * @param {Array<Object>} meals
   * @param {string} [period='7D']
   */
  exportMealsClientCsv(meals, period = '7D') {
    if (!meals || meals.length === 0) return false;

    const escapeCsv = (val) => {
      if (val === null || val === undefined) return '""';
      const str = String(val).replace(/"/g, '""');
      return `"${str}"`;
    };

    const header = [
      'Meal ID',
      'Date',
      'Time',
      'Meal Type',
      'Dish Name',
      'Calories (kcal)',
      'Protein (g)',
      'Carbs (g)',
      'Fat (g)',
      'Fiber (g)',
      'Sugar (g)',
      'Sodium (mg)',
      'Ghee/Tadka (kcal)',
      'Food Items Breakdown',
      'AI Confidence',
      'Verified',
      'Dietitian Clinical Advice',
      'Feedback Rating',
      'Feedback Remarks'
    ].join(',');

    const mealTypeNames = ['Breakfast', 'Lunch', 'Snack', 'Dinner'];

    const rows = meals.map(m => {
      const d = new Date(m.loggedAt || m.LoggedAt);
      const dateStr = !isNaN(d) ? d.toLocaleDateString('en-CA') : '';
      const timeStr = !isNaN(d) ? d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '';
      const typeStr = typeof m.mealType === 'number' ? (mealTypeNames[m.mealType] || 'Meal') : (m.mealType || 'Meal');

      const items = (m.items || m.Items || []).map(i =>
        `${i.name || i.Name} (${i.estimatedPortion || i.EstimatedPortion || '1 serving'}, ${Math.round(i.calories || i.Calories)} kcal, P: ${(i.proteinGrams || i.ProteinGrams || 0).toFixed(1)}g, C: ${(i.carbsGrams || i.CarbsGrams || 0).toFixed(1)}g, F: ${(i.fatGrams || i.FatGrams || 0).toFixed(1)}g, Fib: ${(i.fiberGrams || i.FiberGrams || 0).toFixed(1)}g, Sug: ${(i.sugarGrams || i.SugarGrams || 0).toFixed(1)}g)`
      ).join('; ');

      const addedFat = ((m.addedGheeKcal || 0) + (m.addedTadkaKcal || 0)).toFixed(1);
      const conf = Math.round((m.overallConfidenceScore || 0.85) * 100) + '%';
      const verified = m.isVerifiedByUser ? 'Yes' : 'No';

      return [
        escapeCsv(m.id || m.Id),
        escapeCsv(dateStr),
        escapeCsv(timeStr),
        escapeCsv(typeStr),
        escapeCsv(m.dishName || m.DishName),
        (m.totalCalories || 0).toFixed(1),
        (m.totalProteinGrams || 0).toFixed(1),
        (m.totalCarbsGrams || 0).toFixed(1),
        (m.totalFatGrams || 0).toFixed(1),
        (m.totalFiberGrams || 0).toFixed(1),
        (m.totalSugarGrams || 0).toFixed(1),
        (m.totalSodiumMg || 0).toFixed(1),
        addedFat,
        escapeCsv(items),
        escapeCsv(conf),
        escapeCsv(verified),
        escapeCsv(m.dietitianAdvice || m.DietitianAdvice),
        escapeCsv(m.aiFeedbackRating || m.AiFeedbackRating),
        escapeCsv(m.aiFeedbackRemarks || m.AiFeedbackRemarks)
      ].join(',');
    });

    const csvContent = '\uFEFF' + [header, ...rows].join('\r\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', `DietDost_Meals_${period}_${new Date().toISOString().slice(0, 10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
    return true;
  }
}

