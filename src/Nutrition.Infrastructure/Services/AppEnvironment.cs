/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
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
