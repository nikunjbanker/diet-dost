# Data Models, DDD Aggregates & Contracts
> **Specification Version**: `v1.3.1 (Production & Living SDD)`  
> **Persistence**: Swappable SQLite V1 / PostgreSQL  
> **Architecture Pattern**: Domain-Driven Design (DDD) & Clean Architecture  

---

## 1. Domain Aggregates & Value Objects

### 1.0 `Nutrition.Identity` Context
- **Aggregate Root**: `ApplicationUser`
  - `Id`: string (unique identifier / GUID)
  - `Email`: string (normalized unique index)
  - `MobileNumber`: string (normalized index)
  - `PasswordHash`: string (PBKDF2-HMAC-SHA512)
  - `SecurityStamp`: string (session invalidation stamp)
  - `Role`: UserRole (`User`, `Admin`, `SuperAdmin`)
  - `Tier`: UserTier (`Free`, `Basic`, `Premium`, `SuperAdmin`)
  - `IsEmailVerified`: bool (mandatory for account activation)
  - `IsMobileVerified`: bool (optional/deferred for SMS cost control)
  - `IsActive`: bool
  - `TermsAcceptedAtUtc`: DateTime? (mandatory DPDPA forensic timestamp)
  - `TermsVersionAccepted`: string? (e.g. "v1.0-202609")
  - `HealthConsentAcceptedAtUtc`: DateTime? (mandatory Sensitive Health Data consent)
  - `HealthConsentVersionAccepted`: string? (e.g. "v1.0-202609")
  - `ConsentIpAddress`: string? (client IP recorded at registration)
  - `ConsentUserAgent`: string? (client user agent recorded at registration)
  - `CreatedAtUtc`: DateTime (universal UTC)
  - `LastLoginAtUtc`: DateTime? (universal UTC)
- **Entities & Dynamic Quota Models**:
  - `VerificationOtp`: Id, UserId, Target, OtpCodeHash (SHA-256), Channel (Email, Sms), ExpiresAtUtc (5m TTL), AttemptCount (max 3), IsUsed, CreatedAtUtc
  - `TierFeatureConfiguration`: Id, Tier, DailyAiDetectionLimit (Free: 1, Basic: 7, Premium: 30, SuperAdmin: -1), AllowPhotoCompare, AllowDataExport, AnalyticsHistoryDays, Description, UpdatedAtUtc, UpdatedByUserId
  - `AiUsageLog`: Id, UserId, OperationType (PhotoDetection, TextDetection, ProgressCompare), ModelId, EstimatedTokensUsed, LatencyMs, IsSuccess, ErrorReason, TimestampUtc

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
  - `Timezone`: string (IANA format, e.g. "Asia/Kolkata", default: "Asia/Kolkata")
  - `DiagnosedConditions`: `List<string>` (Diabetes, HTN, Thyroid, Lipids, PCOS, Gout, NAFLD)
  - `Medications`: `List<MedicationEntry>` (DrugName, Dosage, Frequency)
- **Value Objects**:
  - `MedicationEntry`: DrugName, Dosage, Frequency
  - `BmrTdeeResult`: Bmr, Tdee, AdjustedTdee, DeficitCalories, TargetCalories, IdealBodyWeightKg, Bmi, BmiClassification, ClinicalAdjustments, Warnings
  - `MacroDistribution`: TargetCalories, ProteinGrams, CarbsGrams, FatGrams, FiberGrams, SugarCeilingGrams, VisibleCookingOilCeilingGrams, SodiumLimitMg

### 1.2 `Nutrition.VisionService` Context
- **Aggregate Root**: `MealLog`
  - `Id`: string (GUID)
  - `UserId`: string
  - `MealType`: MealType (Breakfast, Lunch, Snack, Dinner)
  - `DishName`: string
  - `PhotoUri`: string?
  - `Items`: `List<FoodItemRecord>`
  - `TotalCalories`: double (recalculated from items * quantity + cooking fat)
  - `TotalProteinGrams`: double (sum of item protein * quantity)
  - `TotalCarbsGrams`: double (sum of item carbs * quantity)
  - `TotalFatGrams`: double (sum of item fat * quantity + added fat)
  - `TotalFiberGrams`: double (sum of item dietary fiber * quantity)
  - `TotalSugarGrams`: double (sum of item free sugar * quantity)
  - `TotalSodiumMg`: double (sum of item sodium * quantity)
  - `AddedGheeKcal`: double (1-tap cooking fat modifier)
  - `AddedTadkaKcal`: double (1-tap cooking fat modifier)
  - `OverallConfidenceScore`: double
  - `IsConfidencePassed`: bool (true if OverallConfidenceScore >= 0.70)
  - `IsVerifiedByUser`: bool
  - `DetectedByModel`: string? (Attributed AI model e.g. `gemini-3-flash-preview` or `Local Clinical Engine`)
  - `WhoComplianceFlags`: `List<string>`
  - `MedicationWarnings`: `List<string>`
  - `ConditionSpecificAdvice`: string?
  - `DietitianAdvice`: string?
  - `AiFeedbackRating`: string?
  - `AiFeedbackRemarks`: string?
  - `LoggedAt`: DateTime (persisted in UTC, presented in UserProfile.Timezone)
  - `RecalculateTotals()`: Domain method performing synchronous recalculation across all 6 macronutrients
- **Entity**: `FoodItemRecord`
  - `Id`: string
  - `MealLogId`: string
  - `Name`: string
  - `OriginalDetection`: string (initial detection before user correction)
  - `HindiOrRegionalName`: string?
  - `EstimatedPortion`: string (e.g. "1 Katori", "2 Phulkas", "1.5 Cup")
  - `Quantity`: double (editable portion multiplier, default: 1.0)
  - `Grams`: double
  - `Calories`: double (unit baseline calories)
  - `ProteinGrams`: double (unit baseline protein)
  - `CarbsGrams`: double (unit baseline carbs)
  - `FatGrams`: double (unit baseline fat)
  - `FiberGrams`: double (unit baseline dietary fiber)
  - `SugarGrams`: double (unit baseline free sugar)
  - `SodiumMg`: double (unit baseline sodium)
  - `CookingMediumEstimate`: string (default: "Standard Home Cooking")
  - `ConfidenceScore`: double
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
  - `CreatedAtUtc`: DateTime (UTC)
  - `FrequencyCount`: int (Usage frequency ranking for system prompt injection)

### 1.3 `Nutrition.AnalyticsService` Context
- **Aggregate Root**: `DailyCalorieLedger`
  - `Id`: string (GUID)
  - `UserId`: string
  - `Date`: DateOnly (normalized against user circadian local day boundary)
  - `BudgetedCalories`: double
  - `ConsumedCalories`: double
  - `PendingCalories`: double (computed: max(0, BudgetedCalories - ConsumedCalories))
  - `TargetProteinGrams`: double
  - `ConsumedProteinGrams`: double
  - `TargetCarbsGrams`: double
  - `ConsumedCarbsGrams`: double
  - `TargetFatGrams`: double
  - `ConsumedFatGrams`: double
  - `TargetFiberGrams`: double (ICMR-NIN 30.0g target)
  - `ConsumedFiberGrams`: double
  - `TargetSugarGrams`: double (ICMR-NIN / WHO max 25.0g free sugar ceiling)
  - `ConsumedSugarGrams`: double
  - `SodiumLimitMg`: double (2000.0 mg ceiling)
  - `ConsumedSodiumMg`: double
  - `VisibleCookingOilLimitGrams`: double (25.0g ceiling)
  - `VisibleCookingOilGrams`: double
  - `WaterIntakeMl`: int
  - `WaterTargetMl`: int (default: 3000 ml)
  - `ConsistencyStreakDays`: int
  - `HealthScore`: int (0 - 100)
  - `EarnedBadges`: `List<string>`
  - `DostMessage`: string
  - `RecalculateLedger(List<MealLog> dayMeals)`: Aggregates all meals logged within circadian boundary

### 1.4 `Nutrition.ProgressService` Context
- **Aggregate Root**: `ProgressPhoto` (Visual Transformation Tracking)
  - `Id`: string (GUID)
  - `UserId`: string
  - `CapturedAtUtc`: DateTime (Universal UTC ISO 8601)
  - `WeightKg`: double
  - `PhotoType`: ProgressPhotoType (Face, FullBodyFront, FullBodySide, FullBodyBack)
  - `PhotoUri`: string (or SVG fallback if inaccessible)
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
          "quantity": { "type": "number", "default": 1.0 },
          "grams": { "type": "number" },
          "calories": { "type": "number" },
          "proteinGrams": { "type": "number" },
          "carbsGrams": { "type": "number" },
          "fatGrams": { "type": "number" },
          "fiberGrams": { "type": "number" },
          "sugarGrams": { "type": "number" },
          "sodiumMg": { "type": "number" },
          "cookingMediumEstimate": { "type": "string" },
          "confidenceScore": { "type": "number" }
        },
        "required": ["name", "estimatedPortion", "calories", "proteinGrams", "carbsGrams", "fatGrams", "fiberGrams", "sugarGrams"]
      }
    },
    "totalCalories": { "type": "number" },
    "totalProteinGrams": { "type": "number" },
    "totalCarbsGrams": { "type": "number" },
    "totalFatGrams": { "type": "number" },
    "totalFiberGrams": { "type": "number" },
    "totalSugarGrams": { "type": "number" },
    "totalSodiumMg": { "type": "number" },
    "overallConfidenceScore": { "type": "number" },
    "isConfidenceGatedPassed": { "type": "boolean" },
    "whoComplianceFlags": { "type": "array", "items": { "type": "string" } },
    "medicationWarnings": { "type": "array", "items": { "type": "string" } },
    "conditionSpecificAdvice": { "type": "string" },
    "dietitianAdvice": { "type": "string" },
    "photoUri": { "type": "string" }
  },
  "required": ["mealType", "dishName", "identifiedItems", "totalCalories", "totalProteinGrams", "totalCarbsGrams", "totalFatGrams", "totalFiberGrams", "totalSugarGrams"]
}
```

---

## 3. SQLite Persistence & Entity Framework Core Mappings

Configured in `Nutrition.Infrastructure.Persistence.DietTrackerDbContext`:
- **Collection ValueComparers (Zero Warnings & Change-Tracking Guarantee)**:
  - `ValueComparer<List<string>>` registered on `UserProfile.DiagnosedConditions`, `MealLog.WhoComplianceFlags`, `MealLog.MedicationWarnings`, and `DailyCalorieLedger.EarnedBadges`.
  - `ValueComparer<List<MedicationEntry>>` registered on `UserProfile.Medications`.
  - Guarantees EF Core accurately tracks additions, updates, and removals within collection properties without data loss.
- **Universal UTC ValueConverter**:
  - Registered across all `DateTime` and `DateTime?` properties on all entity types in `OnModelCreating`.
  - Persists all timestamps to SQLite as UTC ISO 8601 string/value (`.ToUniversalTime()`).
  - Converts all queries from SQLite back into `DateTimeKind.Utc` via `DateTime.SpecifyKind(v, DateTimeKind.Utc)`.
- **AutoInclude Navigation**:
  - `MealLog.Items` navigation is configured with `.AutoInclude()` and cascade delete.
- **Schema-Aware SQLite Startup Migration**:
  - `Program.cs` inspects `PRAGMA table_info("{tableName}")` prior to executing `ALTER TABLE ... ADD COLUMN` statements, preventing SQLite duplicate column exceptions.
  - Adds `Profiles.Timezone` column (`TEXT NOT NULL DEFAULT 'Asia/Kolkata'`) with backward-compatible PRAGMA check.
  - Adds `FoodItems.FiberGrams` (`REAL NOT NULL DEFAULT 0`) and `FoodItems.SugarGrams` (`REAL NOT NULL DEFAULT 0`).
  - Adds `Meals.TotalFiberGrams` (`REAL NOT NULL DEFAULT 0`) and `Meals.TotalSugarGrams` (`REAL NOT NULL DEFAULT 0`).
  - Adds `Ledgers.TargetSugarGrams` (`REAL NOT NULL DEFAULT 25.0`) and `Ledgers.ConsumedSugarGrams` (`REAL NOT NULL DEFAULT 0.0`).
  - `CREATE INDEX IF NOT EXISTS "IX_ProgressPhotos_UserId_CapturedAtUtc"` on `ProgressPhotos(UserId, CapturedAtUtc)`.
  - `CREATE INDEX IF NOT EXISTS "IX_ProgressPhotos_UserId_PhotoType"` on `ProgressPhotos(UserId, PhotoType)`.

---

## 4. Textual Food AI Search & Item Estimation Contracts

### 4.1 `FoodItemEstimateRequest` & `FoodItemNutritionEstimate`
Endpoint: `POST /api/meals/estimate-item`

```csharp
public record FoodItemEstimateRequest(
    string Name, 
    string? Portion, 
    bool UseAi = true, 
    string? UserId = null, 
    string? MealType = null
);

public record FoodItemNutritionEstimate(
    string NormalizedName,
    string HindiOrRegionalName,
    string EstimatedPortion,
    double Grams,
    double Calories,
    double ProteinGrams,
    double CarbsGrams,
    double FatGrams,
    double FiberGrams,
    double SodiumMg,
    string CookingMediumEstimate,
    string Source,
    double ConfidenceScore,
    double SugarGrams
);
```

- When `UseAi = true` and `UserId` is supplied:
  - Clinical profile (`UserProfile`) and continuous memory (`UserCorrectionRecord`) are injected into the agent prompt.
  - Returns complete clinical macronutrient breakdown (`Calories`, `ProteinGrams`, `CarbsGrams`, `FatGrams`, `FiberGrams`, `SugarGrams`, `SodiumMg`).
- Real-time client-side synchronization:
  - Top Aggregated Nutrition Summary Bar (`#review-macro-summary-bar`) pulses and recalculates live.
  - Clinical Dietitian Advice is dynamically regenerated.
  - WHO compliance flags (Sodium > 800mg, Free Sugar > 15g, High Fat > 35g) evaluate and alert dynamically.

