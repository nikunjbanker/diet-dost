/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
/**
 * Indian Food Nutrition Knowledge Engine (Client-side)
 * Calibrated against ICMR-NIN Dietary Guidelines and Indian Food Composition Tables (IFCT).
 * Provides instant, zero-latency nutrition estimation for user corrections and smart search.
 */

export const INDIAN_FOOD_DICTIONARY = [
  // Bakery & Indian Snacks
  {
    keywords: ['veg puff', 'vegetable puff', 'puff', 'aloo patties', 'veg patties', 'patties', 'patty'],
    name: 'Veg Puff / Veg Patties',
    hindiName: 'Aloo Patties / Veg Puff',
    portion: '1 piece (~85g)',
    grams: 85,
    calories: 268,
    proteinGrams: 4.5,
    carbsGrams: 28.2,
    fatGrams: 15.6,
    fiberGrams: 2.1,
    sodiumMg: 380
  },
  {
    keywords: ['tomato sauce', 'tomato ketchup', 'ketchup', 'sauce'],
    name: 'Tomato Ketchup / Sauce',
    hindiName: 'Tomato Sauce',
    portion: '1 tbsp (~18g)',
    grams: 18,
    calories: 20,
    proteinGrams: 0.2,
    carbsGrams: 4.6,
    fatGrams: 0.1,
    fiberGrams: 0.1,
    sodiumMg: 160
  },
  {
    keywords: ['samosa', 'aloo samosa'],
    name: 'Samosa (Potato & Peas)',
    hindiName: 'Aloo Samosa',
    portion: '1 piece (~80g)',
    grams: 80,
    calories: 240,
    proteinGrams: 3.5,
    carbsGrams: 25.0,
    fatGrams: 14.0,
    fiberGrams: 2.0,
    sodiumMg: 320
  },
  // Okra / Bhindi combinations
  {
    keywords: ['okra with pototo', 'okra with potato', 'bhindi aloo', 'aloo bhindi', 'bhindi potato'],
    name: 'Bhindi Aloo (Okra with Potato)',
    hindiName: 'Aloo Bhindi ki Subzi',
    portion: '1 Katori (~120g)',
    grams: 120,
    calories: 120,
    proteinGrams: 2.6,
    carbsGrams: 16.0,
    fatGrams: 5.5,
    fiberGrams: 3.5,
    sodiumMg: 180
  },
  {
    keywords: ['okra', 'bhindi', 'ladyfinger', 'bhendi', 'bhindi masala', 'bhindi fry'],
    name: 'Bhindi Masala (Okra Stir-Fry)',
    hindiName: 'Tadka Bhindi',
    portion: '1 Katori (~100g)',
    grams: 100,
    calories: 110,
    proteinGrams: 2.4,
    carbsGrams: 10.0,
    fatGrams: 6.0,
    fiberGrams: 3.8,
    sodiumMg: 170
  },
  // Palak Paneer / Saag Paneer
  {
    keywords: ['palak paneer', 'saag paneer'],
    name: 'Palak Paneer',
    hindiName: 'Palak Paneer',
    portion: '1 Katori (~150g)',
    grams: 150,
    calories: 220,
    proteinGrams: 12.0,
    carbsGrams: 8.0,
    fatGrams: 16.0,
    fiberGrams: 3.2,
    sodiumMg: 310
  },
  // Paneer Dishes
  {
    keywords: ['paneer bhurji'],
    name: 'Paneer Bhurji',
    hindiName: 'Paneer Bhurji',
    portion: '1 Katori (~120g)',
    grams: 120,
    calories: 210,
    proteinGrams: 14.0,
    carbsGrams: 6.0,
    fatGrams: 15.0,
    fiberGrams: 1.5,
    sodiumMg: 290
  },
  {
    keywords: ['paneer butter masala', 'shahi paneer', 'kadai paneer', 'paneer masala', 'paneer'],
    name: 'Paneer Butter Masala',
    hindiName: 'Paneer Gravy',
    portion: '1 Katori (~150g)',
    grams: 150,
    calories: 260,
    proteinGrams: 11.5,
    carbsGrams: 12.0,
    fatGrams: 19.0,
    fiberGrams: 2.0,
    sodiumMg: 340
  },
  // Aloo Gobi
  {
    keywords: ['aloo gobi', 'alu gobi', 'gobi aloo', 'potato cauliflower'],
    name: 'Aloo Gobi',
    hindiName: 'Aloo Gobi ki Subzi',
    portion: '1 Katori (~130g)',
    grams: 130,
    calories: 135,
    proteinGrams: 3.2,
    carbsGrams: 18.0,
    fatGrams: 6.0,
    fiberGrams: 3.8,
    sodiumMg: 210
  },
  {
    keywords: ['gobi', 'cauliflower', 'gobi masala'],
    name: 'Gobi Masala',
    hindiName: 'Gobi ki Subzi',
    portion: '1 Katori (~120g)',
    grams: 120,
    calories: 105,
    proteinGrams: 2.8,
    carbsGrams: 12.0,
    fatGrams: 5.0,
    fiberGrams: 3.5,
    sodiumMg: 190
  },
  // Baingan / Eggplant
  {
    keywords: ['baingan bharta', 'baingan', 'eggplant', 'aubergine', 'bharta'],
    name: 'Baingan Bharta',
    hindiName: 'Roasted Baingan Bharta',
    portion: '1 Katori (~130g)',
    grams: 130,
    calories: 115,
    proteinGrams: 2.5,
    carbsGrams: 12.0,
    fatGrams: 6.5,
    fiberGrams: 4.0,
    sodiumMg: 190
  },
  // Lauki / Bottle Gourd
  {
    keywords: ['lauki', 'bottle gourd', 'dudhi', 'ghia'],
    name: 'Lauki Subzi',
    hindiName: 'Lauki ki Subzi',
    portion: '1 Katori (~120g)',
    grams: 120,
    calories: 70,
    proteinGrams: 1.5,
    carbsGrams: 8.0,
    fatGrams: 3.5,
    fiberGrams: 2.5,
    sodiumMg: 150
  },
  // Tori / Ridge Gourd
  {
    keywords: ['tori', 'turai', 'ridge gourd'],
    name: 'Turai Subzi',
    hindiName: 'Turai ki Subzi',
    portion: '1 Katori (~120g)',
    grams: 120,
    calories: 65,
    proteinGrams: 1.2,
    carbsGrams: 7.0,
    fatGrams: 3.5,
    fiberGrams: 2.0,
    sodiumMg: 140
  },
  // Karela / Bitter Gourd
  {
    keywords: ['karela', 'bitter gourd'],
    name: 'Karela Fry',
    hindiName: 'Karela Masala',
    portion: '1 Katori (~100g)',
    grams: 100,
    calories: 85,
    proteinGrams: 2.2,
    carbsGrams: 9.0,
    fatGrams: 4.5,
    fiberGrams: 3.0,
    sodiumMg: 160
  },
  // Cabbage / Patta Gobi
  {
    keywords: ['cabbage', 'patta gobi', 'poriyal'],
    name: 'Cabbage Poriyal / Subzi',
    hindiName: 'Bandh Gobi',
    portion: '1 Katori (~100g)',
    grams: 100,
    calories: 75,
    proteinGrams: 1.8,
    carbsGrams: 8.0,
    fatGrams: 4.0,
    fiberGrams: 2.8,
    sodiumMg: 160
  },
  // Mix Veg
  {
    keywords: ['mix veg', 'mixed vegetable', 'mili juli'],
    name: 'Mix Vegetable Subzi',
    hindiName: 'Mili Juli Subzi',
    portion: '1 Katori (~120g)',
    grams: 120,
    calories: 120,
    proteinGrams: 3.0,
    carbsGrams: 15.0,
    fatGrams: 5.5,
    fiberGrams: 3.5,
    sodiumMg: 190
  },
  // Chole / Chana Masala
  {
    keywords: ['chole', 'chana masala', 'kabuli chana', 'chickpeas'],
    name: 'Chole Masala',
    hindiName: 'Amritsari Chole',
    portion: '1 Katori (~150g)',
    grams: 150,
    calories: 185,
    proteinGrams: 7.5,
    carbsGrams: 26.0,
    fatGrams: 6.0,
    fiberGrams: 6.0,
    sodiumMg: 340
  },
  // Rajma
  {
    keywords: ['rajma', 'kidney bean', 'rajmah'],
    name: 'Rajma Masala',
    hindiName: 'Punjabi Rajma',
    portion: '1 Katori (~150g)',
    grams: 150,
    calories: 165,
    proteinGrams: 8.5,
    carbsGrams: 24.0,
    fatGrams: 4.0,
    fiberGrams: 6.5,
    sodiumMg: 320
  },
  // Dal Makhani
  {
    keywords: ['dal makhani', 'makhani dal', 'black dal', 'maa ki dal'],
    name: 'Dal Makhani',
    hindiName: 'Dal Makhani',
    portion: '1 Katori (~150g)',
    grams: 150,
    calories: 240,
    proteinGrams: 8.5,
    carbsGrams: 24.0,
    fatGrams: 13.0,
    fiberGrams: 5.0,
    sodiumMg: 380
  },
  // Yellow Moong / Toor Dal
  {
    keywords: ['yellow moong dal', 'toor dal', 'tuvar dal', 'dal tadka', 'yellow dal', 'arhar dal', 'pili dal'],
    name: 'Yellow Moong Dal Tadka',
    hindiName: 'Pili Moong Dal',
    portion: '1 Katori (~150ml)',
    grams: 150,
    calories: 125,
    proteinGrams: 7.0,
    carbsGrams: 18.0,
    fatGrams: 3.5,
    fiberGrams: 4.5,
    sodiumMg: 300
  },
  // Chana Dal
  {
    keywords: ['chana dal', 'chane ki dal'],
    name: 'Chana Dal Tadka',
    hindiName: 'Chana Dal',
    portion: '1 Katori (~150ml)',
    grams: 150,
    calories: 140,
    proteinGrams: 7.5,
    carbsGrams: 20.0,
    fatGrams: 4.0,
    fiberGrams: 5.0,
    sodiumMg: 310
  },
  // Sambar
  {
    keywords: ['sambar', 'sambhar'],
    name: 'Sambar',
    hindiName: 'South Indian Sambar',
    portion: '1 Katori (~150ml)',
    grams: 150,
    calories: 95,
    proteinGrams: 4.2,
    carbsGrams: 14.0,
    fatGrams: 2.5,
    fiberGrams: 3.5,
    sodiumMg: 340
  },
  // Kadhi
  {
    keywords: ['kadhi', 'karhi', 'besan kadhi', 'dahi kadhi'],
    name: 'Besan Kadhi',
    hindiName: 'Dahi Besan Kadhi',
    portion: '1 Katori (~150ml)',
    grams: 150,
    calories: 130,
    proteinGrams: 5.0,
    carbsGrams: 14.0,
    fatGrams: 6.5,
    fiberGrams: 1.5,
    sodiumMg: 320
  },
  // Roti / Phulka
  {
    keywords: ['roti', 'phulka', 'chapati', 'chapatti', 'fulka'],
    name: 'Whole Wheat Phulka / Roti',
    hindiName: 'Gehu ki Roti',
    portion: '1 piece (folded, ~30g)',
    grams: 30,
    calories: 80,
    proteinGrams: 2.6,
    carbsGrams: 16.0,
    fatGrams: 0.5,
    fiberGrams: 2.2,
    sodiumMg: 3
  },
  // Paratha
  {
    keywords: ['aloo paratha', 'alu paratha'],
    name: 'Aloo Paratha',
    hindiName: 'Aloo Paratha',
    portion: '1 piece (~80g)',
    grams: 80,
    calories: 240,
    proteinGrams: 5.0,
    carbsGrams: 35.0,
    fatGrams: 9.0,
    fiberGrams: 3.5,
    sodiumMg: 240
  },
  {
    keywords: ['paneer paratha'],
    name: 'Paneer Paratha',
    hindiName: 'Paneer Paratha',
    portion: '1 piece (~80g)',
    grams: 80,
    calories: 280,
    proteinGrams: 10.0,
    carbsGrams: 28.0,
    fatGrams: 14.0,
    fiberGrams: 2.5,
    sodiumMg: 260
  },
  {
    keywords: ['paratha', 'parantha', 'plain paratha'],
    name: 'Plain Paratha',
    hindiName: 'Tawa Paratha',
    portion: '1 piece (~50g)',
    grams: 50,
    calories: 180,
    proteinGrams: 3.8,
    carbsGrams: 26.0,
    fatGrams: 7.0,
    fiberGrams: 2.5,
    sodiumMg: 180
  },
  // Rice
  {
    keywords: ['khichdi', 'moong dal khichdi'],
    name: 'Moong Dal Khichdi',
    hindiName: 'Dal Khichdi',
    portion: '1 Katori (~150g)',
    grams: 150,
    calories: 160,
    proteinGrams: 5.5,
    carbsGrams: 27.0,
    fatGrams: 3.8,
    fiberGrams: 3.0,
    sodiumMg: 280
  },
  {
    keywords: ['chicken biryani', 'murgh biryani'],
    name: 'Chicken Biryani',
    hindiName: 'Murgh Biryani',
    portion: '1 Plate (~250g)',
    grams: 250,
    calories: 340,
    proteinGrams: 22.0,
    carbsGrams: 40.0,
    fatGrams: 11.0,
    fiberGrams: 3.0,
    sodiumMg: 480
  },
  {
    keywords: ['veg biryani', 'vegetable biryani'],
    name: 'Vegetable Biryani',
    hindiName: 'Veg Biryani',
    portion: '1 Plate (~200g)',
    grams: 200,
    calories: 250,
    proteinGrams: 6.0,
    carbsGrams: 42.0,
    fatGrams: 8.0,
    fiberGrams: 3.5,
    sodiumMg: 420
  },
  {
    keywords: ['jeera rice'],
    name: 'Jeera Rice',
    hindiName: 'Jeera Rice',
    portion: '1 Katori (~120g)',
    grams: 120,
    calories: 160,
    proteinGrams: 2.8,
    carbsGrams: 29.0,
    fatGrams: 3.5,
    fiberGrams: 1.0,
    sodiumMg: 160
  },
  {
    keywords: ['rice', 'steamed rice', 'chawal', 'white rice', 'basmati rice'],
    name: 'Steamed Basmati Rice',
    hindiName: 'Uble Chawal',
    portion: '1 Katori (~100g)',
    grams: 100,
    calories: 130,
    proteinGrams: 2.7,
    carbsGrams: 28.0,
    fatGrams: 0.4,
    fiberGrams: 0.8,
    sodiumMg: 2
  },
  // Curd / Dahi
  {
    keywords: ['curd', 'dahi', 'yogurt'],
    name: 'Fresh Curd (Dahi)',
    hindiName: 'Ghar ka Dahi',
    portion: '1 Katori (~100g)',
    grams: 100,
    calories: 60,
    proteinGrams: 3.5,
    carbsGrams: 4.5,
    fatGrams: 3.0,
    fiberGrams: 0.0,
    sodiumMg: 40
  },
  {
    keywords: ['chaas', 'buttermilk', 'mattha'],
    name: 'Jeera Chaas',
    hindiName: 'Masala Chaas',
    portion: '1 Glass (~200ml)',
    grams: 200,
    calories: 40,
    proteinGrams: 2.5,
    carbsGrams: 3.5,
    fatGrams: 1.8,
    fiberGrams: 0.5,
    sodiumMg: 120
  },
  // Non-Veg
  {
    keywords: ['chicken curry', 'murgh curry', 'chicken masala'],
    name: 'Chicken Curry',
    hindiName: 'Tariwala Chicken',
    portion: '1 Katori (~150g)',
    grams: 150,
    calories: 220,
    proteinGrams: 22.0,
    carbsGrams: 5.0,
    fatGrams: 12.0,
    fiberGrams: 1.5,
    sodiumMg: 380
  },
  {
    keywords: ['egg curry', 'anda curry'],
    name: 'Egg Curry',
    hindiName: 'Anda Curry',
    portion: '2 Eggs (~160g)',
    grams: 160,
    calories: 190,
    proteinGrams: 13.0,
    carbsGrams: 6.0,
    fatGrams: 13.0,
    fiberGrams: 1.5,
    sodiumMg: 340
  },
  {
    keywords: ['egg bhurji', 'anda bhurji'],
    name: 'Egg Bhurji',
    hindiName: 'Anda Bhurji',
    portion: '2 Eggs (~120g)',
    grams: 120,
    calories: 180,
    proteinGrams: 13.0,
    carbsGrams: 4.0,
    fatGrams: 12.0,
    fiberGrams: 1.0,
    sodiumMg: 320
  },
  {
    keywords: ['fish curry', 'machli curry'],
    name: 'Fish Curry',
    hindiName: 'Machli Curry',
    portion: '1 Katori (~150g)',
    grams: 150,
    calories: 175,
    proteinGrams: 19.0,
    carbsGrams: 4.0,
    fatGrams: 9.0,
    fiberGrams: 1.0,
    sodiumMg: 360
  },
  // Salad
  {
    keywords: ['salad', 'kachumber', 'cucumber', 'kheera', 'green salad'],
    name: 'Green Salad',
    hindiName: 'Kachumber Salad',
    portion: '1 Small Plate (~80g)',
    grams: 80,
    calories: 30,
    proteinGrams: 1.0,
    carbsGrams: 6.0,
    fatGrams: 0.2,
    fiberGrams: 2.0,
    sodiumMg: 25
  }
];

/**
 * Estimate nutrition for any entered Indian food item.
 * @param {string} rawName - Food item name typed by user
 * @param {string} [portion] - Optional portion string
 * @returns {Object} Estimated nutrition metrics
 */
export function estimateIndianFoodNutrition(rawName, portion = null) {
  if (!rawName || !rawName.trim()) {
    return {
      name: 'Custom Food Item',
      hindiName: 'Bhojan',
      portion: portion || '1 Portion',
      grams: 100,
      calories: 100,
      proteinGrams: 3.0,
      carbsGrams: 15.0,
      fatGrams: 3.0,
      fiberGrams: 2.0,
      sodiumMg: 150
    };
  }

  const clean = rawName.trim().toLowerCase()
    .replace(/pototo/g, 'potato')
    .replace(/patato/g, 'potato')
    .replace(/sabji/g, 'subzi')
    .replace(/sabzi/g, 'subzi')
    .replace(/paner/g, 'paneer')
    .replace(/rotii/g, 'roti')
    .replace(/daal/g, 'dal');

  // 1. Direct Keyword / Substring Match in Dictionary
  for (const entry of INDIAN_FOOD_DICTIONARY) {
    for (const kw of entry.keywords) {
      if (clean.includes(kw)) {
        return {
          name: entry.name,
          hindiName: entry.hindiName,
          portion: portion || entry.portion,
          grams: entry.grams,
          calories: entry.calories,
          proteinGrams: entry.proteinGrams,
          carbsGrams: entry.carbsGrams,
          fatGrams: entry.fatGrams,
          fiberGrams: entry.fiberGrams,
          sodiumMg: entry.sodiumMg,
          matchedKeyword: kw
        };
      }
    }
  }

  // 2. Intelligent Culinary Heuristic Fallbacks
  if (clean.includes('paneer')) {
    return {
      name: rawName,
      hindiName: 'Paneer Gravy',
      portion: portion || '1 Katori (~150g)',
      grams: 150,
      calories: 240,
      proteinGrams: 11.0,
      carbsGrams: 10.0,
      fatGrams: 17.0,
      fiberGrams: 2.0,
      sodiumMg: 300,
      source: 'hardcoded (ICMR-NIN Heuristic)',
      isAiEstimated: false
    };
  }

  if (clean.includes('chicken') || clean.includes('murgh')) {
    return {
      name: rawName,
      hindiName: 'Chicken Gravy',
      portion: portion || '1 Katori (~150g)',
      grams: 150,
      calories: 220,
      proteinGrams: 21.0,
      carbsGrams: 5.0,
      fatGrams: 12.0,
      fiberGrams: 1.5,
      sodiumMg: 360,
      source: 'hardcoded (ICMR-NIN Heuristic)',
      isAiEstimated: false
    };
  }

  if (clean.includes('dal') || clean.includes('lentil')) {
    return {
      name: rawName,
      hindiName: 'Dal Tadka',
      portion: portion || '1 Katori (~150ml)',
      grams: 150,
      calories: 130,
      proteinGrams: 7.0,
      carbsGrams: 18.0,
      fatGrams: 3.5,
      fiberGrams: 4.5,
      sodiumMg: 300,
      source: 'hardcoded (ICMR-NIN Heuristic)',
      isAiEstimated: false
    };
  }

  if (clean.includes('rice') || clean.includes('chawal')) {
    return {
      name: rawName,
      hindiName: 'Chawal',
      portion: portion || '1 Katori (~100g)',
      grams: 100,
      calories: 130,
      proteinGrams: 2.7,
      carbsGrams: 28.0,
      fatGrams: 0.5,
      fiberGrams: 1.0,
      sodiumMg: 2,
      source: 'hardcoded (ICMR-NIN Heuristic)',
      isAiEstimated: false
    };
  }

  // Default Vegetable / Subzi estimate
  return {
    name: rawName,
    hindiName: 'Ghar ki Subzi',
    portion: portion || '1 Katori (~100g)',
    grams: 100,
    calories: 110,
    proteinGrams: 2.5,
    carbsGrams: 12.0,
    fatGrams: 5.5,
    fiberGrams: 3.0,
    sugarGrams: 1.8,
    sodiumMg: 180,
    source: 'hardcoded (ICMR-NIN Fallback)',
    isAiEstimated: false
  };
}

// In-memory cache for AI-estimated food items to ensure fast sub-millisecond retrieval on repeat queries
const aiNutritionCache = new Map();

/**
 * Asynchronously estimate nutrition using AI (Google AI Gemini 3.8 Flash / Clinical NLP Agent),
 * backed with ICMR-NIN hardcoded values as instant baseline and safe fallback.
 * 
 * @param {string} rawName - Food item name typed by user
 * @param {string} [portion] - Optional portion string
 * @param {Object} [options] - Options { timeoutMs, forceRefresh }
 * @returns {Promise<Object>} Estimated nutrition metrics with AI refinement
 */
export async function estimateFoodNutritionWithAi(rawName, portion = null, options = {}) {
  const trimmed = (rawName || '').trim();
  if (!trimmed) {
    return estimateIndianFoodNutrition(rawName, portion);
  }

  const cacheKey = `${trimmed.toLowerCase()}__${(portion || '').trim().toLowerCase()}`;
  if (!options.forceRefresh && aiNutritionCache.has(cacheKey)) {
    return aiNutritionCache.get(cacheKey);
  }

  // 1. Instant baseline from hardcoded ICMR-NIN dictionary
  const baseline = estimateIndianFoodNutrition(trimmed, portion);

  // 2. Attempt remote AI estimation via WebGateway endpoint
  const timeoutMs = options.timeoutMs || 20000;
  const controller = typeof AbortController !== 'undefined' ? new AbortController() : null;
  const timer = controller ? setTimeout(() => controller.abort(), timeoutMs) : null;

  try {
    const res = await fetch('/api/meals/estimate-item', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        name: trimmed,
        portion: portion || baseline.portion || '1 Portion',
        useAi: true,
        userId: options.userId || null,
        mealType: options.mealType || null
      }),
      signal: controller ? controller.signal : undefined
    });

    if (timer) clearTimeout(timer);

    if (res.ok) {
      const data = await res.json();
      if (data && (data.calories > 0 || data.calories === 0)) {
        const aiResult = {
          name: data.normalizedName || baseline.name,
          hindiName: data.hindiOrRegionalName || baseline.hindiName,
          portion: data.estimatedPortion || portion || baseline.portion,
          grams: data.grams || baseline.grams,
          calories: Math.round(data.calories),
          proteinGrams: Number((data.proteinGrams ?? baseline.proteinGrams).toFixed(1)),
          carbsGrams: Number((data.carbsGrams ?? baseline.carbsGrams).toFixed(1)),
          fatGrams: Number((data.fatGrams ?? baseline.fatGrams).toFixed(1)),
          fiberGrams: Number((data.fiberGrams ?? baseline.fiberGrams ?? 2.0).toFixed(1)),
          sugarGrams: Number((data.sugarGrams ?? baseline.sugarGrams ?? 1.5).toFixed(1)),
          sodiumMg: Math.round(data.sodiumMg ?? baseline.sodiumMg),
          cookingMedium: data.cookingMediumEstimate || baseline.cookingMedium || 'Homestyle',
          source: data.source || 'AI (Gemini 3.8 / Clinical NLP)',
          isAiEstimated: true,
          confidenceScore: data.confidenceScore || 0.92
        };

        aiNutritionCache.set(cacheKey, aiResult);
        return aiResult;
      }
    }
  } catch (err) {
    // Timeout or network glitch: gracefully proceed with hardcoded ICMR-NIN baseline
    if (timer) clearTimeout(timer);
    console.debug(`[DietDost AI Estimator] AI estimation skipped for "${trimmed}", using ICMR-NIN baseline:`, err?.message || err);
  }

  return {
    ...baseline,
    isAiEstimated: false,
    source: 'hardcoded (ICMR-NIN)'
  };
}

/**
 * Clear AI nutrition cache (useful for testing or profile switches).
 */
export function clearAiNutritionCache() {
  aiNutritionCache.clear();
}

/**
 * Dynamically generates clinical dietitian advice adhering to ICMR-NIN 2024 standards
 * based on current food items, portions, macros, and added cooking fats.
 *
 * @param {Array} items - Array of meal items { name, quantity, calories, proteinGrams, carbsGrams, fatGrams, fiberGrams, sodiumMg }
 * @param {Object} options - { addedGhee, addedTadka, mealType, userConditions }
 * @returns {string} Clinical dietitian advice string
 */
export function generateDietitianAdvice(items = [], options = {}) {
  if (!items || items.length === 0) {
    return 'Add food items to your meal plate to receive real-time ICMR-NIN 2024 clinical dietitian guidance.';
  }

  const addedGheeKcal = options.addedGhee || 0;
  const addedTadkaKcal = options.addedTadka || 0;

  let totalKcal = 0;
  let totalProtein = 0;
  let totalCarbs = 0;
  let totalFat = 0;
  let totalFiber = 0;
  let totalSodium = 0;

  items.forEach(i => {
    const qty = i.quantity || 1;
    totalKcal += (i.calories || 0) * qty;
    totalProtein += (i.proteinGrams || 0) * qty;
    totalCarbs += (i.carbsGrams || 0) * qty;
    totalFat += (i.fatGrams || 0) * qty;
    totalFiber += (i.fiberGrams || 0) * qty;
    totalSodium += (i.sodiumMg || 0) * qty;
  });

  totalKcal += addedGheeKcal + addedTadkaKcal;
  totalFat += (addedGheeKcal + addedTadkaKcal) / 9;

  // Classify items by Indian culinary & clinical nutritional categories
  const paneerItems = items.filter(i => /paneer|tofu|soya|soy/i.test(i.name));
  const dalItems = items.filter(i => /dal|dhal|chana|rajma|chole|moong|urad|masoor|toor|sambhar|kadhi|lobia|lentil/i.test(i.name));
  const grainItems = items.filter(i => /roti|phulka|chapati|rice|chawal|paratha|naan|bhakri|pulao|biryani|oats|khichdi|dosa|idli/i.test(i.name));
  const subziItems = items.filter(i => /subzi|sabzi|bhindi|okra|palak|spinach|gobi|cauliflower|aloo|potato|lauki|bottle gourd|methi|fenugreek|baingan|eggplant|brinjal|tinda|karela|beans|mushroom|peas|matar|mix veg/i.test(i.name) && !/paneer/i.test(i.name));
  const saladItems = items.filter(i => /salad|cucumber|kakdi|kheera|tomato|radish|mooli|onion|pyaz|pyaj|carrot|gajar|beetroot|kachumber|sprout/i.test(i.name));
  const curdItems = items.filter(i => /curd|dahi|yogurt|raita|chaas|buttermilk/i.test(i.name));
  const nonVegItems = items.filter(i => /chicken|egg|fish|mutton|prawn|keema/i.test(i.name));

  const sentences = [];

  // Sentence 1: Portion & Calorie assessment
  if (totalKcal < 300) {
    sentences.push(`Light portion (~${Math.round(totalKcal)} kcal). Ensure you are meeting your baseline daily energy and satiety requirements.`);
  } else if (totalKcal <= 650) {
    sentences.push(`Excellent portion control.`);
  } else if (totalKcal <= 850) {
    sentences.push(`Substantial, energy-rich meal (~${Math.round(totalKcal)} kcal).`);
  } else {
    sentences.push(`Calorie-dense meal (~${Math.round(totalKcal)} kcal). Consider moderating grain or cooking fat portions to maintain caloric balance.`);
  }

  // Sentence 2: Specific Key Food Highlights
  const highlights = [];
  if (paneerItems.length > 0) {
    const pNames = paneerItems.map(p => p.name.replace(/\s*\([^)]*\)/g, '').trim()).join(' & ');
    highlights.push(`The inclusion of ${pNames} provides essential calcium and high-quality protein`);
  }
  if (dalItems.length > 0) {
    const dNames = dalItems.map(d => d.name.replace(/\s*\([^)]*\)/g, '').trim()).join(' & ');
    if (paneerItems.length > 0) {
      highlights.push(`while ${dNames} supplies wholesome plant-based protein and prebiotic fiber`);
    } else {
      highlights.push(`The ${dNames} provides wholesome plant-based protein and prebiotic fiber`);
    }
  }
  if (nonVegItems.length > 0) {
    const nvNames = nonVegItems.map(n => n.name.replace(/\s*\([^)]*\)/g, '').trim()).join(' & ');
    highlights.push(`The ${nvNames} delivers high biological value complete protein`);
  }
  if (subziItems.length > 0 && paneerItems.length === 0) {
    const sNames = subziItems.map(s => s.name.replace(/\s*\([^)]*\)/g, '').trim()).join(' & ');
    if (dalItems.length > 0) {
      highlights.push(`and ${sNames} adds essential micronutrients and dietary fiber`);
    } else {
      highlights.push(`The inclusion of ${sNames} provides essential micronutrients, antioxidants, and dietary fiber`);
    }
  }

  if (highlights.length > 0) {
    sentences.push(highlights.join(', ') + '.');
  } else if (grainItems.length > 0) {
    const gNames = grainItems.map(g => g.name.replace(/\s*\([^)]*\)/g, '').trim()).join(' & ');
    sentences.push(`${gNames} supplies complex carbohydrates for sustained energy.`);
  }

  // Sentence 3: Amino Acid Complementation & Protein targets (ICMR-NIN 2024)
  if (grainItems.length > 0 && dalItems.length > 0) {
    sentences.push(`Features classic cereal-to-pulse amino acid complementation (${totalProtein.toFixed(1)}g Protein).`);
  } else if (totalProtein >= 20) {
    sentences.push(`High protein density (${totalProtein.toFixed(1)}g Protein), supporting metabolic satiety.`);
  } else if (totalProtein < 12 && (options.mealType === 'Lunch' || options.mealType === 'Dinner')) {
    sentences.push(`Protein is on the lower side (${totalProtein.toFixed(1)}g). Consider adding curd, sprouts, or paneer to reach your target.`);
  }

  // Sentence 4: Probiotics
  if (curdItems.length > 0) {
    sentences.push(`Plain Curd contributes gut-friendly probiotics and bioavailable calcium.`);
  }

  // Sentence 5: Salad & Micronutrient Optimization (ICMR-NIN 2024 "My Plate for the Day")
  if (saladItems.length > 0) {
    const salNames = saladItems.map(s => s.name.replace(/\s*\([^)]*\)/g, '').trim()).join(' & ');
    sentences.push(`The inclusion of ${salNames} increases hydration and micronutrient density adhering to ICMR-NIN 2024 guidelines.`);
  } else {
    sentences.push(`To further optimize for ICMR-NIN 2024 standards, consider adding a small portion of raw cucumber or radish (Green Salad) to increase hydration and micronutrient density.`);
  }

  // Sentence 6: Added Fats
  if (addedGheeKcal > 0 || addedTadkaKcal > 0) {
    const fats = [];
    if (addedGheeKcal > 0) fats.push('Ghee Smear (+45 kcal)');
    if (addedTadkaKcal > 0) fats.push('Extra Oil Tadka (+60 kcal)');
    sentences.push(`Note: Added ${fats.join(' & ')} increases fat-soluble vitamin absorption; balance within your daily allowance.`);
  }

  return sentences.join(' ');
}

/**
 * Scales an item's nutrition metrics based on an updated portion string.
 * Supports numbers, ranges ("5-6 Slices"), fractions ("1/2 Cup"), decimals ("1.5 Cup"),
 * and units like Cup, Katori, Bowl, Slice, Piece, Tbsp, Tsp, Grams.
 * 
 * @param {Object} baseline - Item nutrition object
 * @param {string} portionText - New portion string (e.g. "1.5 Cup", "5-6 Slices", "1 Katori", "200g")
 * @returns {Object} Scaled nutrition object
 */
export function scaleNutritionByPortion(baseline, portionText) {
  if (!baseline || !portionText || !portionText.trim()) return baseline;

  const clean = portionText.trim().toLowerCase();
  const baseGrams = baseline.grams || 100;
  let ratio = 1.0;
  let targetGrams = baseGrams;

  // 1. Direct grams / ml check: e.g. "150g", "200 gm", "250ml", "100 grams"
  const gramMatch = clean.match(/(\d+(?:\.\d+)?)\s*(?:g|gm|gram|grams|ml)\b/);
  if (gramMatch && parseFloat(gramMatch[1]) > 0) {
    targetGrams = parseFloat(gramMatch[1]);
    ratio = targetGrams / (baseGrams || 100);
  } else {
    // 2. Quantity extraction (Ranges: "5-6", Decimals: "1.5", Fractions: "1/2", Integers: "3")
    let quantity = 1.0;
    let hasQty = false;

    const rangeMatch = clean.match(/(\d+(?:\.\d+)?)\s*-\s*(\d+(?:\.\d+)?)/);
    if (rangeMatch) {
      const n1 = parseFloat(rangeMatch[1]);
      const n2 = parseFloat(rangeMatch[2]);
      if (!isNaN(n1) && !isNaN(n2)) {
        // A range represents an approximate serving count, so scale by its midpoint.
        quantity = (n1 + n2) / 2.0;
        hasQty = true;
      }
    } else {
      const fracMatch = clean.match(/(\d+)\s*\/\s*(\d+)/);
      if (fracMatch) {
        const num = parseFloat(fracMatch[1]);
        const den = parseFloat(fracMatch[2]);
        if (!isNaN(num) && !isNaN(den) && den > 0) {
          quantity = num / den;
          hasQty = true;
        }
      } else {
        const numMatch = clean.match(/^(\d+(?:\.\d+)?)/);
        if (numMatch) {
          const singleNum = parseFloat(numMatch[1]);
          if (!isNaN(singleNum)) {
            quantity = singleNum;
            hasQty = true;
          }
        }
      }
    }

    if (hasQty && quantity > 0) {
      if (clean.includes('cup')) {
        const unitGrams = 200.0; // Standard 200g Indian Cup
        targetGrams = quantity * unitGrams;
        ratio = targetGrams / (baseGrams || 150);
      } else if (clean.includes('bowl')) {
        const unitGrams = 220.0;
        targetGrams = quantity * unitGrams;
        ratio = targetGrams / (baseGrams || 150);
      } else if (clean.includes('slice')) {
        const unitGrams = clean.includes('bread') ? 30.0 : 20.0;
        targetGrams = quantity * unitGrams;
        ratio = targetGrams / (baseGrams || 80);
      } else if (clean.includes('tbsp') || clean.includes('tablespoon')) {
        const unitGrams = 15.0;
        targetGrams = quantity * unitGrams;
        ratio = targetGrams / (baseGrams || 15);
      } else if (clean.includes('tsp') || clean.includes('teaspoon')) {
        const unitGrams = 5.0;
        targetGrams = quantity * unitGrams;
        ratio = targetGrams / (baseGrams || 5);
      } else if (clean.includes('katori')) {
        ratio = quantity;
        targetGrams = (baseGrams || 120) * ratio;
      } else {
        ratio = quantity;
        targetGrams = (baseGrams || 100) * ratio;
      }
    }
  }

  ratio = Math.max(0.1, Math.min(10.0, ratio));
  targetGrams = Math.round(Math.max(10, Math.min(2500, targetGrams)));

  return {
    ...baseline,
    estimatedPortion: portionText,
    grams: targetGrams,
    calories: Math.round((baseline.calories || 100) * ratio),
    proteinGrams: Number(((baseline.proteinGrams || 3) * ratio).toFixed(1)),
    carbsGrams: Number(((baseline.carbsGrams || 12) * ratio).toFixed(1)),
    fatGrams: Number(((baseline.fatGrams || 5) * ratio).toFixed(1)),
    fiberGrams: Number(((baseline.fiberGrams || 2) * ratio).toFixed(1)),
    sugarGrams: Number(((baseline.sugarGrams || 1.5) * ratio).toFixed(1)),
    sodiumMg: Math.round((baseline.sodiumMg || 100) * ratio)
  };
}
