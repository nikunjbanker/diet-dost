/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nutrition.Application.Common.Options.Validators;

namespace Nutrition.Application.Common.Options;

/// <summary>
/// Extension methods for configuring strongly-typed Options with FluentValidation in .NET Core.
/// </summary>
public static class OptionsValidationExtensions
{
    /// <summary>
    /// Attaches FluentValidation to the OptionsBuilder pipeline.
    /// </summary>
    public static OptionsBuilder<TOptions> ValidateWithFluentValidation<TOptions>(
        this OptionsBuilder<TOptions> builder) where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<IValidateOptions<TOptions>>(sp =>
            new FluentValidationOptions<TOptions>(builder.Name, sp));

        return builder;
    }

    /// <summary>
    /// Registers all Diet-Dost strongly-typed configuration options classes,
    /// discovers and registers their FluentValidation validators,
    /// and configures fail-fast startup validation (ValidateOnStart).
    /// </summary>
    public static IServiceCollection AddDietDostOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // 1. Discover and register all FluentValidation validators in this assembly
        services.AddValidatorsFromAssemblyContaining<JwtOptionsValidator>();

        // 2. Register and validate JwtOptions
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateWithFluentValidation()
            .ValidateOnStart();

        // 3. Register and validate AuthOptions
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .ValidateWithFluentValidation()
            .ValidateOnStart();

        // 4. Register and validate AiOptions
        services.AddOptions<AiOptions>()
            .Bind(configuration.GetSection(AiOptions.SectionName))
            .ValidateWithFluentValidation()
            .ValidateOnStart();

        // 5. Register and validate DatabaseOptions
        services.AddOptions<DatabaseOptions>()
            .Configure(options =>
            {
                configuration.GetSection(DatabaseOptions.SectionName).Bind(options);
                var connStr = configuration.GetConnectionString("DefaultConnection");
                if (!string.IsNullOrWhiteSpace(connStr))
                {
                    options.ConnectionString = connStr;
                }
            })
            .ValidateWithFluentValidation()
            .ValidateOnStart();

        return services;
    }
}
