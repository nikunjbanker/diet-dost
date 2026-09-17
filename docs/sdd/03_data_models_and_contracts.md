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
  - `Items`: `List<FoodItemRecord>`
  - `TotalCalories`: double
  - `TotalProteinGrams`: double
  - `TotalCarbsGrams`: double
  - `TotalFatGrams`: double
  - `TotalFiberGrams`: double
  - `TotalSodiumMg`: double
  - `ConfidenceScore`: double
  - `IsConfidenceGatedPassed`: bool (true if >= 0.70)
  - `IsVerifiedByUser`: bool
  - `DetectedByModel`: string? (Attributed AI model e.g. `gemini-3-flash-preview` or `Local Clinical Engine`)
  - `WhoComplianceFlags`: `List<string>`
  - `MedicationWarnings`: `List<string>`
  - `DietitianAdvice`: string?
  - `LoggedAt`: DateTime (UTC)
- **Entity**: `FoodItemRecord`
  - `Id`: string
  - `MealLogId`: string
  - `Name`: string
  - `HindiOrRegionalName`: string?
  - `EstimatedPortion`: string
  - `Calories`: double
  - `ProteinGrams`: double
  - `CarbsGrams`: double
  - `FatGrams`: double
  - `FiberGrams`: double
  - `SodiumMg`: double
  - `OriginalDetection`: string (Initial detection before user correction)
- **Aggregate Root**: `UserCorrectionRecord` (Continuous Model Training & Memory)
  - `Id`: string
  - `UserId`: string
  - `OriginalDetectedItem`: string
  - `CorrectedItemName`: string
  - `HindiOrRegionalName`: string
  - `EstimatedPortion`: string
  - `Calories`: double
  - `ProteinGrams`: double
  - `CarbsGrams`: double
  - `FatGrams`: double
  - `MealType`: string
  - `CreatedAtUtc`: DateTime
  - `FrequencyCount`: int (Usage frequency ranking for system prompt injection)

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
  - `EarnedBadges`: `List<string>`
  - `HealthScore`: int (0 - 100)
  - `StreakCount`: int
  - `LoggedMealCount`: int

### 1.4 `Nutrition.ProgressService` Context
- **Aggregate Root**: `ProgressPhoto` (Visual Transformation Tracking)
  - `Id`: string (GUID)
  - `UserId`: string
  - `CapturedAtUtc`: DateTime
  - `WeightKg`: double
  - `PhotoType`: ProgressPhotoType (Face, FullBodyFront, FullBodySide, FullBodyBack)
  - `PhotoUri`: string
  - `IsBaseline`: bool
  - `Notes`: string?

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
    "detectedByModel": { "type": "string" },
    "identifiedItems": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "name": { "type": "string" },
          "hindiOrRegionalName": { "type": "string" },
          "estimatedPortion": { "type": "string" },
          "calories": { "type": "number" },
          "proteinGrams": { "type": "number" },
          "carbsGrams": { "type": "number" },
          "fatGrams": { "type": "number" },
          "fiberGrams": { "type": "number" },
          "sodiumMg": { "type": "number" },
          "cookingMediumEstimate": { "type": "string" },
          "confidenceScore": { "type": "number" }
        },
        "required": ["name", "estimatedPortion", "calories", "proteinGrams", "carbsGrams", "fatGrams"]
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

Configured in `Nutrition.Infrastructure.Persistence.DietTrackerDbContext`:
- **Collection ValueComparers (Zero Warnings & Change-Tracking Guarantee)**:
  - `ValueComparer<List<string>>` registered on `UserProfile.DiagnosedConditions`, `MealLog.WhoComplianceFlags`, `MealLog.MedicationWarnings`, and `DailyCalorieLedger.EarnedBadges`.
  - `ValueComparer<List<MedicationEntry>>` registered on `UserProfile.Medications`.
  - Guarantees EF Core accurately tracks additions, updates, and removals within collection properties without data loss.
- **AutoInclude Navigation**:
  - `MealLog.Items` navigation is configured with `.AutoInclude()` and cascade delete.
- **Schema-Aware SQLite Startup Migration**:
  - `Program.cs` inspects `PRAGMA table_info("{tableName}")` prior to executing `ALTER TABLE ... ADD COLUMN` statements, preventing SQLite duplicate column exceptions.
  - `CREATE INDEX IF NOT EXISTS` applied for `ProgressPhotos` indices (`UserId, CapturedAtUtc` and `UserId, PhotoType`).
