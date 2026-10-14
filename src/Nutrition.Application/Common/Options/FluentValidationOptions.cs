/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Nutrition.Application.Common.Options;

/// <summary>
/// Generic options validator implementing Microsoft.Extensions.Options.IValidateOptions{TOptions}
/// using FluentValidation validators registered in the dependency injection container.
/// </summary>
public sealed class FluentValidationOptions<TOptions> : IValidateOptions<TOptions> where TOptions : class
{
    private readonly IServiceProvider _serviceProvider;
    private readonly string? _name;

    public FluentValidationOptions(string? name, IServiceProvider serviceProvider)
    {
        _name = name;
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        // Null name is used to configure all named options.
        if (_name != null && _name != name)
        {
            return ValidateOptionsResult.Skip;
        }

        ArgumentNullException.ThrowIfNull(options);

        using var scope = _serviceProvider.CreateScope();
        var validator = scope.ServiceProvider.GetService<IValidator<TOptions>>();
        if (validator == null)
        {
            return ValidateOptionsResult.Success;
        }

        var result = validator.Validate(options);
        if (result.IsValid)
        {
            return ValidateOptionsResult.Success;
        }

        var errors = result.Errors.Select(failure =>
            $"Options validation failed for '{typeof(TOptions).Name}.{failure.PropertyName}' with error: {failure.ErrorMessage}");

        return ValidateOptionsResult.Fail(errors);
    }
}
