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

public class ClinicalDietitianService
{
    private readonly IRepository<UserProfile> _profileRepo;
    private readonly IRepository<MealLog> _mealRepo;
    private readonly IRepository<DailyCalorieLedger> _ledgerRepo;
    private readonly IUnitOfWork _uow;

    public ClinicalDietitianService(
        IRepository<UserProfile> profileRepo,
        IRepository<MealLog> mealRepo,
        IRepository<DailyCalorieLedger> ledgerRepo,
        IUnitOfWork uow)
    {
        _profileRepo = profileRepo;
        _mealRepo = mealRepo;
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
            existing.UpdatedAtUtc = profile.UpdatedAtUtc;
            await _profileRepo.UpdateAsync(existing, ct);
            await _uow.SaveChangesAsync(ct);
            profile = existing;
        }

        // Immediately update today's ledger targets with new clinical budget
        await GetOrCreateDailyLedgerAsync(profile.Id, DateOnly.FromDateTime(DateTime.UtcNow), ct);

        return profile;
    }

    public async Task<UserProfile?> GetProfileAsync(string userId, CancellationToken ct = default)
    {
        return await _profileRepo.GetByIdAsync(userId, ct);
    }

    public BmrTdeeResult CalculateTargetBudget(UserProfile profile)
    {
        return ClinicalCalculators.CalculateCaloricBudget(profile);
    }

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

        // Fetch day's meals to synchronize ledger
        var allUserMeals = await _mealRepo.FindAsync(m => m.UserId == userId, ct);
        var dayMeals = allUserMeals.Where(m => DateOnly.FromDateTime(m.LoggedAt) == date || DateOnly.FromDateTime(m.LoggedAt.ToLocalTime()) == date).ToList();
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
        await _mealRepo.AddAsync(meal, ct);
        await _uow.SaveChangesAsync(ct);

        // Update day ledger
        await GetOrCreateDailyLedgerAsync(meal.UserId, DateOnly.FromDateTime(meal.LoggedAt), ct);

        return meal;
    }

    public async Task<AnalyticsProjection> GetAnalyticsProjectionAsync(string userId, string period, CancellationToken ct = default)
    {
        var normPeriod = (period ?? "7D").ToUpperInvariant();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var profile = await _profileRepo.GetByIdAsync(userId, ct);
        var budgetObj = profile != null ? ClinicalCalculators.CalculateCaloricBudget(profile) : null;
        var targetBudget = budgetObj?.TargetCalories ?? 1600.0;
        var targetProtein = (profile != null && budgetObj != null) 
            ? ClinicalCalculators.CalculateMacroDistribution(profile, budgetObj).ProteinGrams 
            : 75.0;

        if (normPeriod is "1D" or "DAILY")
        {
            var allUserMeals = await _mealRepo.FindAsync(m => m.UserId == userId, ct);
            var meals = allUserMeals.Where(m => DateOnly.FromDateTime(m.LoggedAt) == today || DateOnly.FromDateTime(m.LoggedAt.ToLocalTime()) == today).ToList();
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
}
