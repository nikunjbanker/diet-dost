/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using FluentValidation;

namespace Nutrition.Application.Common.Options.Validators;

/// <summary>
/// FluentValidation validator for StorageOptions.
/// </summary>
public sealed class StorageOptionsValidator : AbstractValidator<StorageOptions>
{
    public StorageOptionsValidator()
    {
        // WebRootPath is optional and defaults safely to application wwwroot if omitted
    }
}
