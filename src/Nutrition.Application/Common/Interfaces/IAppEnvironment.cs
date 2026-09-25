namespace Nutrition.Application.Common.Interfaces;

/// <summary>
/// Port abstraction exposing compilation configuration and hosting environment.
/// Decouples application use cases from ASP.NET Core hosting primitives and provides
/// clean governance over development-only/demo capabilities.
/// </summary>
public interface IAppEnvironment
{
    /// <summary>
    /// Indicates whether the active assembly was compiled in Debug mode (#if DEBUG).
    /// Always false in Release builds (-c Release).
    /// </summary>
    bool IsDebugMode { get; }

    /// <summary>
    /// Indicates whether the application is running in the Development hosting environment.
    /// </summary>
    bool IsDevelopment { get; }

    /// <summary>
    /// True only if the application is compiled in Debug mode AND running in Development environment.
    /// In Release mode or Production environments, this strictly returns false.
    /// </summary>
    bool AllowsDemoUsers => IsDebugMode && IsDevelopment;
}
