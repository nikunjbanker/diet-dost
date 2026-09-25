namespace Nutrition.Domain.Model.Identity;

/// <summary>
/// Immutable audit log record for every AI model invocation.
/// Tracks multimodal vision and LLM operations for quota tracking and observability.
/// </summary>
public class AiUsageLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// ID of the user requesting the AI operation.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Type of AI operation performed (PhotoDetection, TextDetection, ProgressCompare).
    /// </summary>
    public AiOperationType OperationType { get; set; }

    /// <summary>
    /// Identifier of the AI model executed (e.g. "gemini-3-flash-preview").
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Estimated tokens consumed by prompt and completion.
    /// </summary>
    public int EstimatedTokensUsed { get; set; }

    /// <summary>
    /// Processing duration in milliseconds.
    /// </summary>
    public double LatencyMs { get; set; }

    /// <summary>
    /// Whether the AI call succeeded and produced a valid response.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Error message or failure classification if the operation failed.
    /// </summary>
    public string? ErrorReason { get; set; }

    /// <summary>
    /// Universal UTC timestamp when the operation was initiated.
    /// </summary>
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
