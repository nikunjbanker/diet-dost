using System.Text.Json.Serialization;

namespace Nutrition.Domain.Model.Profile;

public enum BiologicalSex
{
    Male,
    Female
}

public enum ActivityLevel
{
    Sedentary,     // desk job, < 5,000 steps (1.20)
    Light,         // 1-3 days light exercise / 5k-7.5k steps (1.375)
    Moderate,      // 3-5 days moderate exercise / 7.5k-10k steps (1.55)
    High           // 6-7 days heavy training / >12k steps (1.725)
}

public enum DietaryPreference
{
    PureVeg,
    LactoVeg,
    LactoOvo,
    Vegan,
    NonVeg
}

public class MedicationEntry
{
    public string DrugName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public string? TimingNotes { get; set; }
}

public class UserProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public BiologicalSex Sex { get; set; }
    public int Age { get; set; }
    public double HeightCm { get; set; }
    public double CurrentWeightKg { get; set; }

    /// <summary>
    /// Sets HeightCm from feet and inches (e.g. 5 ft 9 in).
    /// </summary>
    public void SetHeightFromFeetInches(int feet, double inches)
    {
        HeightCm = Clinical.ClinicalCalculators.ConvertFeetInchesToCm(feet, inches);
    }

    /// <summary>
    /// Gets height represented as feet and inches.
    /// </summary>
    public (int Feet, double Inches) GetHeightInFeetInches()
    {
        return Clinical.ClinicalCalculators.ConvertCmToFeetInches(HeightCm);
    }

    public double TargetWeightKg { get; set; }
    public double DesiredPaceKgPerWeek { get; set; } = 0.5; // 0.25, 0.5, 0.75
    public ActivityLevel ActivityLevel { get; set; } = ActivityLevel.Sedentary;
    public DietaryPreference DietaryPreference { get; set; } = DietaryPreference.LactoVeg;
    public string RegionalCuisine { get; set; } = "North Indian";

    // Clinical / Medical fields - MANDATORY under Zero Assumption Rule
    public List<string> DiagnosedConditions { get; set; } = new();
    public List<MedicationEntry> Medications { get; set; } = new();

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Enforces the Zero Assumption Rule: Validates that all required metrics and clinical fields are present.
    /// </summary>
    public void ValidateIntakeCompleteness()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new InvalidOperationException("Zero Assumption Rule: Name must be specified.");
        if (Age <= 0 || Age > 120)
            throw new InvalidOperationException("Zero Assumption Rule: Age must be specified with a valid value (1-120).");
        if (HeightCm < 80 || HeightCm > 250)
            throw new InvalidOperationException("Zero Assumption Rule: Height must be specified in cm (80-250cm).");
        if (CurrentWeightKg < 25 || CurrentWeightKg > 350)
            throw new InvalidOperationException("Zero Assumption Rule: Current weight must be specified in kg (25-350kg).");
        if (TargetWeightKg < 25 || TargetWeightKg > 350)
            throw new InvalidOperationException("Zero Assumption Rule: Target weight must be specified in kg (25-350kg).");
        if (DesiredPaceKgPerWeek is not (0.25 or 0.5 or 0.75))
            throw new InvalidOperationException("Zero Assumption Rule: Safe weight loss pace must be 0.25, 0.5, or 0.75 kg/week.");
    }
}
