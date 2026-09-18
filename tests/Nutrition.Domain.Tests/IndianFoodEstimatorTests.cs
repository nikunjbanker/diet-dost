using Nutrition.Domain.Clinical;
using Xunit;

namespace Nutrition.Domain.Tests;

public class IndianFoodEstimatorTests
{
    [Fact]
    public void Estimate_OkraWithPotato_IncludingTypo_ReturnsAccurateNutrition()
    {
        // User typed "Okra with pototo" (with typo)
        var estimate = IndianFoodEstimator.Estimate("Okra with pototo");

        Assert.Contains("Okra with Potato", estimate.NormalizedName);
        Assert.Equal(120, estimate.Calories);
        Assert.Equal(2.6, estimate.ProteinGrams);
        Assert.Equal(16.0, estimate.CarbsGrams);
        Assert.Equal(5.5, estimate.FatGrams);
    }

    [Fact]
    public void Estimate_PalakPaneer_ReturnsHighProteinGreensProfile()
    {
        var estimate = IndianFoodEstimator.Estimate("Palak Paneer");

        Assert.Equal("Palak Paneer", estimate.NormalizedName);
        Assert.Equal(220, estimate.Calories);
        Assert.Equal(12.0, estimate.ProteinGrams);
        Assert.Equal(16.0, estimate.FatGrams);
    }

    [Fact]
    public void Estimate_YellowMoongDal_ReturnsICMRCompliantLentilProfile()
    {
        var estimate = IndianFoodEstimator.Estimate("Yellow Moong Dal Tadka");

        Assert.Contains("Moong Dal", estimate.NormalizedName);
        Assert.Equal(125, estimate.Calories);
        Assert.Equal(7.0, estimate.ProteinGrams);
        Assert.Equal(18.0, estimate.CarbsGrams);
    }

    [Fact]
    public void Estimate_WholeWheatPhulka_ReturnsAccurateRotiProfile()
    {
        var estimate = IndianFoodEstimator.Estimate("Whole Wheat Phulka");

        Assert.Contains("Phulka", estimate.NormalizedName);
        Assert.Equal(80, estimate.Calories);
        Assert.Equal(2.6, estimate.ProteinGrams);
        Assert.Equal(16.0, estimate.CarbsGrams);
        Assert.Equal(0.5, estimate.FatGrams);
    }

    [Fact]
    public void Estimate_DalMakhani_ReturnsRichSlowCookedBlackLentilProfile()
    {
        var estimate = IndianFoodEstimator.Estimate("Dal Makhani");

        Assert.Equal("Dal Makhani", estimate.NormalizedName);
        Assert.Equal(240, estimate.Calories);
        Assert.Equal(8.5, estimate.ProteinGrams);
        Assert.Equal(13.0, estimate.FatGrams);
    }

    [Fact]
    public void Estimate_VegPuff_ReturnsAccurateBakeryPastryNutrition()
    {
        var estimate = IndianFoodEstimator.Estimate("Veg Puff");

        Assert.Contains("Veg Puff", estimate.NormalizedName);
        Assert.Equal(268, estimate.Calories);
        Assert.Equal(4.5, estimate.ProteinGrams);
        Assert.Equal(28.2, estimate.CarbsGrams);
        Assert.Equal(15.6, estimate.FatGrams);
    }

    [Fact]
    public void Estimate_TomatoSauce_ReturnsCondimentProfile()
    {
        var estimate = IndianFoodEstimator.Estimate("Tomato Sauce");

        Assert.Contains("Tomato", estimate.NormalizedName);
        Assert.Equal(20, estimate.Calories);
        Assert.Equal(4.6, estimate.CarbsGrams);
    }

    [Fact]
    public void Estimate_WithCupPortion_ScalesNutritionProportionally()
    {
        // 1.5 Cup of Yellow Moong Dal Tadka (Baseline: 150g -> 125 kcal, 7g protein, 18g carbs)
        // 1 Cup = 200g => 1.5 Cup = 300g (Ratio: 2.0x)
        var estimate = IndianFoodEstimator.Estimate("Yellow Moong Dal Tadka", "1.5 Cup");

        Assert.Equal("1.5 Cup", estimate.EstimatedPortion);
        Assert.Equal(300, estimate.Grams);
        Assert.Equal(250, estimate.Calories); // 125 * 2
        Assert.Equal(14.0, estimate.ProteinGrams); // 7.0 * 2
        Assert.Equal(36.0, estimate.CarbsGrams); // 18.0 * 2
    }

    [Fact]
    public void Estimate_WithSliceRangePortion_AveragesAndScalesAccurately()
    {
        // 5-6 Slices of Cucumber Salad (Baseline: 80g -> 30 kcal, 1.0g protein, 6.0g carbs)
        // Range 5-6 -> avg 5.5 slices @ 20g/slice = 110g (Ratio: 110 / 80 = 1.375x)
        var estimate = IndianFoodEstimator.Estimate("Green Salad", "5-6 Slices");

        Assert.Equal("5-6 Slices", estimate.EstimatedPortion);
        Assert.Equal(110, estimate.Grams);
        Assert.Equal(41, estimate.Calories); // Math.Round(30 * 1.375) = 41
        Assert.Equal(1.4, estimate.ProteinGrams); // Math.Round(1.0 * 1.375, 1) = 1.4
        Assert.Equal(8.3, estimate.CarbsGrams); // Math.Round(6.0 * 1.375, 1) = 8.3
    }

    [Fact]
    public void Estimate_WithExplicitGrams_ScalesDirectlyFromWeight()
    {
        // 200g of Bhindi Masala (Baseline: 100g -> 110 kcal, 2.4g protein, 10.0g carbs, 6.0g fat)
        // Explicit 200g => Ratio: 2.0x
        var estimate = IndianFoodEstimator.Estimate("Bhindi Masala", "200g");

        Assert.Equal("200g", estimate.EstimatedPortion);
        Assert.Equal(200, estimate.Grams);
        Assert.Equal(220, estimate.Calories); // 110 * 2
        Assert.Equal(4.8, estimate.ProteinGrams); // 2.4 * 2
        Assert.Equal(20.0, estimate.CarbsGrams); // 10.0 * 2
        Assert.Equal(12.0, estimate.FatGrams); // 6.0 * 2
    }

    [Fact]
    public void Estimate_WithKatoriMultiplier_ScalesByQuantity()
    {
        // 2 Katori of Palak Paneer (Baseline: 150g -> 220 kcal, 12.0g protein, 16.0g fat)
        var estimate = IndianFoodEstimator.Estimate("Palak Paneer", "2 Katori");

        Assert.Equal("2 Katori", estimate.EstimatedPortion);
        Assert.Equal(300, estimate.Grams);
        Assert.Equal(440, estimate.Calories); // 220 * 2
        Assert.Equal(24.0, estimate.ProteinGrams); // 12.0 * 2
        Assert.Equal(32.0, estimate.FatGrams); // 16.0 * 2
    }

    [Fact]
    public void Estimate_IncludesAccurateFiberAndSugarGrams()
    {
        var bhindi = IndianFoodEstimator.Estimate("Bhindi Masala");
        Assert.Equal(3.8, bhindi.FiberGrams);
        Assert.Equal(2.0, bhindi.SugarGrams);

        var dal = IndianFoodEstimator.Estimate("Yellow Moong Dal Tadka");
        Assert.Equal(4.5, dal.FiberGrams);
        Assert.Equal(1.5, dal.SugarGrams);

        var sauce = IndianFoodEstimator.Estimate("Tomato Sauce");
        Assert.Equal(0.1, sauce.FiberGrams);
        Assert.Equal(4.2, sauce.SugarGrams);

        var roti = IndianFoodEstimator.Estimate("Whole Wheat Phulka");
        Assert.Equal(2.2, roti.FiberGrams);
        Assert.Equal(0.4, roti.SugarGrams);
    }

    [Fact]
    public void Estimate_PortionScaling_ScalesFiberAndSugarProportionally()
    {
        // 1.5 Cup of Yellow Moong Dal (2.0x ratio)
        var scaledDal = IndianFoodEstimator.Estimate("Yellow Moong Dal Tadka", "1.5 Cup");
        Assert.Equal(9.0, scaledDal.FiberGrams); // 4.5 * 2
        Assert.Equal(3.0, scaledDal.SugarGrams); // 1.5 * 2

        // 200g of Bhindi Masala (2.0x ratio)
        var scaledBhindi = IndianFoodEstimator.Estimate("Bhindi Masala", "200g");
        Assert.Equal(7.6, scaledBhindi.FiberGrams); // 3.8 * 2
        Assert.Equal(4.0, scaledBhindi.SugarGrams); // 2.0 * 2
    }
}

