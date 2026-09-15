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
}
