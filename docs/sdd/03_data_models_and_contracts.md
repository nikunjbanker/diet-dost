# Data Models, DDD Aggregates & Contracts
> **Specification Version**: `v1.1.0 (Production & Living SDD)`  
> **Persistence**: Swappable SQLite V1 / PostgreSQL  
> **Architecture Pattern**: Domain-Driven Design (DDD) & Clean Architecture  

---

## 1. Domain Aggregates & Value Objects

### 1.1 `Nutrition.ProfileService` Context
- **Aggregate Root**: `UserProfile`
  - `Id`: string (unique identifier / user handle)
  - `Name`: string (required)
  - `Sex`: BiologicalSex (Male, Female)
  - `Age`: int (years)
  - `HeightCm`: double (stored in cm, converted seamlessly from ft/inches)
  - `CurrentWeightKg`: double (kg)
  - `TargetWeightKg`: double (kg)
  - `DesiredPaceKgPerWeek`: double (0.25, 0.50, 0.75 kg/week)
  - `ActivityLevel`: ActivityLevel (Sedentary, Light, Moderate, High)
  - `DietaryPreference`: DietaryPreference (PureVeg, LactoVeg, LactoOvo, Vegan, NonVeg)
  - `RegionalCuisine`: string (e.g. "North Indian", "South Indian", "Gujarati")
  - `DiagnosedConditions`: `List<string>` (Diabetes, HTN, Thyroid, Lipids, PCOS, Gout, NAFLD)
  - `Medications`: `List<MedicationEntry>` (DrugName, Dosage, Frequency)
- **Value Objects**:
  - `MedicationEntry`: DrugName, Dosage, Frequency
  - `BmrTdeeResult`: Bmr, Tdee, AdjustedTdee, DeficitCalories, TargetCalories, IdealBodyWeightKg, Bmi, BmiClassification, ClinicalAdjustments, Warnings
  - `MacroDistribution`: TargetCalories, ProteinGrams, CarbsGrams, FatGrams, FiberGrams, VisibleCookingOilCeilingGrams, SodiumLimitMg, SugarCeilingGrams

### 1.2 `Nutrition.VisionService` Context
- **Aggregate Root**: `MealLog`
  - `Id`: string (GUID)
  - `UserId`: string
  - `MealType`: MealType (Breakfast, Lunch, Snack, Dinner)
  - `DishName`: string
  - `PhotoUri`: string?
  - `Items`: `List<FoodItem>`
  - `TotalCalories`: double
  - `TotalProteinGrams`: double
  - `TotalCarbsGrams`: double
  - `TotalFatGrams`: double
  - `TotalFiberGrams`: double
  - `TotalSodiumMg`: double
  - `ConfidenceScore`: double
  - `IsConfidenceGatedPassed`: bool (true if >= 0.70)
  - `IsVerifiedByUser`: bool
  - `WhoComplianceFlags`: `List<string>`
  - `MedicationWarnings`: `List<string>`
  - `DietitianAdvice`: string?
  - `LoggedAt`: DateTime (UTC)
- **Entity**: `FoodItem`
  - `Id`: string
  - `Name`: string
  - `HindiOrRegionalName`: string?
  - `EstimatedPortion`: string
  - `Grams`: double
  - `Calories`: double
  - `ProteinGrams`: double
  - `CarbsGrams`: double
  - `FatGrams`: double
  - `FiberGrams`: double
  - `SodiumMg`: double
  - `CookingMediumEstimate`: string?

### 1.3 `Nutrition.AnalyticsService` Context
- **Aggregate Root**: `DailyCalorieLedger`
  - `Id`: string (GUID)
  - `UserId`: string
  - `Date`: DateOnly
  - `BudgetedCalories`: double
  - `ConsumedCalories`: double
  - `PendingCalories`: double
  - `TargetProteinGrams`: double
  - `ConsumedProteinGrams`: double
  - `TargetCarbsGrams`: double
  - `ConsumedCarbsGrams`: double
  - `TargetFatGrams`: double
  - `ConsumedFatGrams`: double
  - `TargetFiberGrams`: double
  - `ConsumedFiberGrams`: double
  - `SodiumLimitMg`: double
  - `ConsumedSodiumMg`: double
  - `VisibleCookingOilLimitGrams`: double
  - `ConsumedCookingOilGrams`: double
  - `HealthScore`: int (0 - 100)
  - `StreakCount`: int
  - `LoggedMealCount`: int

---

## 2. Indian Meal Analysis JSON Schema Contract

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "IndianMealAnalysisResult",
  "type": "object",
  "properties": {
    "mealType": { "type": "string", "enum": ["Breakfast", "Lunch", "Snack", "Dinner"] },
    "dishName": { "type": "string" },
    "identifiedItems": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "name": { "type": "string" },
          "hindiOrRegionalName": { "type": "string" },
          "estimatedPortion": { "type": "string" },
          "grams": { "type": "number" },
          "calories": { "type": "number" },
          "proteinGrams": { "type": "number" },
          "carbsGrams": { "type": "number" },
          "fatGrams": { "type": "number" },
          "fiberGrams": { "type": "number" },
          "sodiumMg": { "type": "number" },
          "cookingMediumEstimate": { "type": "string" },
          "confidenceScore": { "type": "number" }
        },
        "required": ["name", "estimatedPortion", "grams", "calories", "proteinGrams", "carbsGrams", "fatGrams"]
      }
    },
    "totalCalories": { "type": "number" },
    "totalProteinGrams": { "type": "number" },
    "totalCarbsGrams": { "type": "number" },
    "totalFatGrams": { "type": "number" },
    "totalSodiumMg": { "type": "number" },
    "overallConfidenceScore": { "type": "number" },
    "isConfidenceGatedPassed": { "type": "boolean" },
    "whoComplianceFlags": { "type": "array", "items": { "type": "string" } },
    "medicationWarnings": { "type": "array", "items": { "type": "string" } },
    "dietitianAdvice": { "type": "string" }
  },
  "required": ["mealType", "dishName", "identifiedItems", "totalCalories", "totalProteinGrams", "totalCarbsGrams", "totalFatGrams"]
}
```

---

## 3. SQLite Persistence & Entity Framework Core Mappings

- Configured via `DietTrackerDbContext`:
  - `Profiles` mapped with complex JSON serialization for `DiagnosedConditions` and `Medications`.
  - `Meals` mapped with navigation collection `Items`.
  - `Ledgers` mapped with composite index on `(UserId, Date)`.
