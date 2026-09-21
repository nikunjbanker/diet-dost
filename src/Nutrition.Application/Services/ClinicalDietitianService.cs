using Nutrition.Application.Common;
using Nutrition.Domain.Clinical;
using Nutrition.Domain.Model.Ledger;
using Nutrition.Domain.Model.Meal;
using Nutrition.Domain.Model.Profile;

namespace Nutrition.Application.Services;

public record AnalyticsProjection(
    string Period, // "7D", "30D", "90D"
    double TotalDeficitKcal,
    double ProjectedWeightLossKg,
    double AverageDailyCalories,
    double ProteinCompliancePercent,
    int SodiumWarningCount,
    int SugarWarningCount,
    bool PlateauRiskDetected,
    List<DailyTrendPoint> DailyTrends
);

public record DailyTrendPoint(
    string Date,
    double ConsumedCalories,
    double BudgetCalories,
    double ProteinGrams,
    double SodiumMg,
    int HealthScore
);

/// <summary>
/// Coordinates profile, meal, ledger, and analytics operations for the WebGateway.
/// </summary>
public class ClinicalDietitianService
{
    private readonly IRepository<UserProfile> _profileRepo;
    private readonly IRepository<MealLog> _mealRepo;
    private readonly IRepository<FoodItemRecord> _foodItemRepo;
    private readonly IRepository<DailyCalorieLedger> _ledgerRepo;
    private readonly IUnitOfWork _uow;

    public ClinicalDietitianService(
        IRepository<UserProfile> profileRepo,
        IRepository<MealLog> mealRepo,
        IRepository<FoodItemRecord> foodItemRepo,
        IRepository<DailyCalorieLedger> ledgerRepo,
        IUnitOfWork uow)
    {
        _profileRepo = profileRepo;
        _mealRepo = mealRepo;
        _foodItemRepo = foodItemRepo;
        _ledgerRepo = ledgerRepo;
        _uow = uow;
    }

    public async Task<UserProfile> SaveProfileAsync(UserProfile profile, CancellationToken ct = default)
    {
        profile.ValidateIntakeCompleteness();
        profile.UpdatedAtUtc = DateTime.UtcNow;

        var existing = await _profileRepo.GetByIdAsync(profile.Id, ct);
        if (existing is null)
        {
            await _profileRepo.AddAsync(profile, ct);
            await _uow.SaveChangesAsync(ct);
        }
        else
        {
            existing.Name = profile.Name;
            existing.Sex = profile.Sex;
            existing.Age = profile.Age;
            existing.HeightCm = profile.HeightCm;
            existing.CurrentWeightKg = profile.CurrentWeightKg;
            existing.TargetWeightKg = profile.TargetWeightKg;
            existing.DesiredPaceKgPerWeek = profile.DesiredPaceKgPerWeek;
            existing.ActivityLevel = profile.ActivityLevel;
            existing.DietaryPreference = profile.DietaryPreference;
            existing.RegionalCuisine = profile.RegionalCuisine;
            existing.DiagnosedConditions = profile.DiagnosedConditions;
            existing.Medications = profile.Medications;
            existing.Timezone = profile.Timezone;
            existing.UpdatedAtUtc = profile.UpdatedAtUtc;
            await _profileRepo.UpdateAsync(existing, ct);
            await _uow.SaveChangesAsync(ct);
            profile = existing;
        }

        // Immediately update today's ledger targets with new clinical budget using user's local day
        var userTz = GetUserTimeZoneInfo(profile.Timezone);
        var userToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTz));
        await GetOrCreateDailyLedgerAsync(profile.Id, userToday, ct);

        return profile;
    }

    public async Task<UserProfile?> GetProfileAsync(string userId, CancellationToken ct = default)
    {
        return await _profileRepo.GetByIdAsync(userId, ct);
    }

    /// <summary>
    /// Calculates the clinically constrained calorie budget for a complete profile.
    /// </summary>
    /// <param name="profile">The validated user profile.</param>
    /// <returns>BMR, TDEE, target calories, clinical adjustments, and warnings.</returns>
    public BmrTdeeResult CalculateTargetBudget(UserProfile profile)
    {
        return ClinicalCalculators.CalculateCaloricBudget(profile);
    }

    /// <summary>
    /// Calculates macro and safety-nutrient targets from the user's clinical budget.
    /// </summary>
    /// <param name="profile">The validated user profile.</param>
    /// <returns>Protein, carbohydrate, fat, fiber, sodium, oil, and sugar targets.</returns>
    public MacroDistribution CalculateMacros(UserProfile profile)
    {
        var budget = ClinicalCalculators.CalculateCaloricBudget(profile);
        return ClinicalCalculators.CalculateMacroDistribution(profile, budget);
    }

    public async Task<DailyCalorieLedger> GetOrCreateDailyLedgerAsync(string userId, DateOnly date, CancellationToken ct = default)
    {
        var ledgers = await _ledgerRepo.FindAsync(l => l.UserId == userId && l.Date == date, ct);
        var ledger = ledgers.FirstOrDefault();

        var profile = await _profileRepo.GetByIdAsync(userId, ct);
        var budget = profile != null ? ClinicalCalculators.CalculateCaloricBudget(profile) : null;
        var macros = (profile != null && budget != null) ? ClinicalCalculators.CalculateMacroDistribution(profile, budget) : null;

        if (ledger is null)
        {
            ledger = new DailyCalorieLedger
            {
                UserId = userId,
                Date = date,
                BudgetedCalories = budget?.TargetCalories ?? 1600.0,
                TargetProteinGrams = macros?.ProteinGrams ?? 75.0,
                TargetCarbsGrams = macros?.CarbsGrams ?? 180.0,
                TargetFatGrams = macros?.FatGrams ?? 45.0,
                TargetFiberGrams = macros?.FiberGrams ?? 30.0,
                SodiumLimitMg = macros?.SodiumLimitMg ?? 2000.0,
                VisibleCookingOilLimitGrams = macros?.VisibleCookingOilCeilingGrams ?? 25.0
            };
            await _ledgerRepo.AddAsync(ledger, ct);
            await _uow.SaveChangesAsync(ct);
        }
        else if (budget != null)
        {
            ledger.BudgetedCalories = budget.TargetCalories;
            if (macros != null)
            {
                ledger.TargetProteinGrams = macros.ProteinGrams;
                ledger.TargetCarbsGrams = macros.CarbsGrams;
                ledger.TargetFatGrams = macros.FatGrams;
                ledger.TargetFiberGrams = macros.FiberGrams;
                ledger.SodiumLimitMg = macros.SodiumLimitMg;
                ledger.VisibleCookingOilLimitGrams = macros.VisibleCookingOilCeilingGrams;
            }
        }

        // Fetch day's meals to synchronize ledger using user's local timezone
        var userTz = GetUserTimeZoneInfo(profile?.Timezone);
        var allUserMeals = await _mealRepo.FindAsync(m => m.UserId == userId, ct);
        var dayMeals = allUserMeals.Where(m =>
        {
            var userLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(m.LoggedAt, userTz));
            var utcDate = DateOnly.FromDateTime(m.LoggedAt);
            return userLocalDate == date || utcDate == date;
        }).ToList();
        ledger.RecalculateLedger(dayMeals);
        await _ledgerRepo.UpdateAsync(ledger, ct);
        await _uow.SaveChangesAsync(ct);

        return ledger;
    }

    public async Task<MealLog> LogMealAsync(MealLog meal, CancellationToken ct = default)
    {
        var profile = await _profileRepo.GetByIdAsync(meal.UserId, ct);
        if (profile != null)
        {
            // Verify clinical rules & WHO compliance
            var conditions = profile.DiagnosedConditions.Select(c => c.ToLowerInvariant()).ToList();
            var meds = profile.Medications.Select(m => m.DrugName.ToLowerInvariant()).ToList();

            if (meal.TotalSodiumMg > 800)
            {
                meal.WhoComplianceFlags.Add($"High single-meal sodium ({meal.TotalSodiumMg:F0}mg). WHO suggests keeping whole day < 2,000mg.");
            }

            if (conditions.Any(c => c.Contains("diabet")) && meal.TotalCarbsGrams > 60)
            {
                meal.WhoComplianceFlags.Add($"Carb spike notice: Meal contains {meal.TotalCarbsGrams:F0}g carbs. Ensure adequate fiber (e.g. cucumber salad) to balance Glycemic Load.");
            }

            if (conditions.Any(c => c.Contains("hypertens")) && meal.TotalSodiumMg > 500)
            {
                meal.WhoComplianceFlags.Add("Hypertension Alert: Limit achaar/papad/salted snacks to maintain sodium under 1,500mg/day.");
            }

            if (meds.Any(m => m.Contains("telmisartan") || m.Contains("ramipril")) &&
                meal.Items.Any(i => i.Name.ToLowerInvariant().Contains("coconut water") || i.Name.ToLowerInvariant().Contains("diet salt")))
            {
                meal.MedicationWarnings.Add("Medication Warning: High potassium item detected with ARB/ACE inhibitor. Avoid excessive potassium to prevent hyperkalemia.");
            }
        }

        meal.RecalculateTotals();
        meal.LoggedAt = meal.LoggedAt.Kind == DateTimeKind.Utc ? meal.LoggedAt : meal.LoggedAt.ToUniversalTime();
        await _mealRepo.AddAsync(meal, ct);
        await _uow.SaveChangesAsync(ct);

        // Update day ledger using user's local timezone
        var userTz = GetUserTimeZoneInfo(profile?.Timezone);
        var mealLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(meal.LoggedAt, userTz));
        await GetOrCreateDailyLedgerAsync(meal.UserId, mealLocalDate, ct);

        return meal;
    }

    public async Task<MealLog?> GetMealByIdAsync(string mealId, CancellationToken ct = default)
    {
        var meals = await _mealRepo.FindAsync(m => m.Id == mealId, ct);
        return meals.FirstOrDefault();
    }

    public async Task<MealLog?> UpdateMealAsync(MealLog updatedMeal, CancellationToken ct = default)
    {
        var existing = (await _mealRepo.FindAsync(m => m.Id == updatedMeal.Id, ct)).FirstOrDefault();
        if (existing is null)
        {
            return null;
        }

        // Update core scalar properties
        existing.DishName = updatedMeal.DishName;
        existing.MealType = updatedMeal.MealType;
        if (updatedMeal.LoggedAt != default)
        {
            existing.LoggedAt = updatedMeal.LoggedAt.ToUniversalTime();
        }
        existing.AddedGheeKcal = updatedMeal.AddedGheeKcal;
        existing.AddedTadkaKcal = updatedMeal.AddedTadkaKcal;
        if (!string.IsNullOrWhiteSpace(updatedMeal.DietitianAdvice))
        {
            existing.DietitianAdvice = updatedMeal.DietitianAdvice;
        }
        if (!string.IsNullOrWhiteSpace(updatedMeal.AiFeedbackRating))
        {
            existing.AiFeedbackRating = updatedMeal.AiFeedbackRating;
        }
        if (!string.IsNullOrWhiteSpace(updatedMeal.AiFeedbackRemarks))
        {
            existing.AiFeedbackRemarks = updatedMeal.AiFeedbackRemarks;
        }

        // Synchronize child food items
        var updatedItemIds = updatedMeal.Items
            .Where(i => !string.IsNullOrWhiteSpace(i.Id))
            .Select(i => i.Id)
            .ToHashSet();

        // Remove deleted items
        var toRemove = existing.Items.Where(i => !updatedItemIds.Contains(i.Id)).ToList();
        foreach (var item in toRemove)
        {
            existing.Items.Remove(item);
            await _foodItemRepo.DeleteAsync(item.Id, ct);
        }

        // Update existing or add new items
        foreach (var item in updatedMeal.Items)
        {
            var existingItem = existing.Items.FirstOrDefault(i => i.Id == item.Id);
            if (existingItem != null)
            {
                existingItem.Name = item.Name;
                existingItem.HindiOrRegionalName = item.HindiOrRegionalName;
                existingItem.EstimatedPortion = item.EstimatedPortion;
                existingItem.Quantity = item.Quantity;
                existingItem.Grams = item.Grams;
                existingItem.Calories = item.Calories;
                existingItem.ProteinGrams = item.ProteinGrams;
                existingItem.CarbsGrams = item.CarbsGrams;
                existingItem.FatGrams = item.FatGrams;
                existingItem.FiberGrams = item.FiberGrams;
                existingItem.SugarGrams = item.SugarGrams;
                existingItem.SodiumMg = item.SodiumMg;
                existingItem.CookingMediumEstimate = item.CookingMediumEstimate;
                await _foodItemRepo.UpdateAsync(existingItem, ct);
            }
            else
            {
                var newItem = new FoodItemRecord
                {
                    Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString() : item.Id,
                    MealLogId = existing.Id,
                    Name = item.Name,
                    OriginalDetection = string.IsNullOrWhiteSpace(item.OriginalDetection) ? item.Name : item.OriginalDetection,
                    HindiOrRegionalName = item.HindiOrRegionalName,
                    EstimatedPortion = item.EstimatedPortion,
                    Quantity = item.Quantity,
                    Grams = item.Grams,
                    Calories = item.Calories,
                    ProteinGrams = item.ProteinGrams,
                    CarbsGrams = item.CarbsGrams,
                    FatGrams = item.FatGrams,
                    FiberGrams = item.FiberGrams,
                    SugarGrams = item.SugarGrams,
                    SodiumMg = item.SodiumMg,
                    CookingMediumEstimate = item.CookingMediumEstimate,
                    ConfidenceScore = item.ConfidenceScore > 0 ? item.ConfidenceScore : 0.85
                };
                existing.Items.Add(newItem);
                await _foodItemRepo.AddAsync(newItem, ct);
            }
        }

        existing.RecalculateTotals();

        // Clinical compliance re-assessment
        var profile = await _profileRepo.GetByIdAsync(existing.UserId, ct);
        if (profile != null)
        {
            existing.WhoComplianceFlags.Clear();
            existing.MedicationWarnings.Clear();

            var conditions = profile.DiagnosedConditions.Select(c => c.ToLowerInvariant()).ToList();
            var meds = profile.Medications.Select(m => m.DrugName.ToLowerInvariant()).ToList();

            if (existing.TotalSodiumMg > 800)
            {
                existing.WhoComplianceFlags.Add($"High single-meal sodium ({existing.TotalSodiumMg:F0}mg). WHO suggests keeping whole day < 2,000mg.");
            }

            if (conditions.Any(c => c.Contains("diabet")) && existing.TotalCarbsGrams > 60)
            {
                existing.WhoComplianceFlags.Add($"Carb spike notice: Meal contains {existing.TotalCarbsGrams:F0}g carbs. Ensure adequate fiber (e.g. cucumber salad) to balance Glycemic Load.");
            }

            if (conditions.Any(c => c.Contains("hypertens")) && existing.TotalSodiumMg > 500)
            {
                existing.WhoComplianceFlags.Add("Hypertension Alert: Limit achaar/papad/salted snacks to maintain sodium under 1,500mg/day.");
            }

            if (meds.Any(m => m.Contains("telmisartan") || m.Contains("ramipril")) &&
                existing.Items.Any(i => i.Name.ToLowerInvariant().Contains("coconut water") || i.Name.ToLowerInvariant().Contains("diet salt")))
            {
                existing.MedicationWarnings.Add("Medication Warning: High potassium item detected with ARB/ACE inhibitor. Avoid excessive potassium to prevent hyperkalemia.");
            }
        }

        await _mealRepo.UpdateAsync(existing, ct);
        await _uow.SaveChangesAsync(ct);

        // Synchronize and update daily ledger using user's local timezone
        var userTz = GetUserTimeZoneInfo(profile?.Timezone);
        var mealLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(existing.LoggedAt, userTz));
        await GetOrCreateDailyLedgerAsync(existing.UserId, mealLocalDate, ct);

        return existing;
    }

    public async Task<bool> DeleteMealAsync(string mealId, CancellationToken ct = default)
    {
        var meal = (await _mealRepo.FindAsync(m => m.Id == mealId, ct)).FirstOrDefault();
        if (meal is null)
        {
            return false;
        }

        var userId = meal.UserId;
        var profile = await _profileRepo.GetByIdAsync(userId, ct);
        var userTz = GetUserTimeZoneInfo(profile?.Timezone);
        var mealLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(meal.LoggedAt, userTz));

        // Delete child items explicitly
        if (meal.Items != null && meal.Items.Count > 0)
        {
            foreach (var item in meal.Items)
            {
                await _foodItemRepo.DeleteAsync(item.Id, ct);
            }
        }

        await _mealRepo.DeleteAsync(meal.Id, ct);
        await _uow.SaveChangesAsync(ct);

        // Recalculate daily ledger for this date
        await GetOrCreateDailyLedgerAsync(userId, mealLocalDate, ct);
        return true;
    }

    public async Task<AnalyticsProjection> GetAnalyticsProjectionAsync(string userId, string period, CancellationToken ct = default)
    {
        var normPeriod = (period ?? "7D").ToUpperInvariant();
        var profile = await _profileRepo.GetByIdAsync(userId, ct);
        var userTz = GetUserTimeZoneInfo(profile?.Timezone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTz));
        var budgetObj = profile != null ? ClinicalCalculators.CalculateCaloricBudget(profile) : null;
        var targetBudget = budgetObj?.TargetCalories ?? 1600.0;
        var targetProtein = (profile != null && budgetObj != null) 
            ? ClinicalCalculators.CalculateMacroDistribution(profile, budgetObj).ProteinGrams 
            : 75.0;

        if (normPeriod is "1D" or "DAILY")
        {
            var allUserMeals = await _mealRepo.FindAsync(m => m.UserId == userId, ct);
            var meals = allUserMeals.Where(m => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(m.LoggedAt, userTz)) == today).ToList();
            var todayLedger = (await _ledgerRepo.FindAsync(l => l.UserId == userId && l.Date == today, ct)).FirstOrDefault();

            double bFast = Math.Round(meals.Where(m => m.MealType == MealType.Breakfast).Sum(m => m.TotalCalories), 0);
            double lunch = Math.Round(meals.Where(m => m.MealType == MealType.Lunch).Sum(m => m.TotalCalories), 0);
            double snack = Math.Round(meals.Where(m => m.MealType == MealType.Snack).Sum(m => m.TotalCalories), 0);
            double dinner = Math.Round(meals.Where(m => m.MealType == MealType.Dinner).Sum(m => m.TotalCalories), 0);

            var trends = new List<DailyTrendPoint>
            {
                new("Breakfast", bFast, Math.Round(targetBudget * 0.25, 0), meals.Where(m => m.MealType == MealType.Breakfast).Sum(m => m.TotalProteinGrams), meals.Where(m => m.MealType == MealType.Breakfast).Sum(m => m.TotalSodiumMg), 90),
                new("Lunch", lunch, Math.Round(targetBudget * 0.35, 0), meals.Where(m => m.MealType == MealType.Lunch).Sum(m => m.TotalProteinGrams), meals.Where(m => m.MealType == MealType.Lunch).Sum(m => m.TotalSodiumMg), 90),
                new("Snack", snack, Math.Round(targetBudget * 0.15, 0), meals.Where(m => m.MealType == MealType.Snack).Sum(m => m.TotalProteinGrams), meals.Where(m => m.MealType == MealType.Snack).Sum(m => m.TotalSodiumMg), 90),
                new("Dinner", dinner, Math.Round(targetBudget * 0.25, 0), meals.Where(m => m.MealType == MealType.Dinner).Sum(m => m.TotalProteinGrams), meals.Where(m => m.MealType == MealType.Dinner).Sum(m => m.TotalSodiumMg), 90)
            };

            double totalConsumed = todayLedger?.ConsumedCalories ?? (bFast + lunch + snack + dinner);
            double deficit = targetBudget - totalConsumed;
            double proteinConsumed = todayLedger?.ConsumedProteinGrams ?? meals.Sum(m => m.TotalProteinGrams);
            double proteinCompliance = targetProtein > 0 ? Math.Min(100.0, Math.Round((proteinConsumed / targetProtein) * 100, 1)) : 100.0;

            return new AnalyticsProjection(
                Period: "1D",
                TotalDeficitKcal: Math.Round(deficit, 0),
                ProjectedWeightLossKg: Math.Round(Math.Max(0, deficit) / 7700.0, 2),
                AverageDailyCalories: Math.Round(totalConsumed, 0),
                ProteinCompliancePercent: proteinCompliance,
                SodiumWarningCount: (todayLedger != null && todayLedger.ConsumedSodiumMg > todayLedger.SodiumLimitMg) ? 1 : 0,
                SugarWarningCount: 0,
                PlateauRiskDetected: false,
                DailyTrends: trends
            );
        }

        if (normPeriod is "365D" or "YEARLY" or "1Y")
        {
            var startDate = new DateOnly(today.Year, today.Month, 1).AddMonths(-11);
            var ledgers = await _ledgerRepo.FindAsync(l => l.UserId == userId && l.Date >= startDate && l.Date <= today, ct);
            var trends = new List<DailyTrendPoint>();
            double totalDeficit = 0;
            double totalCalories = 0;
            int activeDays = 0;
            int proteinMetDays = 0;

            for (int m = 0; m < 12; m++)
            {
                var monthDate = startDate.AddMonths(m);
                var daysInMonth = DateTime.DaysInMonth(monthDate.Year, monthDate.Month);
                var monthEnd = new DateOnly(monthDate.Year, monthDate.Month, daysInMonth);
                if (monthEnd > today) monthEnd = today;

                var monthLedgers = ledgers.Where(l => l.Date.Year == monthDate.Year && l.Date.Month == monthDate.Month).ToList();
                double consumed = monthLedgers.Sum(l => l.ConsumedCalories);
                int dayCount = (monthEnd.Day - 1) + 1;
                double budget = dayCount * targetBudget;

                if (consumed > 0)
                {
                    activeDays += monthLedgers.Count(l => l.ConsumedCalories > 0);
                    double def = monthLedgers.Sum(l => l.BudgetedCalories - l.ConsumedCalories);
                    totalDeficit += def;
                    totalCalories += consumed;
                    proteinMetDays += monthLedgers.Count(l => l.ConsumedProteinGrams >= l.TargetProteinGrams * 0.85);
                }

                trends.Add(new DailyTrendPoint(
                    Date: monthDate.ToString("MMM yyyy"),
                    ConsumedCalories: Math.Round(consumed, 0),
                    BudgetCalories: Math.Round(budget, 0),
                    ProteinGrams: Math.Round(monthLedgers.Sum(l => l.ConsumedProteinGrams), 1),
                    SodiumMg: Math.Round(monthLedgers.Sum(l => l.ConsumedSodiumMg), 0),
                    HealthScore: monthLedgers.Any() ? (int)monthLedgers.Average(l => l.HealthScore) : 85
                ));
            }

            var projectedWeightLossKg = Math.Round(Math.Max(0, totalDeficit) / 7700.0, 2);
            var avgDailyCalories = activeDays > 0 ? Math.Round(totalCalories / activeDays, 0) : targetBudget;
            var proteinCompliance = activeDays > 0 ? Math.Round((double)proteinMetDays / activeDays * 100, 1) : 100.0;

            return new AnalyticsProjection(
                Period: "365D",
                TotalDeficitKcal: Math.Round(totalDeficit, 0),
                ProjectedWeightLossKg: projectedWeightLossKg,
                AverageDailyCalories: avgDailyCalories,
                ProteinCompliancePercent: proteinCompliance,
                SodiumWarningCount: 0,
                SugarWarningCount: 0,
                PlateauRiskDetected: false,
                DailyTrends: trends
            );
        }

        if (normPeriod is "90D" or "QUARTERLY")
        {
            int days = 91;
            var startDate = today.AddDays(-days + 1);
            var ledgers = await _ledgerRepo.FindAsync(l => l.UserId == userId && l.Date >= startDate && l.Date <= today, ct);
            var trends = new List<DailyTrendPoint>();
            double totalDeficit = 0;
            double totalCalories = 0;
            int activeDays = 0;
            int proteinMetDays = 0;

            for (int w = 0; w < 13; w++)
            {
                var weekStart = startDate.AddDays(w * 7);
                var weekEnd = weekStart.AddDays(6);
                if (weekEnd > today) weekEnd = today;
                if (weekStart > today) break;

                var weekLedgers = ledgers.Where(l => l.Date >= weekStart && l.Date <= weekEnd).ToList();
                double consumed = weekLedgers.Sum(l => l.ConsumedCalories);
                int daySpan = (weekEnd.DayNumber - weekStart.DayNumber) + 1;
                double budget = daySpan * targetBudget;

                if (consumed > 0)
                {
                    activeDays += weekLedgers.Count(l => l.ConsumedCalories > 0);
                    double def = weekLedgers.Sum(l => l.BudgetedCalories - l.ConsumedCalories);
                    totalDeficit += def;
                    totalCalories += consumed;
                    proteinMetDays += weekLedgers.Count(l => l.ConsumedProteinGrams >= l.TargetProteinGrams * 0.85);
                }

                trends.Add(new DailyTrendPoint(
                    Date: weekStart.ToString("MMM dd"),
                    ConsumedCalories: Math.Round(consumed, 0),
                    BudgetCalories: Math.Round(budget, 0),
                    ProteinGrams: Math.Round(weekLedgers.Sum(l => l.ConsumedProteinGrams), 1),
                    SodiumMg: Math.Round(weekLedgers.Sum(l => l.ConsumedSodiumMg), 0),
                    HealthScore: weekLedgers.Any() ? (int)weekLedgers.Average(l => l.HealthScore) : 85
                ));
            }

            var projectedWeightLossKg = Math.Round(Math.Max(0, totalDeficit) / 7700.0, 2);
            var avgDailyCalories = activeDays > 0 ? Math.Round(totalCalories / activeDays, 0) : targetBudget;
            var proteinCompliance = activeDays > 0 ? Math.Round((double)proteinMetDays / activeDays * 100, 1) : 100.0;

            return new AnalyticsProjection(
                Period: "90D",
                TotalDeficitKcal: Math.Round(totalDeficit, 0),
                ProjectedWeightLossKg: projectedWeightLossKg,
                AverageDailyCalories: avgDailyCalories,
                ProteinCompliancePercent: proteinCompliance,
                SodiumWarningCount: 0,
                SugarWarningCount: 0,
                PlateauRiskDetected: false,
                DailyTrends: trends
            );
        }

        int dayCountPeriod = normPeriod is "30D" or "MONTHLY" ? 30 : 7;
        var start = today.AddDays(-dayCountPeriod + 1);

        var periodLedgers = await _ledgerRepo.FindAsync(l => l.UserId == userId && l.Date >= start && l.Date <= today, ct);
        var defaultTrends = new List<DailyTrendPoint>();
        double defaultTotalDeficit = 0;
        double defaultTotalCalories = 0;
        int defaultProteinMetDays = 0;
        int defaultSodiumWarnings = 0;
        int defaultActiveDays = 0;

        for (int i = 0; i < dayCountPeriod; i++)
        {
            var date = start.AddDays(i);
            var l = periodLedgers.FirstOrDefault(item => item.Date == date);
            var consumed = l?.ConsumedCalories ?? 0;
            var budget = l?.BudgetedCalories ?? targetBudget;
            var protein = l?.ConsumedProteinGrams ?? 0;
            var sodium = l?.ConsumedSodiumMg ?? 0;
            var score = l?.HealthScore ?? 85;

            if (consumed > 0)
            {
                defaultActiveDays++;
                var def = budget - consumed;
                defaultTotalDeficit += def;
                defaultTotalCalories += consumed;

                if (protein >= (l?.TargetProteinGrams ?? 75) * 0.85) defaultProteinMetDays++;
                if (sodium > (l?.SodiumLimitMg ?? 2000)) defaultSodiumWarnings++;
            }

            defaultTrends.Add(new DailyTrendPoint(
                Date: date.ToString("MMM dd"),
                ConsumedCalories: consumed,
                BudgetCalories: budget,
                ProteinGrams: protein,
                SodiumMg: sodium,
                HealthScore: score
            ));
        }

        var defaultProjWeightLoss = Math.Round(Math.Max(0, defaultTotalDeficit) / 7700.0, 2);
        var defaultAvgCalories = defaultActiveDays > 0 ? Math.Round(defaultTotalCalories / defaultActiveDays, 0) : targetBudget;
        var defaultProteinCompliance = defaultActiveDays > 0 ? Math.Round((double)defaultProteinMetDays / defaultActiveDays * 100, 1) : 100.0;
        var isPlateau = dayCountPeriod >= 30 && defaultActiveDays >= 20 && defaultTotalDeficit > 15000 && defaultProjWeightLoss < 1.0;

        return new AnalyticsProjection(
            Period: dayCountPeriod == 30 ? "30D" : "7D",
            TotalDeficitKcal: Math.Round(defaultTotalDeficit, 0),
            ProjectedWeightLossKg: defaultProjWeightLoss,
            AverageDailyCalories: defaultAvgCalories,
            ProteinCompliancePercent: defaultProteinCompliance,
            SodiumWarningCount: defaultSodiumWarnings,
            SugarWarningCount: 0,
            PlateauRiskDetected: isPlateau,
            DailyTrends: defaultTrends
        );
    }

    public async Task<List<MealLog>> GetMealHistoryAsync(
        string userId,
        string? period = "7D",
        DateOnly? specificDate = null,
        MealType? mealType = null,
        CancellationToken ct = default)
    {
        var allUserMeals = await _mealRepo.FindAsync(m => m.UserId == userId, ct);
        var profile = await _profileRepo.GetByIdAsync(userId, ct);
        var userTz = GetUserTimeZoneInfo(profile?.Timezone);

        if (specificDate.HasValue)
        {
            var target = specificDate.Value;
            var filtered = allUserMeals.Where(m =>
            {
                var userLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(m.LoggedAt, userTz));
                return userLocalDate == target;
            });

            if (mealType.HasValue)
            {
                filtered = filtered.Where(m => m.MealType == mealType.Value);
            }

            return filtered.OrderByDescending(m => m.LoggedAt).ToList();
        }

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTz));
        var normPeriod = period?.Trim().ToUpperInvariant() ?? "7D";

        DateOnly startDate = normPeriod switch
        {
            "1D" or "DAILY" => today,
            "7D" or "WEEKLY" => today.AddDays(-6),
            "30D" or "MONTHLY" => today.AddDays(-29),
            "90D" or "QUARTERLY" => today.AddDays(-89),
            "365D" or "YEARLY" or "1Y" => today.AddDays(-364),
            _ => today.AddDays(-6)
        };

        var query = allUserMeals.Where(m =>
        {
            var userLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(m.LoggedAt, userTz));
            return userLocalDate >= startDate && userLocalDate <= today;
        });

        if (mealType.HasValue)
        {
            query = query.Where(m => m.MealType == mealType.Value);
        }

        return query.OrderByDescending(m => m.LoggedAt).ToList();
    }

    public static TimeZoneInfo GetUserTimeZoneInfo(string? timezoneId)
    {
        if (string.IsNullOrWhiteSpace(timezoneId))
        {
            return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(timezoneId, out var windowsId))
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                }
                catch
                {
                    // Fallback below
                }
            }
        }
        catch (InvalidTimeZoneException)
        {
            // Fallback below
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            }
            catch
            {
                return TimeZoneInfo.Utc;
            }
        }
    }
}
