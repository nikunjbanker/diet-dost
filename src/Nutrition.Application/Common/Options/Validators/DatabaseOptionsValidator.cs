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
/// FluentValidation validator for DatabaseOptions.
/// Ensures database provider and connection string are non-empty.
/// </summary>
public sealed class DatabaseOptionsValidator : AbstractValidator<DatabaseOptions>
{
    public DatabaseOptionsValidator()
    {
        RuleFor(x => x.Provider)
            .NotEmpty().WithMessage("Database:Provider must not be empty.");

        RuleFor(x => x.ConnectionString)
            .NotEmpty().WithMessage("Database:ConnectionString must not be empty.");
    }
}
