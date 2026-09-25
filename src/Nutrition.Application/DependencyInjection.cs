using Microsoft.Extensions.DependencyInjection;
using Nutrition.Application.Common.CQRS;

namespace Nutrition.Application;

/// <summary>
/// Dependency injection composition root for the Application layer.
/// Scans and registers all native Command and Query handlers with the DI container.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, NativeDispatcher>();

        var assembly = typeof(DependencyInjection).Assembly;

        foreach (var type in assembly.GetTypes().Where(t => !t.IsAbstract && !t.IsInterface))
        {
            foreach (var iface in type.GetInterfaces().Where(i => i.IsGenericType))
            {
                var genericDef = iface.GetGenericTypeDefinition();
                if (genericDef == typeof(ICommandHandler<,>) || genericDef == typeof(IQueryHandler<,>))
                {
                    services.AddScoped(iface, type);
                }
            }
        }

        return services;
    }
}
