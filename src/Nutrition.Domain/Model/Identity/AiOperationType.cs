namespace Nutrition.Domain.Model.Identity;

/// <summary>
/// Identifies the category of AI multimodal or LLM operations tracked in audit logs.
/// </summary>
public enum AiOperationType
{
    PhotoDetection = 0,
    TextDetection = 1,
    ProgressCompare = 2
}
