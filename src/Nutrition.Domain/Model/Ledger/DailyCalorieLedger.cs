using Nutrition.Domain.Clinical;

namespace Nutrition.Domain.Model.Ledger;

public class DailyCalorieLedger
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public double BudgetedCalories { get; set; } = 1600.0;
    public double ConsumedCalories { get; set; } = 0.0;
    public double PendingCalories => Math.Max(0, BudgetedCalories - ConsumedCalories);

    public double TargetProteinGrams { get; set; } = 75.0;
    public double ConsumedProteinGrams { get; set; } = 0.0;

    public double TargetCarbsGrams { get; set; } = 180.0;
    public double ConsumedCarbsGrams { get; set; } = 0.0;

    public double TargetFatGrams { get; set; } = 45.0;
    public double ConsumedFatGrams { get; set; } = 0.0;

    public double TargetFiberGrams { get; set; } = 30.0;
    public double ConsumedFiberGrams { get; set; } = 0.0;

    public double TargetSugarGrams { get; set; } = 25.0; // ICMR-NIN Max 25g free sugar limit
    public double ConsumedSugarGrams { get; set; } = 0.0;

    public double SodiumLimitMg { get; set; } = 2000.0;
    public double ConsumedSodiumMg { get; set; } = 0.0;

    public double VisibleCookingOilGrams { get; set; } = 0.0;
    public double VisibleCookingOilLimitGrams { get; set; } = 25.0; // ICMR-NIN 20-25g

    public int WaterIntakeMl { get; set; } = 0;
    public int WaterTargetMl { get; set; } = 3000;

    public int ConsistencyStreakDays { get; set; } = 1;
    public int HealthScore { get; set; } = 85; // 0 - 100

    public List<string> EarnedBadges { get; set; } = new();
    public string DostMessage { get; set; } = "Namaste! Ready to nourish your body with wholesome Indian nutrition today?";

    public void RecalculateLedger(List<Model.Meal.MealLog> dayMeals)
    {
        ConsumedCalories = Math.Round(dayMeals.Sum(m => m.TotalCalories), 1);
        ConsumedProteinGrams = Math.Round(dayMeals.Sum(m => m.TotalProteinGrams), 1);
        ConsumedCarbsGrams = Math.Round(dayMeals.Sum(m => m.TotalCarbsGrams), 1);
        ConsumedFatGrams = Math.Round(dayMeals.Sum(m => m.TotalFatGrams), 1);
        ConsumedFiberGrams = Math.Round(dayMeals.Sum(m => m.TotalFiberGrams), 1);
        ConsumedSugarGrams = Math.Round(dayMeals.Sum(m => m.TotalSugarGrams), 1);
        ConsumedSodiumMg = Math.Round(dayMeals.Sum(m => m.TotalSodiumMg), 1);

        // Approximate visible cooking oil from fat and added ghee/tadka
        VisibleCookingOilGrams = Math.Round(dayMeals.Sum(m => (m.AddedGheeKcal + m.AddedTadkaKcal) / 9.0 + (m.TotalFatGrams * 0.3)), 1);

        // Compute Daily Health Score (0 - 100)
        double score = 100.0;

        // Calorie deficit penalty if over budget
        if (ConsumedCalories > BudgetedCalories)
        {
            var overKcal = ConsumedCalories - BudgetedCalories;
            score -= Math.Min(35, (overKcal / 20.0));
        }

        // Sodium penalty if over limit
        if (ConsumedSodiumMg > SodiumLimitMg)
        {
            score -= 15.0;
        }

        // Visible cooking fat penalty if over 25g
        if (VisibleCookingOilGrams > VisibleCookingOilLimitGrams)
        {
            score -= 10.0;
        }

        // Protein adherence reward
        var proteinRatio = ConsumedProteinGrams / Math.Max(1, TargetProteinGrams);
        if (proteinRatio >= 0.85) score += 5.0;

        HealthScore = (int)Math.Clamp(Math.Round(score), 0, 100);

        // Badges evaluation
        EarnedBadges.Clear();
        if (VisibleCookingOilGrams <= VisibleCookingOilLimitGrams && ConsumedCalories > 500)
            EarnedBadges.Add("Tadka Ninja 🥷");
        if (ConsumedSodiumMg <= SodiumLimitMg && ConsumedCalories > 500)
            EarnedBadges.Add("Salt Sentry 🛡️");
        if (ConsumedProteinGrams >= TargetProteinGrams * 0.9)
            EarnedBadges.Add("Protein Champion 🏆");
        if (Math.Abs(ConsumedCalories - BudgetedCalories) <= 100 && ConsumedCalories > 500)
            EarnedBadges.Add("Deficit Sniper 🎯");
        if (WaterIntakeMl >= 3000)
            EarnedBadges.Add("Hydration Maharaja 💧");

        // Cheerful Diet Dost Companion Micro-copy
        if (ConsumedCalories == 0)
        {
            DostMessage = "Namaste dost! Have you had breakfast yet? Tap the camera to log your morning chai and meal! ☀️";
        }
        else if (ConsumedCalories > BudgetedCalories)
        {
            DostMessage = $"Don't worry dost! A hearty Indian meal happens to the best of us. Let's take a brisk 15-minute post-meal walk and focus on hydration! 💪";
        }
        else if (PendingCalories <= 300)
        {
            DostMessage = $"Shabash! You've logged wholesome food and have {PendingCalories:F0} kcal left for a light evening snack or herbal green tea. Right on target! 🌟";
        }
        else
        {
            DostMessage = $"Wah! Looking great today. You've hit {ConsumedProteinGrams:F0}g protein with {PendingCalories:F0} kcal remaining in your daily budget! 🥗✨";
        }
    }
}
