using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Features.Meals.Queries.GetAiQuota;

public record GetAiQuotaQuery(
    string CurrentUserId,
    UserTier UserTier
) : IQuery<Result<AiUsageStatsResult>>;

public class GetAiQuotaQueryHandler : IQueryHandler<GetAiQuotaQuery, Result<AiUsageStatsResult>>
{
    private readonly ClinicalDietitianService _dietitianService;
    private readonly IAiQuotaService _quotaService;

    public GetAiQuotaQueryHandler(ClinicalDietitianService dietitianService, IAiQuotaService quotaService)
    {
        _dietitianService = dietitianService;
        _quotaService = quotaService;
    }

    public async Task<Result<AiUsageStatsResult>> HandleAsync(GetAiQuotaQuery request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentUserId))
            return Result<AiUsageStatsResult>.Unauthorized();

        var profile = await _dietitianService.GetProfileAsync(request.CurrentUserId, ct);
        var stats = await _quotaService.GetUsageStatsAsync(request.CurrentUserId, request.UserTier, profile?.Timezone, ct);

        return Result<AiUsageStatsResult>.Success(stats);
    }
}
