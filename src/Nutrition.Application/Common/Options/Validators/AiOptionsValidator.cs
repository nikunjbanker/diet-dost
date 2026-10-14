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
/// FluentValidation validator for AiOptions.
/// Validates AI provider selection, model configuration, token limits, and sampling temperature.
/// </summary>
public sealed class AiOptionsValidator : AbstractValidator<AiOptions>
{
    public AiOptionsValidator()
    {
        RuleFor(x => x.Provider)
            .NotEmpty().WithMessage("AI:Provider must not be empty.")
            .Must(p => string.Equals(p, "GoogleAI", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(p, "AzureOpenAI", StringComparison.OrdinalIgnoreCase))
            .WithMessage("AI:Provider must be either 'GoogleAI' or 'AzureOpenAI'.");

        RuleFor(x => x.MaxTokens)
            .GreaterThan(0).WithMessage("AI:MaxTokens must be greater than 0.");

        RuleFor(x => x.Temperature)
            .InclusiveBetween(0.0, 2.0).WithMessage("AI:Temperature must be between 0.0 and 2.0.");

        When(x => string.Equals(x.Provider, "GoogleAI", StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.GoogleAI.ModelId)
                .NotEmpty().WithMessage("AI:GoogleAI:ModelId must not be empty when using GoogleAI provider.");
        });

        When(x => string.Equals(x.Provider, "AzureOpenAI", StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.AzureOpenAI.DeploymentName)
                .NotEmpty().WithMessage("AI:AzureOpenAI:DeploymentName must not be empty when using AzureOpenAI provider.");
        });
    }
}
