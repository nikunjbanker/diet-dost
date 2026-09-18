namespace Nutrition.Domain.Model.Meal;

public class AiDetectionFeedbackRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string? MealLogId { get; set; }
    public string DishName { get; set; } = string.Empty;
    public string DetectedByModel { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string Rating { get; set; } = "thumbs_up"; // "thumbs_up" | "thumbs_down"
    public string? Remarks { get; set; }
    public string? IdentifiedItemsSummary { get; set; }
    public bool RetrainingTriggered { get; set; } = false;
    public string? RetrainingOutcome { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
