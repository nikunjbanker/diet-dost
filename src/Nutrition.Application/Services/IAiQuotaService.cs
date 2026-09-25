using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Services;

public record AiQuotaCheckResult(
    bool IsAllowed,
    string? RejectionReason,
    int DailyLimit,
    int UsedToday,
    int RemainingCalls,
    DateTime ResetsAtUtc,
    UserTier Tier
);

public record AiUsageStatsResult(
    UserTier Tier,
    int DailyLimit,
    int UsedToday,
    int RemainingCalls,
    DateTime ResetsAtUtc,
    int UsedLast7Days,
    int UsedLast30Days,
    List<AiUsageLog> RecentOperations
);

/// <summary>
/// Domain service interface for tracking, checking, and enforcing AI usage quotas with localized midnight resets.
/// </summary>
public interface IAiQuotaService
{
    Task<AiQuotaCheckResult> CheckQuotaAsync(string userId, UserTier tier, string? userTimezone, CancellationToken ct = default);
    Task<AiUsageStatsResult> GetUsageStatsAsync(string userId, UserTier tier, string? userTimezone, CancellationToken ct = default);
    Task RecordUsageAsync(string userId, AiOperationType operationType, string modelId, int estimatedTokens, long latencyMs, bool isSuccess, string? errorReason = null, CancellationToken ct = default);
}
