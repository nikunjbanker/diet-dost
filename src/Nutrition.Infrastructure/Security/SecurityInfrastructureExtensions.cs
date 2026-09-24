using Microsoft.Extensions.DependencyInjection;
using Nutrition.Application.Common;
using Nutrition.Application.Services;
using Nutrition.Infrastructure.Services;

namespace Nutrition.Infrastructure.Security;

/// <summary>
/// Dependency injection extensions for security, cryptography, and authentication services.
/// </summary>
public static class SecurityInfrastructureExtensions
{
    public static IServiceCollection AddSecurityInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IOtpService, OtpService>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ITierConfigurationService, TierConfigurationService>();
        services.AddScoped<IAiQuotaService, AiQuotaService>();
        return services;
    }
}
