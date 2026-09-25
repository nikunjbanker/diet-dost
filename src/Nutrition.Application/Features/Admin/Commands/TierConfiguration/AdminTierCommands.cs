using Microsoft.Extensions.Logging;
using Nutrition.Application.Common;
using Nutrition.Application.Common.CQRS;
using Nutrition.Application.Common.Models;
using Nutrition.Application.Services;
using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Features.Admin.Commands.TierConfiguration;

public record GetTierConfigsQuery : IQuery<Result<List<TierFeatureConfiguration>>>;

public class GetTierConfigsQueryHandler : IQueryHandler<GetTierConfigsQuery, Result<List<TierFeatureConfiguration>>>
{
    private readonly ITierConfigurationService _tierConfigService;

    public GetTierConfigsQueryHandler(ITierConfigurationService tierConfigService)
    {
        _tierConfigService = tierConfigService;
    }

    public async Task<Result<List<TierFeatureConfiguration>>> HandleAsync(GetTierConfigsQuery request, CancellationToken ct = default)
    {
        var configs = await _tierConfigService.GetAllConfigurationsAsync(ct);
        return Result<List<TierFeatureConfiguration>>.Success(configs);
    }
}

public record UpdateTierConfigCommand(
    UserTier Tier,
    int DailyAiDetectionLimit,
    bool AllowPhotoCompare,
    bool AllowDataExport,
    int AnalyticsHistoryDays,
    string Description,
    string? AdminUserId
) : ICommand<Result<UpdateTierConfigResultDto>>;

public record UpdateTierConfigResultDto(
    string Message,
    TierFeatureConfiguration Configuration
);

public class UpdateTierConfigCommandHandler : ICommandHandler<UpdateTierConfigCommand, Result<UpdateTierConfigResultDto>>
{
    private readonly ITierConfigurationService _tierConfigService;
    private readonly ILogger<UpdateTierConfigCommandHandler> _logger;

    public UpdateTierConfigCommandHandler(
        ITierConfigurationService tierConfigService,
        ILogger<UpdateTierConfigCommandHandler> logger)
    {
        _tierConfigService = tierConfigService;
        _logger = logger;
    }

    public async Task<Result<UpdateTierConfigResultDto>> HandleAsync(UpdateTierConfigCommand request, CancellationToken ct = default)
    {
        var config = await _tierConfigService.GetConfigurationAsync(request.Tier, ct);
        config.DailyAiDetectionLimit = request.DailyAiDetectionLimit;
        config.AllowPhotoCompare = request.AllowPhotoCompare;
        config.AllowDataExport = request.AllowDataExport;
        config.AnalyticsHistoryDays = request.AnalyticsHistoryDays;
        config.Description = request.Description;
        config.UpdatedByUserId = request.AdminUserId;

        var updated = await _tierConfigService.UpdateConfigurationAsync(config, ct);

        _logger.LogInformation("Admin {AdminId} updated tier config for {Tier}",
            request.AdminUserId, request.Tier);

        return Result<UpdateTierConfigResultDto>.Success(new UpdateTierConfigResultDto(
            $"Tier configuration for {request.Tier} updated successfully.",
            updated
        ));
    }
}

public record GetAiLogsQuery(
    string? UserId,
    int Limit
) : IQuery<Result<List<AiUsageLog>>>;

public class GetAiLogsQueryHandler : IQueryHandler<GetAiLogsQuery, Result<List<AiUsageLog>>>
{
    private readonly IRepository<AiUsageLog> _aiLogRepo;

    public GetAiLogsQueryHandler(IRepository<AiUsageLog> aiLogRepo)
    {
        _aiLogRepo = aiLogRepo;
    }

    public async Task<Result<List<AiUsageLog>>> HandleAsync(GetAiLogsQuery request, CancellationToken ct = default)
    {
        var logs = !string.IsNullOrWhiteSpace(request.UserId)
            ? await _aiLogRepo.FindAsync(l => l.UserId == request.UserId, ct)
            : await _aiLogRepo.GetAllAsync(ct);

        var ordered = logs
            .OrderByDescending(l => l.TimestampUtc)
            .Take(Math.Min(request.Limit, 100))
            .ToList();

        return Result<List<AiUsageLog>>.Success(ordered);
    }
}
