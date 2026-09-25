using Nutrition.Domain.Model.Identity;

namespace Nutrition.Application.Services;

/// <summary>
/// Domain service interface for dynamic, database-backed tier configuration governance.
/// </summary>
public interface ITierConfigurationService
{
    Task<TierFeatureConfiguration> GetConfigurationAsync(UserTier tier, CancellationToken ct = default);
    Task<List<TierFeatureConfiguration>> GetAllConfigurationsAsync(CancellationToken ct = default);
    Task<TierFeatureConfiguration> UpdateConfigurationAsync(TierFeatureConfiguration configuration, CancellationToken ct = default);
}
