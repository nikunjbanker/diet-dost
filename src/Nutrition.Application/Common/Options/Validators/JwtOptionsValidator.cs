/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Text;
using FluentValidation;

namespace Nutrition.Application.Common.Options.Validators;

/// <summary>
/// FluentValidation validator for JwtOptions.
/// Enforces RFC 7519 security standards, non-empty issuer/audience,
/// positive expiry, and minimum 256-bit (32 bytes) HMAC-SHA256 signing key.
/// </summary>
public sealed class JwtOptionsValidator : AbstractValidator<JwtOptions>
{
    public JwtOptionsValidator()
    {
        RuleFor(x => x.Issuer)
            .NotEmpty().WithMessage("Jwt:Issuer must not be empty.");

        RuleFor(x => x.Audience)
            .NotEmpty().WithMessage("Jwt:Audience must not be empty.");

        RuleFor(x => x.ExpiryMinutes)
            .GreaterThan(0).WithMessage("Jwt:ExpiryMinutes must be greater than 0.");

        RuleFor(x => x.Key)
            .NotEmpty().WithMessage("Jwt:Key is mandatory and cannot be empty.")
            .Must(key => string.IsNullOrEmpty(key) || Encoding.UTF8.GetByteCount(key) >= 32)
            .WithMessage("Jwt:Key must be at least 32 bytes (256 bits) for HMAC-SHA256 signing security.");
    }
}
