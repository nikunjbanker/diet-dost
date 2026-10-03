/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Nutrition.Application.Common.Interfaces;

namespace Nutrition.Infrastructure.Services;

/// <summary>
/// Infrastructure adapter implementation of <see cref="IAppEnvironment"/>.
/// Bridges C# compile-time preprocessor flags, ASP.NET Core IHostEnvironment,
/// and explicit configuration toggles for showcase/dev environments.
/// </summary>
public class AppEnvironment : IAppEnvironment
{
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IConfiguration _configuration;

    public AppEnvironment(IHostEnvironment hostEnvironment, IConfiguration configuration)
    {
        _hostEnvironment = hostEnvironment;
        _configuration = configuration;
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

    /// <summary>
    /// Evaluates to true if either:
    /// 1. The explicit configuration toggle 'Security:AllowDemoUsers' is true (e.g. for dev.diet-dost.in showcase), OR
    /// 2. The application is compiled in Debug mode AND running in Development environment.
    /// In production environments without the explicit toggle, this strictly returns false.
    /// </summary>
    public bool AllowsDemoUsers =>
        _configuration.GetValue<bool>("Security:AllowDemoUsers") || (IsDebugMode && IsDevelopment);
}
