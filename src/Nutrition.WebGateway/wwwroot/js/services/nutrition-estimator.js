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
  const timeoutMs = options.timeoutMs || 4000;
  const controller = typeof AbortController !== 'undefined' ? new AbortController() : null;
  const timer = controller ? setTimeout(() => controller.abort(), timeoutMs) : null;

  try {
    const res = await fetch('/api/meals/estimate-item', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        name: trimmed,
        portion: portion || baseline.portion,
        useAi: true
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
          portion: data.estimatedPortion || baseline.portion,
          grams: data.grams || baseline.grams,
          calories: Math.round(data.calories),
          proteinGrams: Number((data.proteinGrams ?? baseline.proteinGrams).toFixed(1)),
          carbsGrams: Number((data.carbsGrams ?? baseline.carbsGrams).toFixed(1)),
          fatGrams: Number((data.fatGrams ?? baseline.fatGrams).toFixed(1)),
          fiberGrams: Number((data.fiberGrams ?? baseline.fiberGrams).toFixed(1)),
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
