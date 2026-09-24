using Microsoft.Extensions.DependencyInjection;
using Nutrition.Application.Common;

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
        return services;
    }
}
