using Microsoft.Extensions.Hosting;
using Nutrition.Application.Common.Interfaces;

namespace Nutrition.Infrastructure.Services;

/// <summary>
/// Infrastructure adapter implementation of <see cref="IAppEnvironment"/>.
/// Bridges C# compile-time preprocessor flags and ASP.NET Core IHostEnvironment.
/// </summary>
public class AppEnvironment : IAppEnvironment
{
    private readonly IHostEnvironment _hostEnvironment;

    public AppEnvironment(IHostEnvironment hostEnvironment)
    {
        _hostEnvironment = hostEnvironment;
    }

    /// <summary>
    /// Evaluates to true only if compiled with the DEBUG configuration (#if DEBUG).
    /// Release builds (-c Release) evaluate to false at compile-time.
    /// </summary>
    public bool IsDebugMode
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// Indicates whether ASP.NET Core hosting environment is Development.
    /// </summary>
    public bool IsDevelopment => _hostEnvironment.IsDevelopment();
}
